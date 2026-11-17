using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Helpers;

[HarmonyPatch(typeof(ServerClient))]
public static class ServerStartHelper {
    public static bool ServerIsRunning { get; private set; }
    
    public static void ThrowIfServerRunning(string action) { if (ServerIsRunning) { throw new InvalidOperationException($"Server has already started, {action}."); } }
    
    [HarmonyPrefix]
    [HarmonyPatch(MethodType.Constructor)]
    private static void ConstructorPrefix() { ServerIsRunning = true; }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(ServerClient.Terminate), typeof(float), typeof(string))]
    private static void TerminatePostfix() { ServerIsRunning = false; }
}
