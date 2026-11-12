using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FMODUnity;
using HarmonyLib;
using Landfall.Network;
using Landfall.TABG;

namespace ComputeryLib.MessageLogging;

public static class LoggerImprover {
    public static void ApplyLoggerPatches() {
        Type nestedType = typeof(CommunityBackendAPI).GetNestedType("<>c__DisplayClass7_0", BindingFlags.NonPublic);
        MethodInfo? targetMethod = nestedType.GetMethod("<GameServerHeartbeat>b__0", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        Plugin.Harmony.Patch(targetMethod, transpiler: new HarmonyMethod(typeof(LoggerImprover), nameof(SuppressCommunityBackendHeartbeatLogs)));
        Plugin.Harmony.PatchAll(typeof(LoggerImprover));
    }
    
    public static IEnumerable<CodeInstruction> SuppressCommunityBackendHeartbeatLogs(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Heartbeat sent! Code: "));
        matcher.RemoveInstructions(13);
        return matcher.InstructionEnumeration();
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(LandLog), nameof(LandLog.Log), typeof(string), typeof(object))]
    public static IEnumerable<CodeInstruction> FixLandLog(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Console), nameof(Console.WriteLine), [typeof(string)])));
        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(Console), nameof(Console.Write), [typeof(string)]));
        return matcher.InstructionEnumeration();
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.EndMatch))]
    [HarmonyPatch(typeof(TeamDeadCommand), nameof(TeamDeadCommand.Run))]
    public static IEnumerable<CodeInstruction> FixPlayFabDisabledLog(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Playfab Is Disabled!"));
        matcher.RemoveInstructions(3);
        return matcher.InstructionEnumeration();
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlayerLeaveCommand), nameof(PlayerLeaveCommand.Run))]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Playfab Is Disabled!"));
        List<Label> labelsToPreserve = new List<Label>(matcher.Instruction.labels);
        matcher.RemoveInstructions(3);
        matcher.Instruction.labels.AddRange(labelsToPreserve);
        
        return matcher.InstructionEnumeration();
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RuntimeManager), nameof(RuntimeManager.LoadBank), typeof(string), typeof(bool))]
    [HarmonyPatch(typeof(RuntimeManager), nameof(RuntimeManager.CreateInstance), typeof(string))]
    [HarmonyPatch(typeof(StudioEventEmitter), nameof(StudioEventEmitter.Start))]
    public static bool Disable() { return false; }
}