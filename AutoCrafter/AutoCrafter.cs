using System;
using System.Collections;
using HarmonyLib;
using HMLLibrary;
using RaftModLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// AutoCrafter v0.0.1a - Main Mod entry point.
    /// Upgrades chests into automatic crafting stations.
    /// Compatible with HML (Raft Mod Loader).
    /// </summary>
    public class AutoCrafter : Mod
    {
        // --- Public static accessors used by patches ---

        public static CStorageManager StorageManager { get; private set; }
        public static CDataManager   DataManager    { get; private set; }
        public static CModUI         ModUI          { get; private set; }
        public static CAutoCrafterNetworkManager NetworkManager { get; private set; }

        // --- Private state ---

        private Harmony   mi_harmony;
        private bool      mi_initialized;
        private Coroutine mi_craftLoopCoroutine;
        private bool      mi_sceneSavedAndCleared;

        // --- Mod lifecycle ---

        public void Start()
        {
            try
            {
                Debug.Log("[AutoCrafter] Start - v0.0.1a");

                // Resolve upgrade cost items once all assets are loaded
                CModConfig.ResolveAllCosts();

                // Initialize managers (DataManager must come first, StorageManager needs it)
                DataManager    = new CDataManager();
                StorageManager = new CStorageManager(DataManager);
                NetworkManager = new CAutoCrafterNetworkManager();

                // Apply Harmony patches
                mi_harmony = new Harmony("pp.RaftMods.AutoCrafter");
                mi_harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

                Debug.Log("[AutoCrafter] Harmony patches applied.");

                // Subscribe to Raft's load-complete event - fires after every world load.
                SaveAndLoad.LoadComplete += OnSaveLoaded;

                // Subscribe to scene changes to stop crafting when leaving the game scene.
                SceneManager.activeSceneChanged += OnActiveSceneChanged;

                // Wait for the first game scene entry to build the UI (one-time only).
                StartCoroutine(WaitForPlayer());
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoCrafter] Start failed: " + ex);
            }
        }

        public void OnModUnload()
        {
            try
            {
                SaveAndLoad.LoadComplete -= OnSaveLoaded;
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;

                mi_initialized = false;
                StopAllCoroutines();
                mi_craftLoopCoroutine = null;

                ModUI?.DestroyUI();

                // Only save if OnActiveSceneChanged has not already persisted and cleared the cache.
                // If it did, calling Save() here would overwrite the file with an empty dictionary.
                if (!mi_sceneSavedAndCleared)
                    DataManager?.Save();

                mi_harmony?.UnpatchAll("pp.RaftMods.AutoCrafter");

                StorageManager = null;
                DataManager    = null;
                ModUI          = null;
                NetworkManager = null;

                Debug.Log("[AutoCrafter] Unloaded.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoCrafter] OnModUnload failed: " + ex);
            }
        }

        // --- Late initialization (waits for game scene) ---

        private IEnumerator WaitForPlayer()
        {
            // Wait until the first game scene is active and the local player exists.
            // Only used to build the UI once - data loading is handled by OnSaveLoaded.
            while (!RAPI.IsCurrentSceneGame() || ComponentManager<Network_Player>.Value == null)
                yield return new WaitForSeconds(0.5f);

            if (ModUI == null)
            {
                try
                {
                    GameObject uiHost = new GameObject("AutoCrafter_UIHost");
                    DontDestroyOnLoad(uiHost);
                    ModUI = uiHost.AddComponent<CModUI>();
                    ModUI.BuildUI();
                    Debug.Log("[AutoCrafter] UI built.");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[AutoCrafter] Failed to build UI: " + ex);
                }
            }
        }

        // Called by Raft at the end of every RestoreRGDGame() - after ALL blocks are placed,
        // all inventories restored, and SaveAndLoad.CurrentGameFileName is set correctly.
        private void OnSaveLoaded()
        {
            try
            {
                // Stop any previous craft loop before reinitializing.
                mi_initialized = false;
                if (mi_craftLoopCoroutine != null)
                {
                    StopCoroutine(mi_craftLoopCoroutine);
                    mi_craftLoopCoroutine = null;
                }

                // A new save is being loaded - previous scene-unload guard must be cleared.
                mi_sceneSavedAndCleared = false;

                // Load mod data for the correct save file.
                // CurrentGameFileName is guaranteed to be set by Raft at this point.
                DataManager.Load();

                // Clear stale/null behaviour references from the previous scene.
                StorageManager.Clear();

                // Re-register all Storage_Small objects now present in the scene.
                Storage_Small[] allStorages = FindObjectsOfType<Storage_Small>();
                foreach (var storage in allStorages)
                    StorageManager.RegisterStorage(storage);

                mi_initialized = true;
                mi_craftLoopCoroutine = StartCoroutine(CraftLoop());

                Debug.Log("[AutoCrafter] OnSaveLoaded - " + allStorages.Length + " storages registered.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[AutoCrafter] OnSaveLoaded failed: " + ex);
            }
        }

        // Called by Unity whenever the active scene changes (e.g. game -> main menu).
        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if (next.name == Raft_Network.GameSceneName) return;

            // Leaving the game scene - stop crafting and persist state.
            mi_initialized = false;
            if (mi_craftLoopCoroutine != null)
            {
                StopCoroutine(mi_craftLoopCoroutine);
                mi_craftLoopCoroutine = null;
            }
            DataManager?.Save();
            // Clear both the behaviour registry and the data cache so stale
            // data from the last session cannot pollute the next save load.
            StorageManager?.Clear();
            DataManager?.ClearCache();
            // Signal that we already persisted and cleared; OnModUnload must not save again.
            mi_sceneSavedAndCleared = true;
        }

        // --- Craft loop ---

        private IEnumerator CraftLoop()
        {
            while (mi_initialized)
            {
                yield return new WaitForSeconds(CModConfig.CheckIntervalSeconds);

                // Only the host executes crafting; clients receive inventory sync from the host normally
                if (!Raft_Network.IsHost) continue;

                // Clean up any behaviours whose GameObjects were destroyed
                StorageManager.CleanupNullBehaviours();

                // Execute one craft tick per behaviour
                foreach (CrafterBehaviour behaviour in StorageManager.AllBehaviours)
                {
                    if (behaviour == null) continue;
                    yield return StartCoroutine(behaviour.TryCraft());
                }

                // Auto-save after each full tick
                DataManager.Save();
            }
        }

    }
}
