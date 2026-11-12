using HarmonyLib;
using Landfall.TABG.UI;
using UnityEngine;

namespace ComputerysBetterShop;

public static class Patches {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayFabManager), nameof(PlayFabManager.GetShops))]
    public static bool GetShopsPrefix() {
        GameObject mainStore = GameObject.Find("MainMenuCamPivot/MainMenuCam/UICAM/Canvas/Store/MainStore");
        GameObject betterStore = new GameObject("BetterStore");
        betterStore.transform.SetParent(mainStore.transform);
        
        //PlayFabManager.m_ItemsInShop;
        
        return false;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayfabStoreUIHandler), nameof(PlayfabStoreUIHandler.OnTimeStampGotten))]
    public static bool OnTimeStampGottenPrefix() { return false; }
}