using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class SelfDamageAttributionFix {
    public static readonly float AttributionWindow = Plugin.Config.Bind("Gameplay", "SelfDamageAttributionWindow", 15f, "Seconds that an attacker will override self-damage for kill attribution. 0 or below disables this.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AttributionWindow > 0f; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TABGPlayerServer), nameof(TABGPlayerServer.UpdateLastAttacker))]
    private static bool UpdateLastAttackerPrefix(TABGPlayerServer __instance, byte playerIndex) {
        if (playerIndex != __instance.PlayerIndex) { return true; }
        // Last attacker defaults to max value
        if (__instance.LastAttacker == byte.MaxValue || __instance.LastAttacker == __instance.PlayerIndex) { return true; }
        return Time.time - __instance.LastAttackTime >= AttributionWindow;
    }
}
