using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class DupeFixes {
    private const int ItemIdentifierOffset = 1;
    private const int ItemCountOffset = 5;
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropCommand), nameof(ItemDropCommand.Run))]
    [HarmonyPatch(typeof(ItemThrownCommand), nameof(ItemThrownCommand.Run))]
    private static bool RunPrefix(byte[] msgData, ServerClient world, byte sender) {
        TABGPlayerServer? player = world.GameRoomReference.FindPlayer(sender);
        if (player == null) { return false; }
        
        int itemIdentifier = BitConverter.ToInt32(msgData, ItemIdentifierOffset);
        int requestedCount = BitConverter.ToInt32(msgData, ItemCountOffset);
        int ownedCount = player.HasLoot(itemIdentifier);
        if (requestedCount <= 0 || ownedCount <= 0) {
            Plugin.Logger.LogWarning($"Player {player.PlayerName} ({sender}) tried to drop/throw {requestedCount} of item {itemIdentifier} but has {ownedCount}. Duper.");
            return false;
        }
        if (requestedCount > ownedCount) {
            Plugin.Logger.LogWarning($"Player {player.PlayerName} ({sender}) tried to drop/throw {requestedCount} of item {itemIdentifier} but has {ownedCount}. Duper.");
            Buffer.BlockCopy(BitConverter.GetBytes(ownedCount), 0, msgData, ItemCountOffset, sizeof(int));
        }
        
        return true;
    }
}
