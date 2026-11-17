using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landfall.Network;

namespace ComputeryLib.Helpers;

public enum PlayerColour {
    White,
    LightGreen,
    Green,
    LightBlue,
    Blue,
    DarkBlue,
    Purple,
    DarkPink,
    Pink,
    Red,
    Orange,
    Yellow
}

public static class BroadcastHelper {
    private const int MaxNameLength = 32;
    
    /// <summary>
    /// As it turns out, Landfold didn't implement the kill list properly, and after enough kills, for some reason, it literally just slowly sinks off of the screen.
    /// It walks off the screen. It's insane. Fuck landfall.
    ///
    /// Basically, all the time I spent making this is pointless.
    /// This is only good to send as a player joins the game. Otherwise, there is a very likely chance it won't show up for them.
    ///
    /// I'm gonna make a fix for this at some point and put it in landfall please fix as the client-side mod.
    /// </summary>
    /// <param name="message"> you know </param>
    /// <param name="colour"> you know </param>
    /// <param name="target"> you know </param>
    public static void Broadcast(string message, PlayerColour colour, byte target) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        if (message.Length > MaxNameLength) { Plugin.Logger.LogWarning($"Broadcast \"{message}\" is longer than {MaxNameLength} characters. Will be cropped."); }
        
        HashSet<byte> usedPlayerIndexes = [];
        HashSet<byte> usedGroupIndexes = [];
        foreach (TABGPlayerServer player in world.GameRoomReference.PlayersAndSpectators()) {
            usedPlayerIndexes.Add(player.PlayerIndex);
            usedGroupIndexes.Add(player.GroupIndex);
        }

        if (!TryTakeFree(usedPlayerIndexes, out byte killerIndex) || !TryTakeFree(usedPlayerIndexes, out byte victimIndex)) {
            Plugin.Logger.LogWarning($"Out of free player indexes cannot broadcast: \"{message}\".");
            return;
        }

        if (!TryTakeFree(usedGroupIndexes, out byte groupIndex)) {
            Plugin.Logger.LogWarning($"Out of free group indexes cannot broadcast: \"{message}\".");
            return;
        }

        world.SendMessageToClients(EventCode.Login, BuildRemoteLogin(killerIndex, groupIndex, "", colour), target, true);
        world.SendMessageToClients(EventCode.Login, BuildRemoteLogin(victimIndex, groupIndex, message, colour), target, true);
        world.SendMessageToClients(EventCode.PlayerDead, BuildPlayerDead(victimIndex, killerIndex, message), target, true);
        world.SendMessageToClients(EventCode.PlayerLeft, [victimIndex, 1], target, true);
        world.SendMessageToClients(EventCode.PlayerLeft, [killerIndex, 1], target, true);
    }
    
    public static void KillWithFakePlayer(TABGPlayerServer victim, string killerName, PlayerColour colour) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        HashSet<byte> usedPlayerIndexes = [];
        HashSet<byte> usedGroupIndexes = [];
        foreach (TABGPlayerServer player in world.GameRoomReference.PlayersAndSpectators()) {
            usedPlayerIndexes.Add(player.PlayerIndex);
            usedGroupIndexes.Add(player.GroupIndex);
        }
        
        if (!TryTakeFree(usedPlayerIndexes, out byte killerIndex) || !TryTakeFree(usedGroupIndexes, out byte groupIndex)) {
            Plugin.Logger.LogWarning($"Out of free indexes, killing {victim.PlayerName} without a fake killer \"{killerName}\".");
            ServerKillCommand.Run(world, victim);
            return;
        }
        
        world.SendMessageToClients(EventCode.Login, BuildRemoteLogin(killerIndex, groupIndex, killerName, colour), byte.MaxValue, true);
        victim.UpdateLastAttacker(killerIndex);
        ServerKillCommand.Run(world, victim);
        world.SendMessageToClients(EventCode.PlayerLeft, [killerIndex, 1], byte.MaxValue, true);
    }
    
    private static bool TryTakeFree(HashSet<byte> usedIndexes, out byte index) {
        for (int i = byte.MaxValue - 1; i >= 0; i--) {
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
    
    private static byte[] BuildPlayerDead(byte victimIndex, byte killerIndex, string victimName) {
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
}