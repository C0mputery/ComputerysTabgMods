using System;
using System.Collections.Generic;
using Landfall.Network;

namespace ComputeryLib.Helpers;

public static class FakePlayerHelper {
    private static readonly HashSet<byte> ReservedFakeGroupIndexes = [];
    
    public static FakeGroup ReserveGroup() {
        GameRoom room = WorldHelper.GetWorld().GameRoomReference;
        HashSet<byte> usedGroupIndexes = [.. ReservedFakeGroupIndexes];
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
    
    public static FakePlayer Login(FakeGroup group, string name, PlayerColour colour, byte target) {
        if (!group.IsValid) { return default; }
        
        ServerClient world = WorldHelper.GetWorld();
        byte minPlayerIndex = (byte)(world.GameRoomReference.CurrentGameSettings.MaxPlayers + 1);
        if (!TryTakeFree([.. LoggedInFakeIndexes], minPlayerIndex, out byte playerIndex)) { return default; }
        
        world.SendMessageToClients(EventCode.Login, PacketHelper.BuildRemoteLogin(playerIndex, group.Index, name, colour), target, true);
        LoggedInFakeIndexes.Add(playerIndex);
        return new FakePlayer(playerIndex, target);
    }
    
    internal static void Logout(byte playerIndex, byte target) {
        WorldHelper.GetWorld().SendMessageToClients(EventCode.PlayerLeft, [playerIndex, 1], target, true);
        LoggedInFakeIndexes.Remove(playerIndex);
    }
    
    private static bool TryTakeFree(HashSet<byte> usedIndexes, byte minIndex, out byte index) {
        for (int i = byte.MaxValue - 1; i >= minIndex; i--) {
            if (!usedIndexes.Add((byte)i)) { continue; }
            index = (byte)i;
            return true;
        }
        index = 0;
        return false;
    }
    
    public static void KillWithFakePlayer(TABGPlayerServer victim, string killerName, PlayerColour colour) {
        ServerClient world = WorldHelper.GetWorld();
        
        using (FakeGroup group = ReserveGroup()) {
            if (!group.IsValid) {
                Plugin.Logger.LogWarning($"Out of free group indexes, killing {victim.PlayerName} without a fake killer \"{killerName}\".");
                ServerKillCommand.Run(world, victim);
                return;
            }
            
            using (FakePlayer killer = Login(group, killerName, colour, byte.MaxValue)) {
                if (!killer.IsValid) {
                    Plugin.Logger.LogWarning($"Out of free player indexes, killing {victim.PlayerName} without a fake killer \"{killerName}\".");
                    ServerKillCommand.Run(world, victim);
                    return;
                }
                
                victim.UpdateLastAttacker(killer.Index);
                ServerKillCommand.Run(world, victim);
            }
        }
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
    private readonly byte _target;
    
    public byte Index { get; }
    public bool IsValid { get; }
    
    internal FakePlayer(byte index, byte target) {
        _target = target;
        Index = index;
        IsValid = true;
    }
    
    public void Dispose() {
        if (!IsValid) { return; }
        FakePlayerHelper.Logout(Index, _target);
    }
}