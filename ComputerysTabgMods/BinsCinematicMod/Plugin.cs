using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DeepSky.Haze;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BinsCinematicMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public new static ManualLogSource Logger;
    public new static ConfigFile Config;
    
    private void Awake() {
        Config = base.Config;
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        
        Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        
        if (Config.Bind("General", "LOD Patch", true, "Disabled the LOD system.").Value) {
            harmony.PatchAll(typeof(WilhelmChunkerPatches));
            Logger.LogInfo("Applied LOD patch.");
        }
        
        if (Config.Bind("General", "Gear Layer Patch", true, "Makes gear always render").Value) {
            harmony.PatchAll(typeof(CharacterGearHandlerPatches));
            Logger.LogInfo("Applied Gear Layer patch.");
        }
        
        Config.Bind("General", "Fog Start Distance", 0.03f, "Sets the fog start distance.");
        Config.Bind("General", "Time of Day Override", false, "If true, the time of day will be overridden to the specified value.");
        Config.Bind("General", "Time of Day Value", 0.5f, "The time of day value to override is a value 0 - 1");
        
        harmony.PatchAll(typeof(OptionsPatches));
        harmony.PatchAll(typeof(TimeOfDayPatch));
        
        Logger.LogInfo("Applied patches.");
    }
    
    private void OnEnable() { SceneManager.activeSceneChanged += OnSceneChanged; }
    
    private void OnDisable() { SceneManager.activeSceneChanged -= OnSceneChanged; }
    
    private void OnSceneChanged(Scene current, Scene next) {
        Logger.LogInfo($"Scene changed from {current.name} to {next.name}");
        GameObject hazeZone = GameObject.Find("/MapObjects/DS_HazeController/DS_HazeZone");
        if (hazeZone == null) { return; }
        DS_HazeZone __instance = hazeZone.GetComponent<DS_HazeZone>();
        __instance.Context.m_ContextItems[0].m_FogStartDistance = Config.Bind("General", "Fog Start Distance", 0.03f, "Sets the fog start distance.").Value;
    }
}