using System;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Helpers;

[HarmonyPatch]
public static class ServerStartHelper {
    public static bool ServerIsRunning { get; private set; }
    
    public static void ThrowIfServerRunning(string action) { if (ServerIsRunning) { throw new InvalidOperationException($"Server has already started, {action}."); } }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.StartGameRoom))]
    private static void StartGameRoomPrefix() { ServerIsRunning = true; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.OnDestroy))]
    private static void OnDestroyPostfix() { ServerIsRunning = false; }
}
