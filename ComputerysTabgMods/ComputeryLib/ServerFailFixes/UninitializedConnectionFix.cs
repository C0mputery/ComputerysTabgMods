using System.Collections.Generic;
using ComputeryLib.ConfigImprovements;
using HarmonyLib;
using Landfall.Network;
using Unity.Networking.Transport;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class UninitializedConnectionFix {
    private static readonly HashSet<NetworkConnection> RoomInitedConnections = [];
    
    [HarmonyPrepare] private static bool OnlyWhenRelay() { return GameSettingsPatch.Relay; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RoomInitRequestCommand), nameof(RoomInitRequestCommand.Run))]
    private static void RememberRoomInit(ServerClient world, byte playerID) {
        UnityTransportServer transport = (UnityTransportServer)world.Server;
        if (transport.m_playerIDToConnection.TryGetValue(playerID, out NetworkConnection connection)) { RoomInitedConnections.Add(connection); }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerBase), nameof(ServerBase.InternalRecieve))]
    private static bool DropGameplayBeforeRoomInit(ServerBase __instance, byte[] buffer, byte peerID) {
        if (buffer.Length == 0) { return true; }
        UnityTransportServer transport = (UnityTransportServer)__instance;
        EventCode code = (EventCode)buffer[0];
        if (code != EventCode.PlayerUpdate && code != EventCode.PlayerFire) { return true; }
        if (!transport.m_playerIDToConnection.TryGetValue(peerID, out NetworkConnection connection)) { return true; }
        if (RoomInitedConnections.Contains(connection)) { return true; }
        
        Plugin.Logger.LogWarning($"Player ID {peerID} sent {code} without a RoomInit on this connection, disconnecting.");
        transport.DisconnectPlayer(peerID);
        return false;
    }
}
