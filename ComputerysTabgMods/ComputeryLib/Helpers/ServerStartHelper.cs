using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Helpers;

[HarmonyPatch(typeof(ServerClient))]
public static class ServerStartHelper {
    public static bool HasServerStarted { get; private set; }
    
    public static void ThrowIfServerStarted(string action) { if (HasServerStarted) { throw new InvalidOperationException($"Server has already started, {action}."); } }
    
    [HarmonyPrefix]
    [HarmonyPatch(MethodType.Constructor)]
    private static void ConstructorPrefix() { HasServerStarted = true; }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ServerClient.Terminate), typeof(float), typeof(string))]
    private static void TerminatePostfix() { HasServerStarted = false; }
}
