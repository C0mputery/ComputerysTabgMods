using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class VoidWeaponFix {
    public static readonly bool DropUnslottedWeapons = Plugin.Config.Bind("Gameplay", "DropVoidedWeapons", true, "Fixes clients being able to void weapons").Value;
    public static readonly float UnslottedWeaponGracePeriod = Plugin.Config.Bind("Gameplay", "DropVoidedWeaponsDelay", 1f, "Seconds until considering a newly picked up weapon voided").Value;
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return DropUnslottedWeapons; }
    
    private static readonly ConditionalWeakTable<TABGPlayerServer, StrongBox<float>> LastWeaponReceived = [];
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TABGPlayerServer), nameof(TABGPlayerServer.AddLoot))]
    private static void AddLootPostfix(TABGPlayerServer __instance, NetworkGun g) {
        if (!IsWeapon(g.UniqueIdentifier)) { return; }
        LastWeaponReceived.GetOrCreateValue(__instance).Value = Time.realtimeSinceStartup;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WeaponChangedCommand), nameof(WeaponChangedCommand.Run))]
    private static void WeaponChangedPostfix(byte[] msgData, ServerClient world) {
        const int weaponSlotCount = 5;
        
        TABGPlayerServer? player = world.GameRoomReference.FindPlayer(msgData[0]); // Player index
        if (player == null || player.IsDead) { return; }
        if (LastWeaponReceived.TryGetValue(player, out StrongBox<float> received) && Time.realtimeSinceStartup - received.Value < UnslottedWeaponGracePeriod) { return; }
        
        Dictionary<int, int> slotted = [];
        for (int i = 0; i < weaponSlotCount; i++) {
            int itemIdentifier = player.Equipment[i];
            slotted.TryGetValue(itemIdentifier, out int count);
            slotted[itemIdentifier] = count + 1;
        }
        
        List<(int itemIdentifier, int count)> unslotted = [];
        foreach (TABGPlayerLootItem item in player.Loot) {
            if (!IsWeapon(item.ItemIdentifier)) { continue; }
            slotted.TryGetValue(item.ItemIdentifier, out int slottedCount);
            int unslottedCount = item.ItemCount - slottedCount;
            if (unslottedCount > 0) { unslotted.Add((item.ItemIdentifier, unslottedCount)); }
        }
        
        foreach ((int itemIdentifier, int count) in unslotted) {
            Plugin.Logger.LogWarning($"Player {player.PlayerName} ({player.PlayerIndex}) voided a weapon ({itemIdentifier}). Dropping it.");
            ItemDropCommand.Run(PacketHelper.BuildItemDropRequest(player.PlayerIndex, itemIdentifier, count, player.PlayerPosition), world, player.PlayerIndex);
        }
    }
    
    private static bool IsWeapon(int itemIdentifier) {
        if (!LootDatabase.Instance.HasDataEntry(itemIdentifier)) { return false; }
        return LootDatabase.Instance.GetDataEntry(itemIdentifier).pickup.weaponType == Pickup.WeaponType.Weapon;
    }
}