using System;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class RingHelper {
    public static bool PatchApplied { get; private set; }
    private static Vector2 _ringCenter;
    private static float _ringDiameter;
    
    /// <summary>
    /// Makes a fixed ring pinned at a given location.
    /// </summary>
    /// <param name="center"> Center of the ring on the X/Z plane. </param>
    /// <param name="diameter"> Diameter in Unity meters(?). </param>
    public static void SetFixedRing(Vector2 center, float diameter) {
        ServerStartHelper.ThrowIfServerRunning("Ring settings cannot be set while the server is running.");
        if (diameter <= 0f) { throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "Ring diameter must be greater than 0."); }
        
        _ringCenter = center;
        _ringDiameter = diameter;
        
        if (!PatchApplied) {
            Plugin.Harmony.PatchAll(typeof(RingHelper));
            PatchApplied = true;
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.SpawnTheRing))]
    private static void SpawnTheRingPostfix(ServerClient __instance) {
        TheRing ring = __instance.SpawnedRing;
        // Y has to be 1 to avoid possible bug over conflicts with the default starting position
        ring.SyncRing(1, new Vector3(_ringCenter.x, 1f, _ringCenter.y), _ringDiameter);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TheRing), nameof(TheRing.NextRing))]
    private static bool NextRingPrefix(TheRing __instance) { return !__instance.hasStarted; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.StartRing))]
    private static void StartRingPostfix(ServerClient __instance) {
        TheRing ring = __instance.SpawnedRing;
        if (!ring.isMoving) { return; }
        
        ring.timeTravelled = ring.timeToTravel;
        RingUpdateCommand.Run([0], __instance);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CurrentGameWorldCommand), nameof(CurrentGameWorldCommand.Run))]
    private static void CurrentGameWorldPostfix(ServerClient world, byte playerID) {
        const float joinCatchupDelaySeconds = 1f;
        
        if (world.GameRoomReference.CurrentGameState == GameState.Started) { world.WaitThenDoAction(joinCatchupDelaySeconds, () => SendRingToPlayer(playerID)); }
    }
    
    private static void SendRingToPlayer(byte playerIndex) {
        ServerClient world = WorldHelper.GetWorld();
        TheRing ring = world.SpawnedRing;
        if (!ring.hasStarted || world.GameRoomReference.FindPlayer(playerIndex) == null) { return; }
        
        world.SendMessageToClients(EventCode.RingUpdate, PacketHelper.BuildRingCircle(ring), playerIndex, true); // circle and ring id
        world.SendMessageToClients(EventCode.RingUpdate, [2], playerIndex, true); // start moving toward it.
        world.SendMessageToClients(EventCode.RingUpdate, PacketHelper.BuildRingTime(ring), playerIndex, true); // travel time, past the end so it closes now
    }
}