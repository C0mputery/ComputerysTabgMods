using System;
using System.IO;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class GhostLootFix {
    public static readonly float[] ResendDelays = Plugin.Config.Bind<float[]>("Network", "LootRemovalResendDelays", [30f, 60f, 600f], "Seconds after an item gets picked up to resend the pickup to every client. Used to further curb stomp ghost items in certain race conditions related to them spawning in.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return ResendDelays.Length > 0; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.RemoveWeapon))]
    private static void Prefix(GameRoom __instance, NetworkGun? weapon) {
        if (weapon == null) { return; }
        ScheduleResend(__instance, weapon.Index, 0, 0f);
    }
    
    private static void ScheduleResend(GameRoom room, int index, int step, float elapsed) {
        if (step >= ResendDelays.Length) { return; }
        
        float target = ResendDelays[step];
        room.m_server.WaitThenDoAction(Math.Max(0f, target - elapsed), () => {
            if (room.GetWeapon(index) != null) { return; }
            SendRemoval(room.m_server, index);
            ScheduleResend(room, index, step + 1, Math.Max(elapsed, target));
        });
    }
    
    private static void SendRemoval(ServerClient world, int index) {
        world.SendMessageToClients(EventCode.LootRemovedFromMap, BitConverter.GetBytes(index), byte.MaxValue, true);
    }
}