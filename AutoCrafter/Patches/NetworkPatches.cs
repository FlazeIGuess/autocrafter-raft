using HarmonyLib;
using Steamworks;

namespace pp.RaftMods.AutoCrafter
{
    [HarmonyPatch(typeof(NetworkUpdateManager), "Deserialize")]
    internal static class CNetworkUpdateManagerPatches
    {
        [HarmonyPrefix]
        private static bool Deserialize(Packet_Multiple packet, CSteamID remoteID)
        {
            if (AutoCrafter.NetworkManager == null)
                return true;

            // Return false when all messages were consumed by the mod handler.
            return AutoCrafter.NetworkManager.HandlePacket(packet, remoteID);
        }
    }
}
