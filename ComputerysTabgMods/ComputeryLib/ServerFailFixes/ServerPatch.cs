using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using ComputeryLib.Utilities;
using ENet;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

public class ServerFailPatches {
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(EnetServer), nameof(EnetServer.StartServer))]
    [HarmonyPatch(typeof(UnityTransportServer), nameof(UnityTransportServer.StartServer))]
    [HarmonyPatch(typeof(JobifiedUnityTransportServer), nameof(JobifiedUnityTransportServer.StartServer))] // this one is never used to my knowlage
    private static Exception StartServerFinalizer(Exception? __exception) {
        if (__exception == null) { return null!; }
        
        // When the init fails we need to make sure the server does not try to use the half-initialized server instance.
        // Note, this does not scale and stuff.
        ServerClient.m_Server = null!;
        
        TerminateUtility.TerminateServer("Server failed to start properly. Terminating connection.");
        return __exception;
    }
    
    /// <summary>
    ///     Actually protect from malformed packets causing massive lag spikes and shit
    ///     AND mAKE SURE THE PACKETS GET DISCARDED SO NO MEM LEAKS OMG LANDFALL PLEASE
    /// </summary>
    /// <param name="__exception"> harmony provided exception </param>
    /// <returns> null to swallow exception </returns>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ServerBase), nameof(ServerBase.InternalRecieve))]
    private static Exception InternalRecieveFinalizer(Exception? __exception) {
        if (__exception != null) { Plugin.Logger.LogError(__exception); }
        return null!;
    }
    
    // Flush the server and do not de-init enet.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EnetServer), nameof(EnetServer.Kill))]
    public static bool KillPostfix(EnetServer __instance) {
        __instance.Host.Flush();
        __instance.Host.Dispose();
        ServerClient.m_Server = null; // We gotta remove this or it will try to reuse the disposed host.
        return false;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.OnApplicationQuit))]
    public static void OnApplicationQuitPostfix() {
        Library.Deinitialize(); // Always deinitialize ENet on application quit rather than on server kill.
    }
    
    /// <summary>
    ///     ALRIGHT LANDFALL NEVER CHECKS IF THEY ARE GETTING A PLAYER FROM THE ACTUAL PLAYER OR ANOTHER PERSON CAUSING QUANTUM
    ///     ENTANGLING
    ///     at least I think this fixes quantum entanging
    /// </summary>
    /// <param name="instructions"></param>
    /// <returns></returns>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.HandleNetorkEvent))]
    public static IEnumerable<CodeInstruction> FixRun(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        Type nestedType = typeof(ServerClient).GetNestedType("<>c__DisplayClass84_0", BindingFlags.NonPublic);
        FieldInfo sender = AccessTools.Field(nestedType, "sender");
        
        matcher.MatchForward(
            false,
            new CodeMatch(OpCodes.Ldloc_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ServerClient).GetNestedType("<>c__DisplayClass84_0", BindingFlags.NonPublic), "serverData")),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(PlayerUpdateCommand), "Run"))
        );
        
        matcher.Advance(3);
        matcher.InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Ldfld, sender)
        );
        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(ServerFailPatches), nameof(Run), [typeof(byte[]), typeof(ServerClient), typeof(byte)]));
        
        return matcher.InstructionEnumeration();
    }
    
    public static void Run(byte[] msgData, ServerClient world, byte sender) {
        if (msgData[0] != sender) {
            Plugin.Logger.LogError($"Received PlayerUpdateCommand from sender {sender}, but expected {msgData[0]}. Ignoring.");
            return;
        }
        PlayerUpdateCommand.Run(msgData, world);
    }
    
    // Landfall never closes their connection via the player leave command.
    // Without this, there's a shitload of spam
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerLeaveCommand), nameof(PlayerLeaveCommand.Run))]
    private static void DisconnectLeavingPlayer(ServerClient world, TABGPlayerServer player) { world.Server?.DisconnectPlayer(player.PlayerIndex); }
    
    // After the last ring Landfall calls StartMoving every frame for no reason, this stops that
    // This also makes a shitload of spam
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TheRing), nameof(TheRing.StartMoving))]
    private static bool SkipNoOpStartMoving(TheRing __instance) {
        return !(__instance.hasStarted && !__instance.isMoving && __instance.currentWhiteRingPosition == __instance.endPosition);
    }
}