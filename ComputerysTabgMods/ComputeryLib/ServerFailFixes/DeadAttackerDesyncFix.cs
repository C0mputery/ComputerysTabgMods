using System;
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
            world.SendMessageToClients(EventCode.ReviveState, BuildReviveFinished(victim), senderIndex, true);
        }
        world.SendMessageToClients(EventCode.PlayerHealthStateChanged, BuildHealthChanged(victim), senderIndex, true);
    }
    
    private static byte[] BuildReviveFinished(TABGPlayerServer victim) {
        byte[] buffer = new byte[3 + sizeof(float)];
        buffer[0] = (byte)ReviveState.Finished;
        buffer[1] = victim.PlayerIndex;
        buffer[2] = byte.MaxValue; // Reviver
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, 3, sizeof(float));
        return buffer;
    }
    
    private static byte[] BuildHealthChanged(TABGPlayerServer victim) {
        byte[] buffer = new byte[1 + sizeof(float)];
        buffer[0] = victim.PlayerIndex;
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, 1, sizeof(float));
        return buffer;
    }
}