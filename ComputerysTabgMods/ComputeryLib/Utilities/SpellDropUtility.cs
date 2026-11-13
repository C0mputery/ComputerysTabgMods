using HarmonyLib;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class SpellDropUtility {
    public static readonly bool DisableNaturalSpellDrops = Plugin.Config.Bind("Gameplay", "DisableNaturalSpellDrops", false, "Stops spell drops from naturally spawning.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return DisableNaturalSpellDrops; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Spelldrop_Server), nameof(Spelldrop_Server.Update))]
    private static bool UpdatePrefix() { return false; }
}