using System;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class GhostLootFix {
    public static readonly float[] ResendDelays = Plugin.Config.Bind<float[]>("Network", "LootRemovalResendDelays", [30f, 60f, 600f], "Seconds after an item gets picked up to resend the pickup to every client. Used to further curb stomp ghost items in certain race conditions related to them spawning in.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return ResendDelays.Length > 0; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.RemoveWeapon))]
    private static void RemoveWeaponPrefix(GameRoom __instance, NetworkGun? weapon) {
        if (weapon == null) { return; }
        ScheduleResend(__instance, weapon.Index, 0, 0f);
    }
    
    private static void ScheduleResend(GameRoom room, int weaponIndex, int step, float elapsed) {
        if (step >= ResendDelays.Length) { return; }
        
        float target = ResendDelays[step];
        room.m_server.WaitThenDoAction(Math.Max(0f, target - elapsed), () => {
            if (room.GetWeapon(weaponIndex) != null) { return; }
            SendRemoval(weaponIndex);
            ScheduleResend(room, weaponIndex, step + 1, Math.Max(elapsed, target));
        });
    }
    
    private static void SendRemoval(int weaponIndex) {
        WorldHelper.GetWorld().SendMessageToClients(EventCode.LootRemovedFromMap, BitConverter.GetBytes(weaponIndex), byte.MaxValue, true);
    }
}