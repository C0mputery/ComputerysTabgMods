using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
using Landfall.Network.GameModes;

namespace ComputeryLib.Utilities;

public static class GameEndUtility {
    public static readonly bool DisableGameEnd = Plugin.Config.Bind("Gameplay", "DisableGameEnd", false, "When enabled, a Battle Royale match stays running after it would have ended with one or zero groups left.").Value;
    public static readonly bool KeepRunningWithZeroPlayers = Plugin.Config.Bind("Gameplay", "KeepRunningWithZeroPlayers", false, "When enabled, the game keeps running even with zero players.").Value;
    
    public static void ApplyPatches() {
        if (DisableGameEnd) { Plugin.Harmony.PatchAll(typeof(SkipGroupEndPatch)); }
        if (KeepRunningWithZeroPlayers) { Plugin.Harmony.PatchAll(typeof(SkipCheckForTerminatePatch)); }
    }
    
    private static class SkipGroupEndPatch {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(BattleRoyaleGameMode), nameof(BattleRoyaleGameMode.CheckGameState), typeof(GameState))]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            CodeMatcher matcher = new CodeMatcher(instructions);
            FieldInfo gameRoomField = AccessTools.Field(typeof(TABGBaseGameMode), nameof(TABGBaseGameMode.m_GameRoom));
            MethodInfo endMatchMethod = AccessTools.Method(typeof(GameRoom), nameof(GameRoom.EndMatch), [typeof(TeamStanding)]);
            MethodInfo changeGameStateMethod = AccessTools.Method(typeof(GameRoom), nameof(GameRoom.ChangeGameState), [typeof(GameState), typeof(bool), typeof(byte[])]);
            
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, gameRoomField),
                new CodeMatch(OpCodes.Ldnull),
                new CodeMatch(OpCodes.Callvirt, endMatchMethod)
            );
            matcher.RemoveInstructions(4);
            
            matcher.MatchForward(false,
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, gameRoomField),
                new CodeMatch(OpCodes.Ldc_I4_4),
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Ldnull),
                new CodeMatch(OpCodes.Callvirt, changeGameStateMethod)
            );
            matcher.RemoveInstructions(6);
            
            return matcher.InstructionEnumeration();
        }
    }
    
    private static class SkipCheckForTerminatePatch {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.CheckForTerminate))]
        private static bool Prefix() { return false; }
    }
}