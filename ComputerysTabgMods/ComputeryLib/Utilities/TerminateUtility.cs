using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

public static class TerminateUtility {
    public static readonly float RestartTime = Plugin.Config.Bind("Server", "RestartTime", 25f, "Time that it takes the server to reload when the game ends or when a it cannot start properly (in seconds)").Value;
    
    public static void TerminateServer(TerminateCause cause) {
        if (WorldUtility.TryGetWorld(out ServerClient? world)) { world!.Terminate(RestartTime, cause); }
    }
    
    public static void TerminateServer(string reason) {
        if (WorldUtility.TryGetWorld(out ServerClient? world)) { world!.Terminate(RestartTime, reason); }
    }
    
    /// <summary>
    ///     Removes Landfalls hardcoded 10-second delay on termination to allowing our custom restart time to be accurate.
    /// </summary>
    /// <param name="time"> Harmony provided time parameter </param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.Terminate), typeof(float), typeof(string))]
    public static void AwakePrefix(ref float time) {
        time -= 10f;
        if (time < 0f) { time = 0f; }
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.EndMatch), typeof(TeamStanding))]
    public static IEnumerable<CodeInstruction> FixLandLog(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 15f));
        matcher.Set(OpCodes.Ldc_R4, RestartTime);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 15f));
        matcher.Set(OpCodes.Ldc_R4, RestartTime);
        return matcher.InstructionEnumeration();
    }
}