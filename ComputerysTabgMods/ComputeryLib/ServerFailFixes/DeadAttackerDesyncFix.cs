using System;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class DeadAttackerDesyncFix {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerDamageCommand), nameof(PlayerDamageCommand.Run))]
    private static void PlayerDamagePrefix(byte[] msgData, ServerClient world, byte senderIndex) {
        const int attackerIndexOffset = 0;
        const int victimIndexOffset = 1;
        const int healthOffset = 2;
        
        byte attackerIndex = msgData[attackerIndexOffset];
        if (senderIndex != attackerIndex) { return; }
        
        GameRoom room = world.GameRoomReference;
        TABGPlayerServer? attacker = room.FindPlayerOrSpectator(attackerIndex);
        TABGPlayerServer? victim = room.FindPlayer(msgData[victimIndexOffset]);
        if (attacker == null || victim == null || !attacker.IsDead || victim.IsDead) { return; }
        
        float clientHealth = BitConverter.ToSingle(msgData, healthOffset);
        if (clientHealth <= 0f && !victim.IsDowned) {
            Plugin.Logger.LogInfo($"Dead player {attacker.PlayerName} ({attackerIndex}) downed {victim.PlayerName} ({victim.PlayerIndex}) on their client, resyncing.");
            world.SendMessageToClients(EventCode.ReviveState, PacketHelper.BuildReviveFinished(victim), senderIndex, true);
        }
        world.SendMessageToClients(EventCode.PlayerHealthStateChanged, PacketHelper.BuildHealthChanged(victim), senderIndex, true);
    }
}