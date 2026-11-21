using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class GearBlacklistUtility {
    public static readonly HashSet<int> BlacklistedGearIDs = Plugin.Config.Bind<int[]>("Gameplay", "BlacklistedCosmeticIDs", [], "Gear ID Blacklist. The Annoying Cosmetics: 441 (banana), 450 (pickle), 498 (dynamite).").Value.ToHashSet();
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return BlacklistedGearIDs.Count > 0; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TABGPlayerServer), MethodType.Constructor, typeof(string), typeof(byte), typeof(byte), typeof(ulong), typeof(string), typeof(int), typeof(int[]), typeof(int), typeof(bool), typeof(bool))]
    private static void TABGPlayerServerConstructorPrefix(int[] gearData) { StripBlacklisted(gearData); }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GearChangeCommand), nameof(GearChangeCommand.Run))]
    private static void GearChangePrefix(byte[] msgData) {
        const int gearCountOffset = 1;
        const int firstGearOffset = 5;
        
        int gearCount = BitConverter.ToInt32(msgData, gearCountOffset);
        StripBlacklisted(MemoryMarshal.Cast<byte, int>(msgData.AsSpan(firstGearOffset, gearCount * sizeof(int))));
    }
    
    private static void StripBlacklisted(Span<int> gear) {
#pragma warning disable Harmony003
        for (int i = 0; i + 1 < gear.Length; i += 2) {
            if (!BlacklistedGearIDs.Contains(gear[i])) { continue; }
            gear[i] = -1;
            gear[i + 1] = -1;
        }
#pragma warning restore Harmony003
    }
}