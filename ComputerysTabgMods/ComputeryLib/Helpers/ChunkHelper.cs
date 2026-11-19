using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class ChunkHelper {
    public static bool PatchApplied { get; private set; }
    
    /// <summary>
    /// Sets the chunk sizes
    /// </summary>
    /// <param name="size"> Size in Unity meters(?) </param>
    public static void SetChunkSize(float size) { ApplySettings(ChunkCenter, size); }
    
    /// <summary>
    /// Sets the center of the chunk system
    /// </summary>
    /// <param name="center"> This is different from the origin! Think of it like the center of the first chunk. </param>
    public static void SetChunkCenter(Vector2 center) { ApplySettings(center, ServerChunks.CHUNK_SIZE); }
    
    /// <summary>
    /// Sets the center and size of the chunk system.
    /// </summary>
    /// <param name="center"> This is different from the origin! Think of it like the center of the first chunk. </param>
    /// <param name="size"> Size in Unity meters(?) </param>
    public static void SetChunkSettings(Vector2 center, float size) { ApplySettings(center, size); }
    
    public static Vector2 ChunkCenter { get; private set; } = new Vector2(ServerChunks.CHUNK_SIZE / 2f, ServerChunks.CHUNK_SIZE / 2f);
    private static float _originX;
    private static float _originZ;
    
    private static void ApplySettings(Vector2 center, float size) {
        ServerStartHelper.ThrowIfServerRunning("Chunk settings cannot be set while the server is running.");
        if (size < 16f) { throw new ArgumentOutOfRangeException(nameof(size), size, "Chunk size must be at least 16. Chunk coordinates are sbytes (-128 to 127), any smaller leads to overflow too easily."); }
        
        ServerChunks.CHUNK_SIZE = size;
        EntityUpdatesCommand.RECEIVE_ALL_DISTANCE = size / 6f;
        EntityUpdatesCommand.RECEIVE_HALF_DISTANCE = size / 3f;
        ChunkCenter = center;
        
        Vector2 corner = ChunkCenter - new Vector2(size / 2f, size / 2f);
        _originX = Mathf.Repeat(corner.x, size);
        _originZ = Mathf.Repeat(corner.y, size);
        
        if (!PatchApplied) {
            Plugin.Harmony.PatchAll(typeof(ChunkHelper));
            PatchApplied = true;
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AreaOfInterest), nameof(AreaOfInterest.ForceClearAreas))]
    private static bool ForceClearAreasPrefix(AreaOfInterest __instance) {
        for (int i = 0; i < __instance.m_areas.Length; i++) { __instance.m_areas[i] = new ChunkDataServer(sbyte.MinValue, sbyte.MinValue); }
        return false;
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ServerNetworkPlayer), nameof(ServerNetworkPlayer.CheckPositionTiles))]
    [HarmonyPatch(typeof(ServerNetworkGun), nameof(ServerNetworkGun.CheckPositionTiles))]
    [HarmonyPatch(typeof(ServerNetworkVehicle), nameof(ServerNetworkVehicle.InternalCheckPositionTiles))]
    [HarmonyPatch(typeof(ServerNetworkVehicle), nameof(ServerNetworkVehicle.CheckPositionTiles))]
    [HarmonyPatch(typeof(TABGPlayerServer), nameof(TABGPlayerServer.InitPositionTiles))]
    private static IEnumerable<CodeInstruction> UseChunkOrigin(IEnumerable<CodeInstruction> instructions) {
        FieldInfo chunkSize = AccessTools.Field(typeof(ServerChunks), nameof(ServerChunks.CHUNK_SIZE));
        MethodInfo floorToInt = AccessTools.Method(typeof(Mathf), nameof(Mathf.FloorToInt));
        CodeMatch[] chunkCalculation = [
            new CodeMatch(instruction => instruction.LoadsField(chunkSize)),
            new CodeMatch(OpCodes.Div),
            new CodeMatch(instruction => instruction.Calls(floorToInt))
        ];
        
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // Mathf.FloorToInt(position.x / ServerChunks.CHUNK_SIZE) -> ToChunkX(position.x)
        matcher.MatchForward(false, chunkCalculation);
        matcher.RemoveInstructions(2);
        matcher.Operand = AccessTools.Method(typeof(ChunkHelper), nameof(ToChunkX));
        
        // Mathf.FloorToInt(position.z / ServerChunks.CHUNK_SIZE) -> ToChunkZ(position.z)
        matcher.MatchForward(false, chunkCalculation);
        matcher.RemoveInstructions(2);
        matcher.Operand = AccessTools.Method(typeof(ChunkHelper), nameof(ToChunkZ));
        
        return matcher.InstructionEnumeration();
    }
    
    public static int ToChunkX(float worldX) { return Mathf.FloorToInt((worldX - _originX) / ServerChunks.CHUNK_SIZE); }
    public static int ToChunkZ(float worldZ) { return Mathf.FloorToInt((worldZ - _originZ) / ServerChunks.CHUNK_SIZE); }
}