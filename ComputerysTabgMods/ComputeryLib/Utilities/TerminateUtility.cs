using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class TerminateUtility {
    public static readonly float RestartTime = Plugin.Config.Bind("Server", "RestartTime", 25f, "Time that it takes the server to reload when the game ends or when a it cannot start properly (in seconds)").Value;
    
    /// <summary>
    ///     Removes Landfalls hardcoded 10-second delay on termination to allowing our custom restart time to be accurate.
    /// </summary>
    /// <param name="time"> Harmony provided time parameter </param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.Terminate), typeof(float), typeof(string))]
    private static void AwakePrefix(ref float time) { time -= 10f; }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.EndMatch), typeof(TeamStanding))]
    private static IEnumerable<CodeInstruction> FixLandLog(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 15f));
        matcher.Repeat(match => match.SetOperandAndAdvance(RestartTime));
        return matcher.InstructionEnumeration();
    }
}