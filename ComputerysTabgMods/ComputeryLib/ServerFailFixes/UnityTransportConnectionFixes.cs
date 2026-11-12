using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using Unity.Networking.Transport;

namespace ComputeryLib.ServerFailFixes;

public static class UnityTransportConnectionFixes {
    private static readonly List<byte> DisconnectedPlayerIDBuffer = [];
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnityTransportServer), nameof(UnityTransportServer.Update))]
    private static void ClearStalePlayerIDs(UnityTransportServer __instance) {
        if (!__instance.m_isHosting || __instance.m_playerIDToConnection == null) { return; }
        
        DisconnectedPlayerIDBuffer.Clear();
        foreach (byte playerID in __instance.m_playerIDToConnection.GetSecondValues()) {
            NetworkConnection connection = __instance.m_playerIDToConnection.Get(playerID);
            if (__instance.m_networkDriver.GetConnectionState(connection) == NetworkConnection.State.Disconnected) { DisconnectedPlayerIDBuffer.Add(playerID); }
        }
        
        foreach (byte playerID in DisconnectedPlayerIDBuffer) {
            NetworkConnection connection = __instance.m_playerIDToConnection.Get(playerID);
            RemovePlayerConnection(__instance, playerID, connection);
            Plugin.Logger.LogWarning($"Unbound dead Unity Transport connection for player ID {playerID} (InternalId {connection.InternalId}).");
            
            TABGPlayerServer? player = __instance.m_ServerHandler.GameRoomReference.FindPlayer(playerID);
            if (player != null) { __instance.m_ServerHandler.HandlePlayerLeave(player); }
        }
    }
    
    // Landfall just wrote DisconnectPlayer wrong so we fully overwrite it.
    // They treated m_connections as a playerID indexable data source, so they remove random players not the one they wanted
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnityTransportServer), nameof(UnityTransportServer.DisconnectPlayer))]
    private static bool DisconnectPlayerFix(UnityTransportServer __instance, byte playerToKick) {
        if (!__instance.m_playerIDToConnection.TryGetValue(playerToKick, out NetworkConnection connection)) { return false; }
        
        __instance.m_networkDriver.Disconnect(connection);
        RemovePlayerConnection(__instance, playerToKick, connection);
        
        return false;
    }
    
    private static void RemovePlayerConnection(UnityTransportServer server, byte playerID, NetworkConnection connection) {
        server.m_playerIDToConnection.Remove(playerID);
        server.m_BufferedPackagesDIC.Remove(playerID);
        server.m_NumberOfPackagesPerConnectionDic.Remove(playerID);
        
        for (int index = 0; index < server.m_connections.Length; index++) {
            if (server.m_connections[index] == connection) { server.m_connections[index] = default; }
        }
    }
}