using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Helpers;

[HarmonyPatch(typeof(ServerClient))]
public static class WorldHelper {
    private static ServerClient? _world;
    
    public static ServerClient GetWorld() { return _world == null ? throw new InvalidOperationException("World doesn't exist??") : _world; }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerClient.Awake))]
    private static void AwakePrefix(ServerClient __instance) { _world = __instance; }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ServerClient.OnDestroy))]
    private static void OnDestroyPostfix() { _world = null; }
}
