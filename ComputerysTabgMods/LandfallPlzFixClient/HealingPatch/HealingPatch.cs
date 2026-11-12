using HarmonyLib;

namespace LandfallPlzFixClient.ProgressBarPatch;

/// <summary>
///     This fixes the issue when a progress bar is canceled early it creates a copy of the med item being used.
///     Again this is a mod, and is far from the proper way I would recommend fixing this in the actual game code.
/// </summary>
[HarmonyPatch(typeof(Healing))]
public static class HealingPatch {
    /// <summary>
    ///     This makes sure that healing is false before calling this.progressBar.StopAllProgress to prevent
    ///     Healing.CancelHealing from being called again.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Healing.CancelHealing))]
    public static void CancelHealingPrefix(Healing __instance) { __instance.isHealing = false; }
}