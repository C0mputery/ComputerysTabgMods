using System.Collections.Generic;
using System.Reflection;
using ComputeryLib.ConfigImprovements;
using ENet;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch(typeof(EnetServer))]
public static class EnetConnectionFixes {
    private static EnetServer? Server;
    private static readonly Dictionary<uint, (Peer Peer, byte PlayerID)> PeerIDToPlayer = [];
    private static readonly HashSet<byte> UsedPlayerIDs = [];
    private static readonly List<uint> GonePeerIDBuffer = [];
    
    [HarmonyPrepare] private static bool OnlyWhenEnet() { return !GameSettingsUtility.Relay; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.OnApplicationQuit))]
    private static void DeinitializeOnQuit() { Library.Deinitialize(); } // Always deinitialize ENet on application quit rather than on server kill.
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(EnetServer.StartServer))]
    private static void CaptureServer(EnetServer __instance) { Server = __instance; }
    
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(EnetServer.Update))]
    private static IEnumerable<CodeInstruction> UseMappedPlayerIDs(IEnumerable<CodeInstruction> instructions) {
        MethodInfo peerID = AccessTools.PropertyGetter(typeof(Peer), nameof(Peer.ID));
        MethodInfo playerID = AccessTools.Method(typeof(EnetConnectionFixes), nameof(GetPlayerID));
        return instructions.MethodReplacer(peerID, playerID);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(EnetServer.Update))]
    private static void ReleaseGonePeers(EnetServer __instance) {
        GonePeerIDBuffer.Clear();
        foreach (KeyValuePair<uint, (Peer Peer, byte PlayerID)> entry in PeerIDToPlayer) {
            if (IsGone(entry.Key, entry.Value.Peer)) { GonePeerIDBuffer.Add(entry.Key); }
        }
        
        foreach (uint peerID in GonePeerIDBuffer) {
            byte playerID = PeerIDToPlayer[peerID].PlayerID;
            PeerIDToPlayer.Remove(peerID);
            UsedPlayerIDs.Remove(playerID);
            __instance.m_JoiningPeers.Remove(playerID);
            __instance.m_IndexToENetPeerDic.Remove(playerID);
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(EnetServer.Kill))]
    private static void ClearPlayerIDs() {
        PeerIDToPlayer.Clear();
        UsedPlayerIDs.Clear();
        Server = null;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.FindPlayer), typeof(Peer))]
    private static bool FindPlayerByMappedID(GameRoom __instance, Peer peer, ref TABGPlayerServer __result) {
        __result = __instance.FindPlayer((byte)GetPlayerID(ref peer));
        return false;
    }
    
    public static uint GetPlayerID(ref Peer peer) {
        if (PeerIDToPlayer.TryGetValue(peer.ID, out (Peer Peer, byte PlayerID) mapped)) { return mapped.PlayerID; }
        
        byte playerID = GetFreePlayerID();
        PeerIDToPlayer[peer.ID] = (peer, playerID);
        if (playerID == byte.MaxValue) {
            Plugin.Logger.LogError($"No free player IDs for ENet peer {peer.ID}, disconnecting.");
            peer.DisconnectNow(0u);
            return playerID;
        }
        
        UsedPlayerIDs.Add(playerID);
        Plugin.Logger.LogInfo($"Mapped ENet peer {peer.ID} to player ID {playerID}.");
        return playerID;
    }
    
    private static byte GetFreePlayerID() {
        List<TABGPlayerServer> players = Server!.m_ServerHandler.GameRoomReference.Players;
        for (int id = 0; id < byte.MaxValue; id++) {
            byte playerID = (byte)id;
            if (UsedPlayerIDs.Contains(playerID)) { continue; }
            if (players.Exists(player => player.PlayerIndex == playerID)) { continue; }
            return playerID;
        }
        return byte.MaxValue;
    }
    
    private static bool IsGone(uint peerID, Peer peer) {
#pragma warning disable Harmony003
        return Native.enet_peer_get_id(peer.NativeData) != peerID;
#pragma warning restore Harmony003
    }
}