using System;
using System.IO;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class RingHelper {
    public static bool PatchApplied { get; private set; }
    private static Vector2 RingCenter;
    private static float RingDiameter;
    
    /// <summary>
    ///     Makes a fixed ring pinned at a given location.
    /// </summary>
    /// <param name="center"> Center of the ring on the X/Z plane. </param>
    /// <param name="diameter"> Diameter in Unity meters(?). </param>
    public static void SetFixedRing(Vector2 center, float diameter) {
        ServerStartHelper.ThrowIfServerStarted("Ring settings cannot be set after server start.");
        if (diameter <= 0f) { throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "Ring diameter must be greater than 0."); }
        
        RingCenter = center;
        RingDiameter = diameter;
        
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
        ring.SyncRing(1, new Vector3(RingCenter.x, 1f, RingCenter.y), RingDiameter);
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
    
    private const float JoinCatchupDelaySeconds = 1f;
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CurrentGameWorldCommand), nameof(CurrentGameWorldCommand.Run))]
    private static void CurrentGameWorldPostfix(ServerClient world, byte playerID) {
        if (world.GameRoomReference.CurrentGameState == GameState.Started) { world.WaitThenDoAction(JoinCatchupDelaySeconds, () => SendRingToPlayer(world, playerID)); }
    }
    
    private static void SendRingToPlayer(ServerClient world, byte playerIndex) {
        TheRing ring = world.SpawnedRing;
        if (!ring.hasStarted || world.GameRoomReference.FindPlayer(playerIndex) == null) { return; }
        
        world.SendMessageToClients(EventCode.RingUpdate, BuildRingCircle(ring), playerIndex, true); // circle and ring id
        world.SendMessageToClients(EventCode.RingUpdate, [2], playerIndex, true); // start moving toward it.
        world.SendMessageToClients(EventCode.RingUpdate, BuildRingTime(ring), playerIndex, true); // travel time, past the end so it closes now
    }
    
    private static byte[] BuildRingCircle(TheRing ring) {
        byte[] buffer = new byte[2 + 4 * sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)1);
        writer.Write((byte)ring.currentRingID);
        writer.Write(ring.currentWhiteRingPosition.x);
        writer.Write(ring.currentWhiteRingPosition.y);
        writer.Write(ring.currentWhiteRingPosition.z);
        writer.Write(ring.currentWhiteSize);
        return buffer;
    }
    
    private static byte[] BuildRingTime(TheRing ring) {
        byte[] buffer = new byte[1 + sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)0);
        writer.Write(Mathf.Max(ring.GetTimeTravelled(), ring.timeToTravel));
        return buffer;
    }
}
