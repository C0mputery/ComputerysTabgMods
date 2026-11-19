using System.Collections.Generic;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class BlessingVisibilityDesyncFix {
    // Client spawns them with every blessing in slot 0
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerEnteredChunkCommand), nameof(PlayerEnteredChunkCommand.Run))]
    private static void PlayerEnteredChunkPostfix(ServerClient world, TABGPlayerServer player, ChunkDataServer chunkPos) {
        if (!HasBlessings(player)) { return; }
        
        List<TABGPlayerServer> watchers = PlayerEnteredChunkReceiverFix.GetNewWatchers(chunkPos, player);
        if (watchers.Count <= 0) { return; }
        
        byte[] receivers = new byte[watchers.Count];
        for (int i = 0; i < receivers.Length; i++) { receivers[i] = watchers[i].PlayerIndex; }
        world.SendMessageToClients(EventCode.BlessingRecieved, PacketHelper.BuildBlessingRecieved(player), receivers, true);
    }
    
    // ChunkEntry never sends blessings
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChunkEntryCommand), nameof(ChunkEntryCommand.Run))]
    private static void ChunkEntryPostfix(ServerClient world, TABGPlayerServer _player) { 
        ServerChunks chunks = ServerChunks.Instance;
        foreach (ChunkDataServer chunk in _player.AreaOfInterest.GetPositiveDiff()) {
            List<TABGPlayerServer>? players = chunks.GetPlayers(chunk);
            if (players == null) { continue; }
            
            foreach (TABGPlayerServer other in players) {
                if (other == _player || !other.HasDropped || !HasBlessings(other)) { continue; }
                world.SendMessageToClients(EventCode.BlessingRecieved, PacketHelper.BuildBlessingRecieved(other), _player.PlayerIndex, true);
            }
        }
    }
    
    private static bool HasBlessings(TABGPlayerServer player) {
        const int noBlessing = -1;
        
        int[] blessings = player.GetBlessings();
        return blessings[0] != noBlessing || blessings[1] != noBlessing || blessings[2] != noBlessing;
    }
}
