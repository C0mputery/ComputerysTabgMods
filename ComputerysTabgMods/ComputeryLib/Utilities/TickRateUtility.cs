using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch(typeof(ServerClient))]
public static class TickRateUtility {
    public static readonly int TickRate = Plugin.Config.Bind("Performance", "TickRate", 30, "Sets the server tick rate. Unknown what effects this has.").Value;
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ServerClient.Awake))]
    public static void AwakePostfix(ServerClient __instance) { __instance.SetTickRate(TickRate); }
}