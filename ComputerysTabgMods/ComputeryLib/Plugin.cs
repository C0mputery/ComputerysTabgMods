using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ComputeryLib.CLI;
using ComputeryLib.Commands;
using ComputeryLib.ConfigImprovements;
using ComputeryLib.MessageLogging;
using ComputeryLib.ServerFailFixes;
using ComputeryLib.Utilities;
using ComputeryLib.VisitorLog;
using HarmonyLib;
using UnityEngine;

namespace ComputeryLib;

[DefaultExecutionOrder(-int.MaxValue / 10)]
[BepInIncompatibility("citrusbird.tabg.citruslib")]
[BepInIncompatibility("com.starterpack.tabg.contagious")]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public new static ManualLogSource Logger = null!;
    public new static ConfigFile Config = null!;
    public static readonly Harmony Harmony = new Harmony(PluginInfo.PLUGIN_GUID);
    
    private void Awake() {
        bool usingTui = ArgumentUtility.TryGetArgument("-pipeName", out string? pipeName);
        if (usingTui) { Harmony.PatchAll(typeof(ManualLogSourcePatch)); }
        
        Logger = base.Logger;
        Config = base.Config;
        
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        if (usingTui) {
            Logger.LogInfo("Detected CLI, initializing pipe handler.");
            PipeHandler.CreatePipe(pipeName!);
        }
        
        ApplyPatches();
        
        MessageLogger.InitializeLogger();
        
        Logger.LogInfo("Applied patches.");
    }
    
    private static void ApplyPatches() {
        Harmony.PatchAll(typeof(GameSettingsPatch));
        Harmony.PatchAll(typeof(MessageUtilities));
        Harmony.PatchAll(typeof(ChatMessageCommandPatch));
        Harmony.PatchAll(typeof(WorldUtility));
        Harmony.PatchAll(typeof(RoomInitRequestCommandPatch));
        Harmony.PatchAll(typeof(ServerFailPatches));
        Harmony.PatchAll(typeof(UnityTransportConnectionFixes));
        Harmony.PatchAll(typeof(TerminateUtility));
        Harmony.PatchAll(typeof(TickRateUtility));
        Harmony.PatchAll(typeof(MessageLogger));
        Harmony.PatchAll(typeof(PacketQueueUtility));
        GameEndUtility.ApplyPatches();
        MidMatchJoiningUtility.ApplyPatches();
        AutoStartUtility.ApplyPatches();
        LoggerImprover.ApplyLoggerPatches();
    }
    
    private void Start() { ChatCommandManager.RegisterCommands(); }
}