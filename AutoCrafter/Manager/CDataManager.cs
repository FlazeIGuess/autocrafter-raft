using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Handles saving and loading all AutoCrafter data for the current game session.
    /// Data is stored per save-file as JSON next to Raft's persistent data directory.
    /// </summary>
    public class CDataManager
    {
        private Dictionary<uint, CCrafterData> mi_allData = new Dictionary<uint, CCrafterData>();
        private readonly Dictionary<string, Dictionary<uint, CCrafterData>> mi_dataBySaveName =
            new Dictionary<string, Dictionary<uint, CCrafterData>>(StringComparer.Ordinal);
        private Dictionary<uint, string> mi_chestNames = new Dictionary<uint, string>();
        private string mi_lastSaveName = string.Empty;

        // --- Public API ---

        /// <summary>
        /// Returns the saved data for a chest, or null if none exists.
        /// </summary>
        public CCrafterData GetData(uint objectIndex)
        {
            mi_allData.TryGetValue(objectIndex, out CCrafterData data);
            if (data == null) return null;

            if (data.ObjectIndex != objectIndex)
            {
                Debug.LogWarning("[AutoCrafter] GetData: discarding entry for index " + objectIndex
                    + " - ObjectIndex mismatch (stored=" + data.ObjectIndex + ")");
                return null;
            }

            // Discard data that belongs to a different save file to prevent cross-save leakage.
            if (!string.IsNullOrEmpty(data.SaveName)
                && data.SaveName != SaveAndLoad.CurrentGameFileName)
            {
                Debug.LogWarning("[AutoCrafter] GetData: discarding entry for index " + objectIndex
                    + " - SaveName mismatch (stored=" + data.SaveName
                    + ", current=" + SaveAndLoad.CurrentGameFileName + ")");
                return null;
            }
            Debug.Log("[AutoCrafter] GetData(" + objectIndex + "): UpgradeLevel=" + data.UpgradeLevel
                + " SaveName=" + data.SaveName);
            return data;
        }

        /// <summary>
        /// Stores or replaces data for a chest and immediately saves to disk.
        /// </summary>
        public void SetData(CCrafterData data)
        {
            if (data == null) return;

            string currentSaveName = GetCurrentSaveName();
            if (string.IsNullOrEmpty(currentSaveName)) return;

            var currentSlice = EnsureCurrentWorldSlice(currentSaveName);
            data.SaveName = currentSaveName;
            currentSlice[data.ObjectIndex] = data;
            mi_allData = currentSlice;
            Save();
        }

        /// <summary>
        /// Removes data for a chest (on downgrade/destroy) and saves to disk.
        /// </summary>
        public void RemoveData(uint objectIndex)
        {
            string currentSaveName = GetCurrentSaveName();
            if (string.IsNullOrEmpty(currentSaveName)) return;

            var currentSlice = EnsureCurrentWorldSlice(currentSaveName);
            if (currentSlice.Remove(objectIndex))
            {
                mi_allData = currentSlice;
                Save();
            }
        }

        /// <summary>Clears the in-memory data cache without writing to disk.
        /// Call this when leaving the game scene so stale data does not bleed
        /// into the next save that is loaded.</summary>
        public void ClearCache()
        {
            mi_allData.Clear();
            mi_dataBySaveName.Clear();
            mi_chestNames.Clear();
            Debug.Log("[AutoCrafter] Data cache cleared.");
        }

        /// <summary>Loads all saved data from the current save file's JSON.</summary>
        public void Load()
        {
            mi_allData.Clear();
            mi_dataBySaveName.Clear();

            string currentSaveName = GetCurrentSaveName();
            if (string.IsNullOrEmpty(currentSaveName))
            {
                LoadChestNames();
                return;
            }

            string path = GetSavePath();
            Debug.Log("[AutoCrafter] Load() - CurrentGameFileName=" + SaveAndLoad.CurrentGameFileName
                + " path=" + path);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.Log("[AutoCrafter] No save file found at " + path + " - starting fresh.");
                LoadChestNames();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var saveFile = SaveFileFromJson(json);
                if (saveFile?.entries == null)
                {
                    Debug.LogWarning("[AutoCrafter] Load(): save file parsed but entries list is null.");
                    LoadChestNames();
                    return;
                }

                foreach (var entry in saveFile.entries)
                {
                    if (entry == null)
                        continue;

                    string saveName = string.IsNullOrEmpty(entry.SaveName) ? currentSaveName : entry.SaveName;
                    if (!mi_dataBySaveName.TryGetValue(saveName, out Dictionary<uint, CCrafterData> worldSlice))
                    {
                        worldSlice = new Dictionary<uint, CCrafterData>();
                        mi_dataBySaveName[saveName] = worldSlice;
                    }

                    // Do NOT call ResolveItems() here - storages may not be in the scene yet.
                    // CrafterBehaviour.Initialize() calls ResolveItems() after all blocks are placed.
                    entry.SaveName = saveName;
                    worldSlice[entry.ObjectIndex] = entry;
                    Debug.Log("[AutoCrafter] Loaded entry: ObjectIndex=" + entry.ObjectIndex
                        + " UpgradeLevel=" + entry.UpgradeLevel
                        + " SaveName=" + saveName);
                }

                mi_allData = EnsureCurrentWorldSlice(currentSaveName);
                Debug.Log("[AutoCrafter] Loaded " + mi_allData.Count + " current-world crafter entries from " + path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AutoCrafter] Failed to load save file: " + ex.Message);
                mi_allData = EnsureCurrentWorldSlice(currentSaveName);
            }

            LoadChestNames();
        }

        /// <summary>Saves all current data to the JSON file for the current save.</summary>
        public void Save()
        {
            string path = GetSavePath();
            if (string.IsNullOrEmpty(path)) return;

            string currentSaveName = GetCurrentSaveName();
            if (string.IsNullOrEmpty(currentSaveName)) return;

            try
            {
                var currentSnapshot = new Dictionary<uint, CCrafterData>();
                foreach (var kvp in mi_allData)
                {
                    if (kvp.Value == null)
                        continue;

                    kvp.Value.SaveName = currentSaveName;
                    currentSnapshot[kvp.Key] = kvp.Value;
                }

                // Replace only the active world slice before serialization.
                mi_dataBySaveName[currentSaveName] = currentSnapshot;
                mi_allData = currentSnapshot;

                // File is per-world (<SaveName>_AutoCrafter.json), so only serialize
                // the current world's snapshot into this file.
                var saveFile = new CCrafterSaveFile();
                foreach (var entry in currentSnapshot.OrderBy(kvp => kvp.Key))
                    saveFile.entries.Add(entry.Value);

                string json = SaveFileToJson(saveFile);
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
                Debug.Log("[AutoCrafter] Saved " + saveFile.entries.Count + " entries to " + path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AutoCrafter] Failed to save data: " + ex.Message);
            }
        }

        // --- Chest name persistence ---

        /// <summary>Returns the custom name for a chest, or empty string if none is set.</summary>
        public string GetChestName(uint objectIndex)
        {
            mi_chestNames.TryGetValue(objectIndex, out string name);
            return name ?? string.Empty;
        }

        /// <summary>Sets or clears the name for a chest and immediately saves.</summary>
        public void SetChestName(uint objectIndex, string name)
        {
            name = name?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name))
                mi_chestNames.Remove(objectIndex);
            else
                mi_chestNames[objectIndex] = name;
            SaveChestNames();
        }

        private void LoadChestNames()
        {
            mi_chestNames.Clear();
            string path = GetChestNamesSavePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                var saveFile = ChestNameSaveFileFromJson(json);
                if (saveFile?.entries == null) return;
                foreach (var entry in saveFile.entries)
                    mi_chestNames[entry.objectIndex] = entry.name;
                Debug.Log("[AutoCrafter] Loaded " + mi_chestNames.Count + " chest names.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AutoCrafter] Failed to load chest names: " + ex.Message);
            }
        }

        private void SaveChestNames()
        {
            string path = GetChestNamesSavePath();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var saveFile = new CChestNameSaveFile();
                foreach (var kvp in mi_chestNames)
                    saveFile.entries.Add(new CChestNameEntry { objectIndex = kvp.Key, name = kvp.Value });
                string json = ChestNameSaveFileToJson(saveFile);
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AutoCrafter] Failed to save chest names: " + ex.Message);
            }
        }

        // --- Private helpers ---

        private string GetSavePath()
        {
            string saveName = GetCurrentSaveName();
            if (string.IsNullOrEmpty(saveName))
            {
                Debug.LogWarning("[AutoCrafter] CurrentGameFileName is empty, cannot determine save path.");
                return null;
            }
            mi_lastSaveName = saveName;
            return Path.Combine(Application.persistentDataPath, saveName + "_AutoCrafter.json");
        }

        private string GetCurrentSaveName()
        {
            string saveName = SaveAndLoad.CurrentGameFileName;
            if (!string.IsNullOrEmpty(saveName))
                return saveName;

            return mi_lastSaveName;
        }

        private Dictionary<uint, CCrafterData> EnsureCurrentWorldSlice(string saveName)
        {
            if (!mi_dataBySaveName.TryGetValue(saveName, out Dictionary<uint, CCrafterData> worldSlice))
            {
                worldSlice = new Dictionary<uint, CCrafterData>();
                mi_dataBySaveName[saveName] = worldSlice;
            }

            return worldSlice;
        }

        private string GetChestNamesSavePath()
        {
            string main = GetSavePath();
            if (string.IsNullOrEmpty(main)) return null;
            return Path.Combine(
                Path.GetDirectoryName(main),
                Path.GetFileNameWithoutExtension(main) + "_ChestNames.json");
        }

        // --- Manual JSON helpers (JsonUtility does not work for dynamically compiled mod assemblies) ---

        private static string SaveFileToJson(CCrafterSaveFile saveFile)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"entries\":[");
            for (int i = 0; i < saveFile.entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendEntry(sb, saveFile.entries[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendEntry(System.Text.StringBuilder sb, CCrafterData d)
        {
            sb.Append("{\"ObjectIndex\":").Append(d.ObjectIndex)
              .Append(",\"SaveName\":").Append(JsonStr(d.SaveName))
              .Append(",\"UpgradeLevel\":").Append(d.UpgradeLevel)
              .Append(",\"Slots\":[");
            for (int j = 0; j < d.Slots.Count; j++)
            {
                if (j > 0) sb.Append(',');
                AppendSlot(sb, d.Slots[j]);
            }
            sb.Append("]}");
        }

        private static void AppendSlot(System.Text.StringBuilder sb, CCrafterSlot s)
        {
            sb.Append("{\"RecipeItemIndex\":").Append(s.RecipeItemIndex)
              .Append(",\"IsInfinite\":").Append(s.IsInfinite ? "true" : "false")
              .Append(",\"RemainingCount\":").Append(s.RemainingCount)
              .Append(",\"IsActive\":").Append(s.IsActive ? "true" : "false")
              .Append(",\"OutputContainerIndex\":").Append(s.OutputContainerIndex)
              .Append(",\"InputContainerIndex\":").Append(s.InputContainerIndex)
              .Append('}');
        }

        private static CCrafterSaveFile SaveFileFromJson(string json)
        {
            var result = new CCrafterSaveFile();
            if (string.IsNullOrEmpty(json)) return result;
            int arrStart = FindArrayStart(json, "entries");
            if (arrStart < 0) return result;
            int arrEnd = FindMatchingBracket(json, arrStart);
            if (arrEnd < 0) return result;
            string content = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            foreach (var entryJson in SplitTopLevelObjects(content))
            {
                var data = ParseEntry(entryJson);
                if (data != null) result.entries.Add(data);
            }
            return result;
        }

        private static CCrafterData ParseEntry(string json)
        {
            var d = new CCrafterData();
            d.ObjectIndex = JsonReadUInt(json, "ObjectIndex", 0);
            d.SaveName = JsonReadString(json, "SaveName", string.Empty);
            d.UpgradeLevel = JsonReadInt(json, "UpgradeLevel", 0);
            int slotsArr = FindArrayStart(json, "Slots");
            if (slotsArr >= 0)
            {
                int slotsEnd = FindMatchingBracket(json, slotsArr);
                if (slotsEnd >= 0)
                {
                    string slotsContent = json.Substring(slotsArr + 1, slotsEnd - slotsArr - 1);
                    foreach (var slotJson in SplitTopLevelObjects(slotsContent))
                    {
                        var slot = ParseSlot(slotJson);
                        if (slot != null) d.Slots.Add(slot);
                    }
                }
            }
            return d;
        }

        private static CCrafterSlot ParseSlot(string json)
        {
            var s = new CCrafterSlot();
            s.RecipeItemIndex = JsonReadInt(json, "RecipeItemIndex", -1);
            s.IsInfinite = JsonReadBool(json, "IsInfinite", true);
            s.RemainingCount = JsonReadInt(json, "RemainingCount", 0);
            s.IsActive = JsonReadBool(json, "IsActive", false);
            s.OutputContainerIndex = JsonReadInt(json, "OutputContainerIndex", -1);
            s.InputContainerIndex = JsonReadInt(json, "InputContainerIndex", -1);
            return s;
        }

        private static string ChestNameSaveFileToJson(CChestNameSaveFile saveFile)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"entries\":[");
            for (int i = 0; i < saveFile.entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = saveFile.entries[i];
                sb.Append("{\"objectIndex\":").Append(e.objectIndex)
                  .Append(",\"name\":").Append(JsonStr(e.name))
                  .Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static CChestNameSaveFile ChestNameSaveFileFromJson(string json)
        {
            var result = new CChestNameSaveFile();
            if (string.IsNullOrEmpty(json)) return result;
            int arrStart = FindArrayStart(json, "entries");
            if (arrStart < 0) return result;
            int arrEnd = FindMatchingBracket(json, arrStart);
            if (arrEnd < 0) return result;
            string content = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
            foreach (var entryJson in SplitTopLevelObjects(content))
            {
                uint objectIndex = JsonReadUInt(entryJson, "objectIndex", 0);
                string name = JsonReadString(entryJson, "name", string.Empty);
                if (!string.IsNullOrEmpty(name))
                    result.entries.Add(new CChestNameEntry { objectIndex = objectIndex, name = name });
            }
            return result;
        }

        // Finds the opening '[' of a named JSON array, e.g. "entries":[  returns position of '['
        private static int FindArrayStart(string json, string key)
        {
            int keyPos = json.IndexOf('"' + key + '"');
            if (keyPos < 0) return -1;
            int bracket = json.IndexOf('[', keyPos + key.Length + 2);
            return bracket;
        }

        // Returns position of the ']' that matches the '[' at openPos
        private static int FindMatchingBracket(string json, int openPos)
        {
            if (openPos < 0 || openPos >= json.Length || json[openPos] != '[') return -1;
            int depth = 1;
            bool inString = false;
            for (int i = openPos + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '[') depth++;
                else if (c == ']') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // Splits a JSON string into top-level {...} objects (handles nesting)
        private static List<string> SplitTopLevelObjects(string s)
        {
            var result = new List<string>();
            int depth = 0;
            int start = -1;
            bool inString = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        result.Add(s.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }
            return result;
        }

        private static int JsonReadInt(string json, string key, int defaultVal)
        {
            string pattern = '"' + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int start = idx + pattern.Length;
            while (start < json.Length && json[start] == ' ') start++;
            int end = start;
            if (end < json.Length && (json[end] == '-' || json[end] == '+')) end++;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == start) return defaultVal;
            int val;
            return int.TryParse(json.Substring(start, end - start), out val) ? val : defaultVal;
        }

        private static uint JsonReadUInt(string json, string key, uint defaultVal)
        {
            string pattern = '"' + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int start = idx + pattern.Length;
            while (start < json.Length && json[start] == ' ') start++;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == start) return defaultVal;
            uint val;
            return uint.TryParse(json.Substring(start, end - start), out val) ? val : defaultVal;
        }

        private static bool JsonReadBool(string json, string key, bool defaultVal)
        {
            string pattern = '"' + key + "\":";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int start = idx + pattern.Length;
            while (start < json.Length && json[start] == ' ') start++;
            if (start + 4 <= json.Length && json.Substring(start, 4) == "true") return true;
            if (start + 5 <= json.Length && json.Substring(start, 5) == "false") return false;
            return defaultVal;
        }

        private static string JsonReadString(string json, string key, string defaultVal)
        {
            string pattern = '"' + key + "\":\"";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return defaultVal;
            int start = idx + pattern.Length;
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\'&& i + 1 < json.Length)
                {
                    char next = json[++i];
                    if (next == '"') sb.Append('"');
                    else if (next == '\\') sb.Append('\\');
                    else if (next == 'n') sb.Append('\n');
                    else if (next == 'r') sb.Append('\r');
                    else if (next == 't') sb.Append('\t');
                    else { sb.Append('\\'); sb.Append(next); }
                }
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string JsonStr(string s)
        {
            return '"' + (s ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + '"';
        }
    }
}
