using System;
using System.Collections.Generic;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// All persistent data for one upgraded chest.
    /// Serialized to JSON via JsonUtility.
    /// </summary>
    [Serializable]
    public class CCrafterData
    {
        /// <summary>The chest's unique block index. Used to match data to the right chest.</summary>
        public uint ObjectIndex;

        /// <summary>Name of the save file this data belongs to.</summary>
        public string SaveName = string.Empty;

        /// <summary>
        /// Current upgrade level (0 = not upgraded, 1/2/3 = slots unlocked).
        /// Determines how many slots are active.
        /// </summary>
        public int UpgradeLevel = 0;

        /// <summary>Per-slot crafting configuration. Always has exactly UpgradeLevel entries when upgraded.</summary>
        public List<CCrafterSlot> Slots = new List<CCrafterSlot>();

        public CCrafterData() { }

        public CCrafterData(uint objectIndex)
        {
            ObjectIndex = objectIndex;
        }

        /// <summary>Returns true if this chest has been upgraded to any level.</summary>
        public bool IsUpgraded => UpgradeLevel > 0;

        /// <summary>
        /// Ensures Slots list has at least 'count' entries.
        /// New slots are appended with default values.
        /// </summary>
        public void EnsureSlotCount(int count)
        {
            while (Slots.Count < count)
                Slots.Add(new CCrafterSlot());
        }

        /// <summary>Resolves all cached item and storage references after loading from JSON.</summary>
        public void ResolveItems()
        {
            foreach (var slot in Slots)
            {
                slot.ResolveItem();
                slot.ResolveOutputStorage();
                slot.ResolveInputStorage();
            }
        }
    }

    /// <summary>
    /// Wrapper for JsonUtility serialization of a list of CCrafterData.
    /// JsonUtility cannot serialize a top-level List, so we wrap it.
    /// </summary>
    [Serializable]
    public class CCrafterSaveFile
    {
        public List<CCrafterData> entries = new List<CCrafterData>();
    }

    /// <summary>One entry in the chest name save file.</summary>
    [Serializable]
    public class CChestNameEntry
    {
        public uint objectIndex;
        public string name;
    }

    /// <summary>Wrapper so JsonUtility can serialize the chest name list.</summary>
    [Serializable]
    public class CChestNameSaveFile
    {
        public List<CChestNameEntry> entries = new List<CChestNameEntry>();
    }
}
