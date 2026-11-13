using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class ChunkEntryLootBreakup {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ChunkEntryCommand), nameof(ChunkEntryCommand.Run))]
    private static IEnumerable<CodeInstruction> SplitLoot(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // if (world.GameRoomReference.CurrentGameState == GameState.WaitingForPlayers) { list2.Clear(); }
        matcher.MatchForward(true,
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ServerClient), nameof(ServerClient.GameRoomReference))),
            new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(GameRoom), nameof(GameRoom.CurrentGameState))),
            new CodeMatch(instruction => instruction.Branches(out _)),
            new CodeMatch(OpCodes.Ldloc_2),
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(List<TABGCarServer>), nameof(List<TABGCarServer>.Clear)))
        );
        
        matcher.Advance(1);
        List<Label> labelsToPreserve = [.. matcher.Instruction.labels];
        matcher.Instruction.labels.Clear();
        
        // TrimLoot(list, list2, list3, world, _player);
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Ldloc_2),
            new CodeInstruction(OpCodes.Ldloc_3),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChunkEntryLootBreakup), nameof(TrimLoot)))
        );
        
        matcher.Instruction.labels.AddRange(labelsToPreserve);
        
        // world.SendMessageToClients(EventCode.ChunkEntry, buffer, _player.UpdateMessageQueue.Receivers, reliable: true);
        matcher.MatchForward(true,
            new CodeMatch(instruction => instruction.LoadsConstant(EventCode.ChunkEntry)),
            new CodeMatch(instruction => instruction.IsLdloc())
        );
        matcher.Advance(1);
        
        // buffer into TrimPlayerPadding(buffer, list)
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChunkEntryLootBreakup), nameof(TrimPlayerPadding)))
        );
        
        return matcher.InstructionEnumeration();
    }
    
    private const int MaxPayloadSize = UnityTransportConnectionFixes.ClientFragmentationPayloadCapacity - 1024;
    private const int LootEntrySize = 24;
    private const int LootOnlyHeaderSize = 6;
    private const int LootPerPacket = (MaxPayloadSize - LootOnlyHeaderSize) / LootEntrySize;
    private const int MaxQuaternionSize = 7;
    
    private static void TrimLoot(List<TABGPlayerServer> players, List<TABGCarServer> cars, List<NetworkGun> loot, ServerClient world, TABGPlayerServer player) {
        int lootThatFits = Mathf.Max(0, (MaxPayloadSize - GetSizeWithoutLoot(players, cars)) / LootEntrySize);
        if (loot.Count <= lootThatFits) { return; }
        
        for (int i = lootThatFits; i < loot.Count; i += LootPerPacket) {
            List<NetworkGun> batch = loot.GetRange(i, Mathf.Min(LootPerPacket, loot.Count - i));
            world.SendMessageToClients(EventCode.ChunkEntry, BuildLootOnlyChunkEntry(player.PlayerIndex, batch), player.UpdateMessageQueue.Receivers, true);
        }
        loot.RemoveRange(lootThatFits, loot.Count - lootThatFits);
    }
    
    private static int GetSizeWithoutLoot(List<TABGPlayerServer> players, List<TABGCarServer> cars) {
        int carSize = 20 * cars.Count;
        foreach (TABGCarServer car in cars) { carSize += car.NumberOfSeats + MaxQuaternionSize; }
        
        return GetPlayerSize(players) + 4 + 5 + carSize + 1;
    }
    
    private static int GetVanillaPlayerSize(List<TABGPlayerServer> players) {
        int gearSize = 0;
        int attachmentSize = 0;
        foreach (TABGPlayerServer player in players) {
            gearSize += 4 * player.GearData.Length;
            attachmentSize += 2 * player.Attachments.Length;
        }
        return players.Count * (9 + 4 * gearSize + 4 + 2 * TABGPlayerServer.EquipmentDataLength + 1 + attachmentSize);
    }
    
    private static int GetPlayerSize(List<TABGPlayerServer> players) {
        int size = 0;
        foreach (TABGPlayerServer player in players) { size += 1 + 1 + 4 + 4 + 1 + 1 + 4 + 4 * player.GearData.Length + 1 + 2 * TABGPlayerServer.EquipmentDataLength + 1 + 2 * player.Attachments.Length; }
        return size;
    }
    
    private static byte[] TrimPlayerPadding(byte[] buffer, List<TABGPlayerServer> players) {
        int padding = GetVanillaPlayerSize(players) - GetPlayerSize(players);
        if (padding <= 0) { return buffer; }
        
        byte[] trimmed = new byte[buffer.Length - padding];
        System.Array.Copy(buffer, trimmed, trimmed.Length);
        return trimmed;
    }
    
    private static byte[] BuildLootOnlyChunkEntry(byte playerIndex, List<NetworkGun> loot) {
        byte[] buffer = new byte[LootOnlyHeaderSize + loot.Count * LootEntrySize];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(playerIndex);
        writer.Write((byte)0); // player count
        writer.Write((ushort)loot.Count);
        writer.Write((ushort)0); // car count
        foreach (NetworkGun gun in loot) {
            writer.Write(gun.Index);
            writer.Write(gun.UniqueIdentifier);
            writer.Write(gun.Quantity);
            writer.Write(gun.Position.x);
            writer.Write(gun.Position.y);
            writer.Write(gun.Position.z);
        }
        return buffer;
    }
}