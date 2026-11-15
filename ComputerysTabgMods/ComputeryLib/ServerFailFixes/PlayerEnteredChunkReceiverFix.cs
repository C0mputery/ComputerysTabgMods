using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class PlayerEnteredChunkReceiverFix {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerEnteredChunkCommand), nameof(PlayerEnteredChunkCommand.Run))]
    private static IEnumerable<CodeInstruction> UseNewWatchers(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // ServerChunks.Instance.GetWatchers(chunkPos) -> GetNewWatchers(chunkPos, player)
        matcher.MatchForward(false,
            new CodeMatch(instruction => instruction.Calls(AccessTools.PropertyGetter(typeof(ServerChunks), nameof(ServerChunks.Instance)))),
            new CodeMatch(instruction => instruction.IsLdarg(3)),
            new CodeMatch(instruction => instruction.Calls(AccessTools.Method(typeof(ServerChunks), nameof(ServerChunks.GetWatchers))))
        );
        matcher.RemoveInstructions(3);
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_3),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerEnteredChunkReceiverFix), nameof(GetNewWatchers)))
        );
        
        return matcher.InstructionEnumeration();
    }
    
    internal static List<TABGPlayerServer> GetNewWatchers(ChunkDataServer chunkPos, TABGPlayerServer player) {
        List<TABGPlayerServer> watchers = ServerChunks.Instance.GetWatchers(chunkPos);
        if (watchers == null) { return []; }
        
        watchers.Remove(player);
        
        // Stupid fucking hack that I hate
        ChunkDataServer[] positiveDiff = player.AreaOfInterest.GetPositiveDiff();
        watchers.RemoveAll(watcher => !positiveDiff.Contains(watcher.AreaOfInterest.GetAreasOfInterest()[8])); // 8 is the center chunk
        return watchers;
    }
}
