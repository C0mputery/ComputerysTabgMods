using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class PacketHelper {
    public const int LootEntrySize = sizeof(int) + sizeof(int) + sizeof(int) + 3 * sizeof(float);
    public const int LootOnlyChunkEntryHeaderSize = sizeof(byte) + sizeof(byte) + sizeof(ushort) + sizeof(ushort);
    
    public static byte[] BuildRemoteLogin(byte playerIndex, byte groupIndex, string playerName, PlayerColour colour) {
        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(memoryStream);
        
        byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
        writer.Write(playerIndex);
        writer.Write(groupIndex);
        writer.Write(nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(0);
        writer.Write(false);
        writer.Write((int)colour);
        
        return memoryStream.ToArray();
    }
    
    public static byte[] BuildPlayerDead(byte victimIndex, byte killerIndex, string victimName) {
        const int maxNameLength = byte.MaxValue / sizeof(char);
        
        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(memoryStream);
        
        byte[] nameBytes = Encoding.Unicode.GetBytes(victimName);
        if (nameBytes.Length > byte.MaxValue) { nameBytes = Encoding.Unicode.GetBytes(victimName[..maxNameLength]); }
        writer.Write(victimIndex);
        writer.Write(killerIndex);
        writer.Write(byte.MaxValue); // Spectator target
        writer.Write((byte)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(int.MaxValue); // Weapon
        writer.Write(false); // Ring death
        
        return memoryStream.ToArray();
    }
    
    public static byte[] BuildReviveFinished(TABGPlayerServer victim) {
        const int stateOffset = 0;
        const int victimOffset = 1;
        const int reviverOffset = 2;
        const int healthOffset = 3;
        
        byte[] buffer = new byte[healthOffset + sizeof(float)];
        buffer[stateOffset] = (byte)ReviveState.Finished;
        buffer[victimOffset] = victim.PlayerIndex;
        buffer[reviverOffset] = byte.MaxValue; // Reviver
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, healthOffset, sizeof(float));
        return buffer;
    }
    
    public static byte[] BuildHealthChanged(TABGPlayerServer victim) {
        const int victimOffset = 0;
        const int healthOffset = 1;
        
        byte[] buffer = new byte[healthOffset + sizeof(float)];
        buffer[victimOffset] = victim.PlayerIndex;
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, healthOffset, sizeof(float));
        return buffer;
    }
    
    public static byte[] BuildBlessingRecieved(TABGPlayerServer player) {
        const int blessingSlotCount = 3;
        
        int[] blessings = player.GetBlessings();
        byte[] buffer = new byte[sizeof(byte) + blessingSlotCount * sizeof(int)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(player.PlayerIndex);
        for (int i = 0; i < blessingSlotCount; i++) { writer.Write(blessings[i]); }
        return buffer;
    }
    
    public static byte[] BuildWeaponChanged(TABGPlayerServer player, short throwable) {
        const int weaponSlotCount = 5;
        const int slotFlagIndex = 5;
        const int headerSize = sizeof(byte) + sizeof(byte) + weaponSlotCount * sizeof(short) + sizeof(byte);
        
        byte[] buffer = new byte[headerSize + player.Attachments.Length * sizeof(short) + sizeof(short)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(player.PlayerIndex);
        writer.Write((byte)player.Equipment[slotFlagIndex]);
        for (int i = 0; i < weaponSlotCount; i++) { writer.Write(player.Equipment[i]); }
        writer.Write((byte)player.Attachments.Length);
        foreach (short attachment in player.Attachments) { writer.Write(attachment); }
        writer.Write(throwable);
        return buffer;
    }
    
    public static byte[] BuildThrowChatMessage(byte playerIndex, Vector3 position, string chunk) {
        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(memoryStream);
        
        writer.Write(playerIndex);
        writer.Write(position.x);
        writer.Write(position.y);
        writer.Write(position.z);
        writer.Write(90f); // Rotation X
        writer.Write(0f); // Rotation Y
        
        byte[] chunkBytes = Encoding.Unicode.GetBytes(chunk);
        int chunkLength = chunkBytes.Length;
        if (chunkLength > byte.MaxValue) { throw new ArgumentException("Chunk size exceeds maximum allowed length??? Something broke with the chunking logic."); }
        writer.Write((byte)chunkLength);
        writer.Write(chunkBytes);
        
        return memoryStream.ToArray();
    }
    
    public static byte[] BuildItemDrop(int weaponIndex, int itemId, int quantity, Vector3 position) {
        byte[] buffer = new byte[LootEntrySize];
        using (MemoryStream stream = new MemoryStream(buffer)) {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(weaponIndex);
            writer.Write(itemId);
            writer.Write(quantity);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
        }
        return buffer;
    }
    
    public static byte[] BuildLootOnlyChunkEntry(byte playerIndex, List<NetworkGun> loot) {
        byte[] buffer = new byte[LootOnlyChunkEntryHeaderSize + loot.Count * LootEntrySize];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(playerIndex);
        writer.Write((byte)0); // Player count
        writer.Write((ushort)loot.Count);
        writer.Write((ushort)0); // Car count
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
    
    public static byte[] BuildRingCircle(TheRing ring) {
        byte[] buffer = new byte[sizeof(byte) + sizeof(byte) + 3 * sizeof(float) + sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)1); // Circle update
        writer.Write((byte)ring.currentRingID);
        writer.Write(ring.currentWhiteRingPosition.x);
        writer.Write(ring.currentWhiteRingPosition.y);
        writer.Write(ring.currentWhiteRingPosition.z);
        writer.Write(ring.currentWhiteSize);
        return buffer;
    }
    
    public static byte[] BuildRingTime(TheRing ring) {
        byte[] buffer = new byte[sizeof(byte) + sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)0); // Time update
        writer.Write(Mathf.Max(ring.GetTimeTravelled(), ring.timeToTravel));
        return buffer;
    }
    
    public static byte[] BuildItemDropRequest(byte playerIndex, int itemId, int quantity, Vector3 position) {
        const int packetSize = sizeof(byte) + 2 * sizeof(int) + 3 * sizeof(float);
        
        byte[] buffer = new byte[packetSize];
        using (MemoryStream stream = new MemoryStream(buffer)) {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(playerIndex);
            writer.Write(itemId);
            writer.Write(quantity);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
        }
        return buffer;
    }
}