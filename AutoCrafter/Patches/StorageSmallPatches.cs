using HarmonyLib;
using UnityEngine;

namespace pp.RaftMods.AutoCrafter
{
    /// <summary>
    /// Harmony patches for Storage_Small.
    /// - Open: show the AutoCrafter UI panel for the local player.
    /// - Close: hide the UI panel.
    /// - OnFinishedPlacement: register the new chest with the storage manager.
    /// </summary>
    [HarmonyPatch(typeof(Storage_Small))]
    internal static class CStorageSmallPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Open")]
        private static void Open(Storage_Small __instance, Network_Player player)
        {
            if (player == null || !player.IsLocalPlayer) return;
            if (!Raft_Network.IsHost)
                AutoCrafter.StorageManager?.GetBehaviour(__instance.ObjectIndex)?.RequestStateFromHost();
            AutoCrafter.ModUI?.OnStorageOpened(__instance.ObjectIndex);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Close")]
        private static void Close(Storage_Small __instance, Network_Player player)
        {
            if (player == null || !player.IsLocalPlayer) return;
            AutoCrafter.ModUI?.OnStorageClosed(__instance.ObjectIndex);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnFinishedPlacement")]
        private static void OnFinishedPlacement(Storage_Small __instance)
        {
            // Skip registration during initial game load - OnSaveLoaded handles that after
            // all blocks are placed and data is loaded from disk.
            if (SaveAndLoad.IsGameLoading) return;
            AutoCrafter.StorageManager?.RegisterStorage(__instance);
        }
    }

    /// <summary>
    /// Harmony patch for Block destruction.
    /// When a Storage_Small block is destroyed, unregister it from the manager.
    /// </summary>
    [HarmonyPatch(typeof(Storage_Small))]
    internal static class CBlockDestroyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("OnDestroy")]
        private static void OnDestroy(Storage_Small __instance)
        {
            if (__instance == null) return;
            AutoCrafter.ModUI?.OnStorageDestroyed(__instance.ObjectIndex);
            AutoCrafter.StorageManager?.UnregisterStorage(__instance.ObjectIndex);
        }
    }
}
