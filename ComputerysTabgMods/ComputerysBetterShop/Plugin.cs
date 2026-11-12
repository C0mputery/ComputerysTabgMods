using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ComputerysBetterShop;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public new static ManualLogSource Logger;
    
    private void Awake() {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        //EmbeddedAssetBundle.Initialize();
        Logger.LogInfo("Loaded embedded asset bundle.");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), PluginInfo.PLUGIN_GUID);
        Logger.LogInfo("Applied patches.");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode sceneLoad) {
        if (scene.name == "MainMenu") {
            GameObject featured = GameObject.Find("MainMenuCamPivot/MainMenuCam/UICAM/Canvas/Store/MainStore/Featured");
            featured.SetActive(false);
            GameObject thriftStore = GameObject.Find("MainMenuCamPivot/MainMenuCam/UICAM/Canvas/Store/MainStore/ThriftStore");
            thriftStore.SetActive(false);
        }
    }
}