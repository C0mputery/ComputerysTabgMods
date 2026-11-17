using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class OrbitalDupeFix {
    public static readonly Dictionary<int, float> PickupWindows = Plugin.Config.Bind("Gameplay", "ThrownPickupWindows", new Dictionary<int, float> { { 205, 3f } }, "ItemID: Time after throw in seconds to disable pickup").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return PickupWindows.Count > 0; }
    
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
        matcher.Insert(loadItemSpawnIndex, loadItemIdentifier, CodeInstruction.Call(typeof(OrbitalDupeFix), nameof(TrackThrownItem)));
        
        return matcher.InstructionEnumeration();
    }
    
    private static readonly Dictionary<int, float> PickupDeadlines = new Dictionary<int, float>();
    private static void TrackThrownItem(int itemSpawnIndex, int itemIdentifier) {
        if (!PickupWindows.TryGetValue(itemIdentifier, out float window)) { return; }
        PickupDeadlines[itemSpawnIndex] = Time.realtimeSinceStartup + window;
    }
    
    private const int WeaponIndexOffset = 1;
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RequestPickupCommand), nameof(RequestPickupCommand.Run))]
    private static bool RequestPickupPrefix(byte[] msgData) {
        int weaponIndex = BitConverter.ToInt32(msgData, WeaponIndexOffset);
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
}
