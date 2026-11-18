using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class RemoteItemPickupFix {
    public static readonly bool SendRemoteItemPickupsAsRemoval = Plugin.Config.Bind("Network", "SendRemoteItemPickupsAsRemoval", true, "Two birds with one stone should fix ghost items on the ground in certain cases and should fix the reload on weapon pullout bug.").Value;
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return SendRemoteItemPickupsAsRemoval; }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RequestPickupCommand), nameof(RequestPickupCommand.Run))]
    private static IEnumerable<CodeInstruction> RedirectPickupSend(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // world.SendMessageToClients(EventCode.WeaponPickUpAccepted, buffer, array, reliable: true);
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ServerClient), nameof(ServerClient.SendMessageToClients), [typeof(EventCode), typeof(byte[]), typeof(byte[]), typeof(bool), typeof(bool)]))
        );
        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(RemoteItemPickupFix), nameof(SendPickup)));
        
        return matcher.InstructionEnumeration();
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GivePickUpCommand), nameof(GivePickUpCommand.Run), typeof(byte[]), typeof(ServerClient), typeof(byte), typeof(int[]), typeof(byte[]))]
    private static IEnumerable<CodeInstruction> RedirectLootPackSend(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // world.SendMessageToClients(EventCode.LootPackGiven, buffer, array4, reliable: true);
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ServerClient), nameof(ServerClient.SendMessageToClients), [typeof(EventCode), typeof(byte[]), typeof(byte[]), typeof(bool), typeof(bool)]))
        );
        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(RemoteItemPickupFix), nameof(SendLootPack)));
        
        return matcher.InstructionEnumeration();
    }
    
    private static void SendPickup(ServerClient world, EventCode opCode, byte[] buffer, byte[] recipients, bool reliable, bool alsoSendToTeamates) {
        byte pickerIndex = buffer[0];
        int lootIndex = BitConverter.ToInt32(buffer, 1);
        int itemIdentifier = BitConverter.ToInt32(buffer, 5);
        if (KeepVanillaPickup(itemIdentifier)) {
            world.SendMessageToClients(opCode, buffer, recipients, reliable, alsoSendToTeamates);
            return;
        }
        
        List<byte> observers = new List<byte>(recipients.Length);
        foreach (byte recipient in recipients) {
            if (recipient != pickerIndex) { observers.Add(recipient); }
        }
        
        world.SendMessageToClients(opCode, buffer, pickerIndex, reliable, alsoSendToTeamates);
        if (observers.Count > 0) { world.SendMessageToClients(EventCode.LootRemovedFromMap, BitConverter.GetBytes(lootIndex), observers.ToArray(), reliable); }
    }
    
    private const int LootPackHeaderSize = 2;
    private const int LootPackItemSize = 9;
    private static void SendLootPack(ServerClient world, EventCode opCode, byte[] buffer, byte[] recipients, bool reliable, bool alsoSendToTeamates) {
        byte receiverIndex = buffer[0];
        int count = buffer[1];
        
        List<int> weaponOffsets = new List<int>(count);
        for (int i = 0; i < count; i++) {
            int offset = LootPackHeaderSize + i * LootPackItemSize;
            if (KeepVanillaPickup(BitConverter.ToInt32(buffer, offset + 4))) { weaponOffsets.Add(offset); }
        }
        if (weaponOffsets.Count == count) {
            world.SendMessageToClients(opCode, buffer, recipients, reliable, alsoSendToTeamates);
            return;
        }
        
        List<byte> observers = new List<byte>(recipients.Length);
        foreach (byte recipient in recipients) {
            if (recipient != receiverIndex) { observers.Add(recipient); }
        }
        
        world.SendMessageToClients(opCode, buffer, receiverIndex, reliable, alsoSendToTeamates);
        if (observers.Count <= 0 || weaponOffsets.Count <= 0) { return; }
        
        byte[] observerBuffer = new byte[LootPackHeaderSize + weaponOffsets.Count * LootPackItemSize];
        observerBuffer[0] = receiverIndex;
        observerBuffer[1] = (byte)weaponOffsets.Count;
        for (int i = 0; i < weaponOffsets.Count; i++) {
            Buffer.BlockCopy(buffer, weaponOffsets[i], observerBuffer, LootPackHeaderSize + i * LootPackItemSize, LootPackItemSize);
        }
        world.SendMessageToClients(opCode, observerBuffer, observers.ToArray(), reliable, alsoSendToTeamates);
    }
    
    private static bool KeepVanillaPickup(int itemIdentifier) {
        if (!LootDatabase.Instance.HasDataEntry(itemIdentifier)) { return true; }
        Pickup pickup = LootDatabase.Instance.GetDataEntry(itemIdentifier).pickup;
        return pickup.weaponType == Pickup.WeaponType.Weapon;
    }
}
