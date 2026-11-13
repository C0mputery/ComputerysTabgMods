using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using Unity.Networking.Transport;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class UninitializedConnectionFix {
    private static readonly HashSet<NetworkConnection> RoomInitedConnections = [];
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RoomInitRequestCommand), nameof(RoomInitRequestCommand.Run))]
    private static void RememberRoomInit(ServerClient world, byte playerID) {
        if (world.Server is not UnityTransportServer transport) { return; }
        if (transport.m_playerIDToConnection.TryGetValue(playerID, out NetworkConnection connection)) { RoomInitedConnections.Add(connection); }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerBase), nameof(ServerBase.InternalRecieve))]
    private static bool DropGameplayBeforeRoomInit(ServerBase __instance, byte[] buffer, byte peerID) {
        if (__instance is not UnityTransportServer transport || buffer.Length == 0) { return true; }
        EventCode code = (EventCode)buffer[0];
        if (code != EventCode.PlayerUpdate && code != EventCode.PlayerFire) { return true; }
        if (!transport.m_playerIDToConnection.TryGetValue(peerID, out NetworkConnection connection)) { return true; }
        if (RoomInitedConnections.Contains(connection)) { return true; }
        
        Plugin.Logger.LogWarning($"Player ID {peerID} sent {code} without a RoomInit on this connection, disconnecting.");
        transport.DisconnectPlayer(peerID);
        return false;
    }
}
