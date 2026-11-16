using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class ChunkEntryLootOrder {
    public static readonly bool SortByDistance = Plugin.Config.Bind("Network", "SortChunkEntryLootByDistance", true, "Sorts chunk entry loot order by distance from the player").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return SortByDistance; }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ChunkEntryCommand), nameof(ChunkEntryCommand.Run))]
    private static IEnumerable<CodeInstruction> SortLoot(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // if (world.GameRoomReference.CurrentGameState == GameState.WaitingForPlayers)
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ServerClient), nameof(ServerClient.GameRoomReference))),
            new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(GameRoom), nameof(GameRoom.CurrentGameState)))
        );
        
        List<Label> labelsToPreserve = [.. matcher.Instruction.labels];
        matcher.Instruction.labels.Clear();
        
        // SortByDistanceFrom(list3, _player);
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_3),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChunkEntryLootOrder), nameof(SortByDistanceFrom)))
        );
        
        matcher.Instruction.labels.AddRange(labelsToPreserve);
        
        return matcher.InstructionEnumeration();
    }
    
    private static void SortByDistanceFrom(List<NetworkGun> loot, TABGPlayerServer player) {
        Vector3 origin = player.PlayerPosition;
        NetworkGun[] guns = loot.ToArray();
        float[] distances = new float[guns.Length];
        for (int i = 0; i < guns.Length; i++) { distances[i] = (guns[i].Position - origin).sqrMagnitude; }
        Array.Sort(distances, guns);
        loot.Clear();
        loot.AddRange(guns);
    }
}
