using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class AggressiveRingUtility {
    public static readonly bool AggressiveRingChecks = Plugin.Config.Bind("Gameplay", "AggressiveRingChecks", false, "Instantly kills any player who is outside the last closed ring.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AggressiveRingChecks; }
    
    private static bool _ringHasClosedEver;
    private static Vector3 _closedRingCenter;
    private static float _closedRingRadiusSquared;
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TheRing), nameof(TheRing.Awake))]
    private static void AwakePostfix() { _ringHasClosedEver = false; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TheRing), nameof(TheRing.StopRing))]
    private static void StopRingPostfix(TheRing __instance) {
        float radius = __instance.currentWhiteSize * 0.5f;
        _closedRingCenter = __instance.currentWhiteRingPosition;
        _closedRingRadiusSquared = radius * radius;
        _ringHasClosedEver = true;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TheRing), nameof(TheRing.Update))]
    private static void UpdatePostfix() {
        if (!_ringHasClosedEver) { return; }
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) { return; }
        
        List<TABGPlayerServer> players = world.GameRoomReference.Players;
        for (int i = players.Count - 1; i >= 0; i--) {
            TABGPlayerServer player = players[i];
            if (player.IsDead || !player.HasDropped) { continue; }
            
            Vector3 position = player.PlayerPosition;
            float offsetX = position.x - _closedRingCenter.x;
            float offsetZ = position.z - _closedRingCenter.z;
            float distanceFromCenterSquared = offsetX * offsetX + offsetZ * offsetZ;
            if (distanceFromCenterSquared <= _closedRingRadiusSquared) { continue; }
            
            Plugin.Logger.LogInfo($"Player {player.PlayerName} is outside of the closed ring, killing.");
            ServerKillCommand.Run(world, player);
        }
    }
}