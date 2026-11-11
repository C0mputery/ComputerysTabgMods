using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Epic.OnlineServices.AntiCheatClient;
using HarmonyLib;
using Landfall;

namespace AntiCheatBootErrorRemover;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    private void Awake() {
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_GUID);
        Logger.LogInfo($"SetACInterface has been patched, 'Anti Cheat Boot Error...' will no longer appear.");
    }
}
    
[HarmonyPatch(typeof(Easy_AC_Client), nameof(Easy_AC_Client.SetACInterface))] 
public static class Easy_AC_Client_Patch {
    [HarmonyPrefix]
    public static bool SetACInterface_Prefix(AntiCheatClientInterface ACInterface) {
            Easy_AC_Client.m_Interface = ACInterface;
            return false;
    }
}