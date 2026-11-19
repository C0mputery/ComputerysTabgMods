using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.Helpers;

public static class PacketHelper {
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
        using MemoryStream memoryStream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(memoryStream);
        
        byte[] nameBytes = Encoding.Unicode.GetBytes(victimName);
        if (nameBytes.Length > byte.MaxValue) { nameBytes = Encoding.Unicode.GetBytes(victimName[..(byte.MaxValue / 2)]); }
        writer.Write(victimIndex);
        writer.Write(killerIndex);
        writer.Write(byte.MaxValue);
        writer.Write((byte)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(int.MaxValue);
        writer.Write(false);
        
        return memoryStream.ToArray();
    }
    
    public static byte[] BuildReviveFinished(TABGPlayerServer victim) {
        byte[] buffer = new byte[3 + sizeof(float)];
        buffer[0] = (byte)ReviveState.Finished;
        buffer[1] = victim.PlayerIndex;
        buffer[2] = byte.MaxValue; // Reviver
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, 3, sizeof(float));
        return buffer;
    }
    
    public static byte[] BuildHealthChanged(TABGPlayerServer victim) {
        byte[] buffer = new byte[1 + sizeof(float)];
        buffer[0] = victim.PlayerIndex;
        Buffer.BlockCopy(BitConverter.GetBytes(victim.Health), 0, buffer, 1, sizeof(float));
        return buffer;
    }
    
    public static byte[] BuildBlessingRecieved(TABGPlayerServer player) {
        int[] blessings = player.GetBlessings();
        byte[] buffer = new byte[1 + 3 * sizeof(int)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(player.PlayerIndex);
        writer.Write(blessings[0]);
        writer.Write(blessings[1]);
        writer.Write(blessings[2]);
        return buffer;
    }
    
    public static byte[] BuildWeaponChanged(TABGPlayerServer player, short throwable) {
        byte[] buffer = new byte[2 + 5 * 2 + 1 + player.Attachments.Length * 2 + 2];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(player.PlayerIndex);
        writer.Write((byte)player.Equipment[5]);
        for (int i = 0; i < 5; i++) { writer.Write(player.Equipment[i]); }
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
        writer.Write(90f);
        writer.Write(0f);
        
        byte[] chunkBytes = Encoding.Unicode.GetBytes(chunk);
        int chunkLength = chunkBytes.Length;
        if (chunkLength > byte.MaxValue) { throw new ArgumentException("Chunk size exceeds maximum allowed length??? Something broke with the chunking logic."); }
        writer.Write((byte)chunkLength);
        writer.Write(chunkBytes);
        
        return memoryStream.ToArray();
    }
    
    public const int LootEntrySize = 24;
    
    public static byte[] BuildItemDrop(int weaponIndex, int itemId, int quantity, Vector3 position) {
        byte[] buffer = new byte[24];
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
    
    public static byte[] BuildItemThrown(byte playerIndex, int weaponIndex, int itemId, Vector3 position) {
        byte[] buffer = new byte[38];
        using (MemoryStream stream = new MemoryStream(buffer)) {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(playerIndex);
            writer.Write(weaponIndex);
            writer.Write(itemId);
            writer.Write(0);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(1f);
            writer.Write(false);
        }
        return buffer;
    }
    
    public const int LootOnlyChunkEntryHeaderSize = 6;
    
    public static byte[] BuildLootOnlyChunkEntry(byte playerIndex, List<NetworkGun> loot) {
        byte[] buffer = new byte[LootOnlyChunkEntryHeaderSize + loot.Count * LootEntrySize];
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
    
    public static byte[] BuildRingCircle(TheRing ring) {
        byte[] buffer = new byte[2 + 4 * sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)1);
        writer.Write((byte)ring.currentRingID);
        writer.Write(ring.currentWhiteRingPosition.x);
        writer.Write(ring.currentWhiteRingPosition.y);
        writer.Write(ring.currentWhiteRingPosition.z);
        writer.Write(ring.currentWhiteSize);
        return buffer;
    }
    
    public static byte[] BuildRingTime(TheRing ring) {
        byte[] buffer = new byte[1 + sizeof(float)];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write((byte)0);
        writer.Write(Mathf.Max(ring.GetTimeTravelled(), ring.timeToTravel));
        return buffer;
    }
    
    public static byte[] BuildItemDropRequest(byte playerIndex, int itemId, int quantity, Vector3 position) {
        byte[] buffer = new byte[21];
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