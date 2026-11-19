using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class AntiCheatUtility {
    public static readonly bool AntiCheat = Plugin.Config.Bind("Anticheat", "Enable", true, "Enables or disables the anti-cheat").Value;
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AntiCheat; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDropCommand), nameof(ItemDropCommand.Run))]
    [HarmonyPatch(typeof(ItemThrownCommand), nameof(ItemThrownCommand.Run))]
    private static bool DropThrowPrefix(byte[] msgData, ServerClient world, byte sender) {
        const int itemIdentifierOffset = 1;
        const int itemCountOffset = 5;
        
        TABGPlayerServer? player = world.GameRoomReference.FindPlayer(sender);
        if (player == null) { return false; }
        
        int itemIdentifier = BitConverter.ToInt32(msgData, itemIdentifierOffset);
        int requestedCount = BitConverter.ToInt32(msgData, itemCountOffset);
        int ownedCount = player.HasLoot(itemIdentifier);
        if (requestedCount <= 0 || ownedCount <= 0) {
            Plugin.Logger.LogWarning($"Player {player.PlayerName} ({sender}) tried to drop/throw {requestedCount} of item {itemIdentifier} but has {ownedCount}. Duper.");
            return false;
        }
        if (requestedCount > ownedCount) {
            Plugin.Logger.LogWarning($"Player {player.PlayerName} ({sender}) tried to drop/throw {requestedCount} of item {itemIdentifier} but has {ownedCount}. Duper.");
            Buffer.BlockCopy(BitConverter.GetBytes(ownedCount), 0, msgData, itemCountOffset, sizeof(int));
        }
        
        return true;
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ItemThrownCommand), nameof(ItemThrownCommand.Run))]
    private static IEnumerable<CodeInstruction> InsertTrackThrownItem(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // ItemManipulation.SpawnItemDrop(world, gameRoomReference, newWeaponIndex, num, num2, pos, throwed: true, item.weaponType == Pickup.WeaponType.Grenade);
        matcher.MatchForward(false,
            new CodeMatch(instruction => instruction.IsLdarg()),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.LoadsConstant(1)),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.LoadsField(AccessTools.Field(typeof(Pickup), nameof(Pickup.weaponType)))),
            new CodeMatch(instruction => instruction.LoadsConstant((int)Pickup.WeaponType.Grenade)),
            new CodeMatch(OpCodes.Ceq),
            new CodeMatch(instruction => instruction.Calls(AccessTools.Method(typeof(ItemManipulation), nameof(ItemManipulation.SpawnItemDrop))))
        );
        CodeInstruction loadItemSpawnIndex = matcher.InstructionAt(2).Clone(); // newWeaponIndex
        CodeInstruction loadItemIdentifier = matcher.InstructionAt(3).Clone(); // num
        
        matcher.Advance(12);
        matcher.Insert(loadItemSpawnIndex, loadItemIdentifier, CodeInstruction.Call(typeof(AntiCheatUtility), nameof(TrackThrownItem)));
        
        return matcher.InstructionEnumeration();
    }
    
    public static readonly Dictionary<int, float> PickupWindows = Plugin.Config.Bind("Anticheat", "ThrownPickupWindows", new Dictionary<int, float> { { 205, 3f } }, "ItemID: Time after throw in seconds to disable pickup").Value;
    private static readonly Dictionary<int, float> PickupDeadlines = new Dictionary<int, float>();
    private static void TrackThrownItem(int itemSpawnIndex, int itemIdentifier) {
        if (!PickupWindows.TryGetValue(itemIdentifier, out float window)) { return; }
        PickupDeadlines[itemSpawnIndex] = Time.realtimeSinceStartup + window;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RequestPickupCommand), nameof(RequestPickupCommand.Run))]
    private static bool RequestPickupPrefix(byte[] msgData) {
        const int weaponIndexOffset = 1;
        
        int weaponIndex = BitConverter.ToInt32(msgData, weaponIndexOffset);
        if (!PickupDeadlines.TryGetValue(weaponIndex, out float deadline)) { return true; }
        if (Time.realtimeSinceStartup < deadline) { return true; }
        
        Plugin.Logger.LogInfo($"Blocked pickup of thrown item {weaponIndex} after pickup window. Duper...");
        return false;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.RemoveWeapon))]
    private static void RemoveWeaponPrefix(NetworkGun? weapon) {
        if (weapon == null) { return; }
        PickupDeadlines.Remove(weapon.Index);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerRequestBlessingCommand), nameof(PlayerRequestBlessingCommand.Run))]
    private static bool RequestBlessingPrefix(byte[] msgData, ServerClient world, byte sender) {
        const int firstBlessingOffset = 1;
        
        TABGPlayerServer? player = world.GameRoomReference.FindPlayer(sender);
        if (player == null) { return true; }
        
        int[] blessings = new int[3];
        for (int i = 0; i < blessings.Length; i++) {
            blessings[i] = BitConverter.ToInt32(msgData, firstBlessingOffset + i * sizeof(int));
        }
        
        for (int i = 0; i < blessings.Length; i++) {
            int blessing = blessings[i];
            if (blessing == -1 || Array.IndexOf(blessings, blessing) != i) { continue; }
            
            int equippedCount = 0;
            foreach (int other in blessings) { if (other == blessing) { equippedCount++; } }
            
            int ownedCount = CountOwned(player, blessing);
            if (equippedCount > ownedCount) {
                Plugin.Logger.LogWarning($"{player.PlayerName} tried to equip blessing {blessing} {equippedCount} times but only owns {ownedCount}. Duper...");
                FakePlayerHelper.KillWithFakePlayer(player, "Anti-Cheat", PlayerColour.Red);
                return false;
            }
        }
        
        return true;
    }
    
    private static int CountOwned(TABGPlayerServer player, int itemIndex) {
        int count = 0;
        foreach (TABGPlayerLootItem loot in player.Loot) {
            if (loot.ItemIdentifier == itemIndex) { count += loot.ItemCount; }
        }
        return count;
    }
}
