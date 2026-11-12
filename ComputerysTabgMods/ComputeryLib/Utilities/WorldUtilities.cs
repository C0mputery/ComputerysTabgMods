using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch(typeof(ServerClient))]
public static class WorldUtility {
    private static ServerClient? _worldClient;
    
    public static bool TryGetWorld(out ServerClient? world) {
        if (_worldClient) {
            world = _worldClient;
            return true;
        }
        world = null;
        return false;
    }
    
    public static ServerClient GetWorld() { return _worldClient == null ? throw new InvalidOperationException("World client has not been set yet.") : _worldClient; }
    
    internal static void SetWorldClient(ServerClient world) { _worldClient = world; }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerClient.Awake))]
    public static void AwakePrefix(ref ServerClient __instance) { SetWorldClient(__instance); }
}