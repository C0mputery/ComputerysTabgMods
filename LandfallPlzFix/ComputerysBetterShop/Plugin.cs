using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ComputerysBetterShop;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin { 
    public new static ManualLogSource Logger;
        
    private void Awake() {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_GUID);
        Logger.LogInfo($"Applied patches.");
    }
}