using System;
using ComputeryLib.Helpers;
using ComputeryLib.Utilities;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class DeadAttackerDesyncFix {
    private const int AttackerIndexOffset = 0;
    private const int VictimIndexOffset = 1;
    private const int HealthOffset = 2;
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerDamageCommand), nameof(PlayerDamageCommand.Run))]
    private static void RunPrefix(byte[] msgData, ServerClient world, byte senderIndex) {
        byte attackerIndex = msgData[AttackerIndexOffset];
        if (senderIndex != attackerIndex) { return; }
        
        GameRoom room = world.GameRoomReference;
        TABGPlayerServer? attacker = room.FindPlayer(attackerIndex);
        TABGPlayerServer? victim = room.FindPlayer(msgData[VictimIndexOffset]);
        if (attacker == null || victim == null || !attacker.IsDead || victim.IsDead) { return; }
        
        float clientHealth = BitConverter.ToSingle(msgData, HealthOffset);
        if (clientHealth <= 0f && !victim.IsDowned) {
            Plugin.Logger.LogInfo($"Dead player {attacker.PlayerName} ({attackerIndex}) downed {victim.PlayerName} ({victim.PlayerIndex}) on their client, resyncing.");
            world.SendMessageToClients(EventCode.ReviveState, PacketHelper.BuildReviveFinished(victim), senderIndex, true);
        }
        world.SendMessageToClients(EventCode.PlayerHealthStateChanged, PacketHelper.BuildHealthChanged(victim), senderIndex, true);
    }
}