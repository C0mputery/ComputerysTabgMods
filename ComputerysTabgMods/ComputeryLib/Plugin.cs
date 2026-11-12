using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ComputeryLib.Commands;
using ComputeryLib.MessageLogging;
using ComputeryLib.Utilities;
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
        Logger = base.Logger;
        Config = base.Config;
        
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        
        ApplyPatches();
        
        MessageLogger.InitializeLogger();
        
        Logger.LogInfo("Applied patches.");
    }
    
    private static void ApplyPatches() {
        Harmony.PatchAll(Assembly.GetExecutingAssembly());
        GameEndUtility.ApplyPatches();
        MidMatchJoiningUtility.ApplyPatches();
        AutoStartUtility.ApplyPatches();
        AggressiveRingUtility.ApplyPatches();
        LoggerImprover.ApplyHeartbeatPatch();
    }
    
    private void Start() { ChatCommandManager.RegisterCommands(); }
}