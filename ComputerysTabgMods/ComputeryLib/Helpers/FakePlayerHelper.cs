using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Landfall.Network;

namespace ComputeryLib.Helpers;

public static class FakePlayerHelper {
    private static readonly HashSet<byte> ReservedFakeGroupIndexes = [];
    public static FakeGroup ReserveGroup(ServerClient world) {
        GameRoom room = world.GameRoomReference;
        HashSet<byte> usedGroupIndexes = [..ReservedFakeGroupIndexes];
        foreach (TeamStanding team in room.CurrentGameStats.GetAllTeams()) { usedGroupIndexes.Add(team.TeamIndex); }
        foreach (TABGPlayerServer player in room.PlayersAndSpectators()) { usedGroupIndexes.Add(player.GroupIndex); }
        
        byte minGroupIndex = (byte)(room.CurrentGameSettings.MaxPlayers + 1);
        if (!TryTakeFree(usedGroupIndexes, minGroupIndex, out byte groupIndex)) { return default; }
        
        ReservedFakeGroupIndexes.Add(groupIndex);
        return new FakeGroup(groupIndex);
    }
    
    internal static void ReleaseGroup(byte groupIndex) {
        ReservedFakeGroupIndexes.Remove(groupIndex);
    }
    
    private static readonly HashSet<byte> LoggedInFakeIndexes = [];
    public static FakePlayer Login(ServerClient world, FakeGroup group, string name, PlayerColour colour, byte target) {
        if (!group.IsValid) { return default; }
        
        byte minPlayerIndex = (byte)(world.GameRoomReference.CurrentGameSettings.MaxPlayers + 1);
        if (!TryTakeFree([..LoggedInFakeIndexes], minPlayerIndex, out byte playerIndex)) { return default; }
        
        world.SendMessageToClients(EventCode.Login, BuildRemoteLogin(playerIndex, group.Index, name, colour), target, true);
        LoggedInFakeIndexes.Add(playerIndex);
        return new FakePlayer(world, playerIndex, target);
    }
    
    internal static void Logout(ServerClient world, byte playerIndex, byte target) {
        world.SendMessageToClients(EventCode.PlayerLeft, [playerIndex, 1], target, true);
        LoggedInFakeIndexes.Remove(playerIndex);
    }
    
    private static bool TryTakeFree(HashSet<byte> usedIndexes, byte minIndex, out byte index) {
        for (int i = byte.MaxValue - 1; i >= minIndex; i--) {
            if (!usedIndexes.Add((byte)i)) { continue; }
            index = (byte)i; return true;
        }
        index = 0;
        return false;
    }
    
    private static byte[] BuildRemoteLogin(byte playerIndex, byte groupIndex, string playerName, PlayerColour colour) {
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
}

public readonly struct FakeGroup : IDisposable {
    public byte Index { get; }
    public bool IsValid { get; }
    
    internal FakeGroup(byte index) {
        Index = index;
        IsValid = true;
    }
    
    public void Dispose() {
        if (!IsValid) { return; }
        FakePlayerHelper.ReleaseGroup(Index);
    }
}

public readonly struct FakePlayer : IDisposable {
    private readonly ServerClient? _world;
    private readonly byte _target;
    
    public byte Index { get; }
    public bool IsValid => _world != null;
    
    internal FakePlayer(ServerClient world, byte index, byte target) {
        _world = world;
        _target = target;
        Index = index;
    }
    
    public void Dispose() {
        if (_world == null) { return; }
        FakePlayerHelper.Logout(_world, Index, _target);
    }
}
