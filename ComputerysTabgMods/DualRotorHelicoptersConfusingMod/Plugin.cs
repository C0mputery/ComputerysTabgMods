using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Epic.OnlineServices.AntiCheatCommon;
using HarmonyLib;
using Landfall.Network;

namespace DualRotorHelicoptersConfusingMod;

[HarmonyPatch]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public new static ManualLogSource Logger;
    public new static ConfigFile Config = null!;
    
    private void Awake() {
        Logger = base.Logger;
        Config = base.Config;
        
        timeInSeconds = Config.Bind("Why", "Time Between PlayerUpdateCommand For Kick", 1f, "idk kicks the player if we ever get PlayerUpdateCommand in to short of a time, do you understand how dumb this is?").Value;
        
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_GUID);
        Logger.LogInfo("Applied patches.");
    }
    
    private static float timeInSeconds = 1;
    private static readonly Dictionary<byte, DateTime> whyWouldYouWantThis = new Dictionary<byte, DateTime>();
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerUpdateCommand), nameof(PlayerUpdateCommand.Run))]
    private static void PlayerUpdateCommandPatch(ref byte[] msgData, ref ServerClient world) {
        if (msgData == null || msgData.Length == 0) {
            return;
        }
        
        byte senderId = msgData[0];
        DateTime now = DateTime.UtcNow;
        
        if (whyWouldYouWantThis.TryGetValue(senderId, out DateTime lastSeen) && now - lastSeen <= TimeSpan.FromSeconds(timeInSeconds)) {
            PlayerKickCommand.Run(senderId, world, KickReason.Invalid);
        }
        
        whyWouldYouWantThis[senderId] = now;
    }
}