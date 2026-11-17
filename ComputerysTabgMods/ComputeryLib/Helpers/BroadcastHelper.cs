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
        
        using (FakeGroup group = FakePlayerHelper.ReserveGroup(world)) {
            if (!group.IsValid) {
                Plugin.Logger.LogWarning($"Out of free group indexes, cannot broadcast: \"{message}\".");
                return;
            }
            
            using (FakePlayer killer = FakePlayerHelper.Login(world, group, "", colour, target))
            using (FakePlayer victim = FakePlayerHelper.Login(world, group, message, colour, target)) {
                if (!killer.IsValid || !victim.IsValid) {
                    Plugin.Logger.LogWarning($"Out of free player indexes, cannot broadcast: \"{message}\".");
                    return;
                }
                
                world.SendMessageToClients(EventCode.PlayerDead, BuildPlayerDead(victim.Index, killer.Index, message), target, true);
            }
        }
    }
    
    public static void KillWithFakePlayer(TABGPlayerServer victim, string killerName, PlayerColour colour) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        using (FakeGroup group = FakePlayerHelper.ReserveGroup(world)) {
            if (!group.IsValid) {
                Plugin.Logger.LogWarning($"Out of free group indexes, killing {victim.PlayerName} without a fake killer \"{killerName}\".");
                ServerKillCommand.Run(world, victim);
                return;
            }
            
            using (FakePlayer killer = FakePlayerHelper.Login(world, group, killerName, colour, byte.MaxValue)) {
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
    
    public static void SpoofKills(ServerClient world, TABGPlayerServer killer, int kills, byte target) {
        if (kills <= 0) { return; }
        
        using (FakeGroup group = FakePlayerHelper.ReserveGroup(world)) {
            if (!group.IsValid) {
                Plugin.Logger.LogWarning($"Out of free group indexes, cannot spoof {kills} kills for {killer.PlayerName}.");
                return;
            }
            
            using (FakePlayer phantom = FakePlayerHelper.Login(world, group, "", PlayerColour.White, target)) {
                if (!phantom.IsValid) {
                    Plugin.Logger.LogWarning($"Out of free player indexes, cannot spoof {kills} kills for {killer.PlayerName}.");
                    return;
                }
                
                byte[] dead = BuildPlayerDead(phantom.Index, killer.PlayerIndex, "");
                for (int i = 0; i < kills; i++) { world.SendMessageToClients(EventCode.PlayerDead, dead, target, true); }
            }
        }
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
