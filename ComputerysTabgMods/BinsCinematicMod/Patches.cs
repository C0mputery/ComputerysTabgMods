using HarmonyLib;
using UnityEngine;

namespace BinsCinematicMod;

[HarmonyPatch(typeof(WilhelmChunker))]
public static class WilhelmChunkerPatches {
    [HarmonyPrefix]
    [HarmonyPatch(nameof(WilhelmChunker.ChunkUpdate))]
    private static bool ChunkUpdatePrefix(ref WilhelmChunker __instance) {
        for (int x = 0; x < __instance.numberOfChunks; x++) {
            for (int y = 0; y < __instance.numberOfChunks; y++) {
                WilhelmChunkPiece chunk = __instance.chunks[x, y];
                if (__instance.loadedChunks.Contains(chunk)) { continue; }
                chunk.EnterChunk();
                __instance.loadedChunks.Add(chunk);
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(CharacterGearHandler))]
public static class CharacterGearHandlerPatches {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharacterGearHandler.AttachGear), typeof(Gear), typeof(Gear.GearType), typeof(bool))]
    public static void AttachGearPostfix(ref CharacterGearHandler __instance) {
        if (Player.localPlayer != __instance.m_player) { return; }
        foreach (Gear gear in __instance.m_gearObjects.Values) {
            gear.gameObject.SetLayerRecursively(13, 22);
            foreach (GameObject additionalObject in gear.m_additionalObjects) { additionalObject.SetLayerRecursively(13, 22); }
        }
    }
}

public static class OptionsPatches {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OptionsButton), nameof(OptionsButton.Init))]
    public static void InitPrefix(ref OptionsButton __instance) {
        string name = __instance.transform.name;
        if (name == "Item_ShadowQuality") {
            __instance.valueNames = [.. __instance.valueNames, "ULTRA"];
        }
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(OptionsHolder), nameof(OptionsHolder.ApplyGameClientOptions))]
    public static void ApplyGameClientOptionsPrefix() {
        if (OptionsHolder.shadowQuality == 3) {
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowmaskMode = ShadowmaskMode.DistanceShadowmask;
            QualitySettings.shadowCascades = 8;
        }
    }
}

public static class TimeOfDayPatch {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DayAndNightCycle), nameof(DayAndNightCycle.Update))]
    public static bool UpdatePrefix(ref DayAndNightCycle __instance) {
        bool useOverride = Plugin.Config.Bind("General", "Time of Day Override", false, "If true, the time of day will be overridden to the specified value.").Value;
        if (!useOverride) { return true; }
        float overrideTime = Plugin.Config.Bind("General", "Time of Day Value", 0.5f, "The time of day value to override is a value 0 - 1").Value;
        overrideTime = Mathf.Clamp(overrideTime, 0f, 1f);
        __instance.timeOfDay = overrideTime;
        __instance.CheckTwoClosestFrames();
        return false;
    }
}