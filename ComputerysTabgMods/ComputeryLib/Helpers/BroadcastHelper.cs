using System.Linq;
using Landfall.Network;

namespace ComputeryLib.Helpers;

public enum PlayerColour {
    White = 0,
    LightGreen = 1,
    Green = 2,
    LightBlue = 3,
    Blue = 4,
    DarkBlue = 5,
    Purple = 6,
    DarkPink = 7,
    Pink = 8,
    Red = 9,
    Orange = 10,
    Yellow = 11
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
        ServerClient world = WorldHelper.GetWorld();
        
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
                
                world.SendMessageToClients(EventCode.PlayerDead, PacketHelper.BuildPlayerDead(victim.Index, killer.Index, message), target, true);
            }
        }
    }
    
    public static void KillWithFakePlayer(TABGPlayerServer victim, string killerName, PlayerColour colour) {
        ServerClient world = WorldHelper.GetWorld();
        
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
}
