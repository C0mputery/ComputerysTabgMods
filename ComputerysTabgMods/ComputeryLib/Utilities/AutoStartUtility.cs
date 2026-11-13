using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class AutoStartUtility {
    public static readonly bool AutoStart = Plugin.Config.Bind("Gameplay", "AutoStart", false, "Makes the match insta start. Probably only use this when you have midmatch joining enabled.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AutoStart; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.OnHosted))]
    private static void OnHostedPostfix(ServerClient __instance) { __instance.GameRoomReference.CurrentGameMode.RunStart(); }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.StartFlying))]
    private static bool StartFlyingPrefix() { return false; }
}