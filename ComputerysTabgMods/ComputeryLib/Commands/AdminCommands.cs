using System.Linq;
using ComputeryLib.Utilities;
using Landfall.Network;

namespace ComputeryLib.Commands;

public class AdminCommands {
    [ChatCommand("setlevel", "Sets the permission level of a player. Usage: /setlevel <player_name|player_id|epic_username> <level>", 10)]
    public static void SetLevelCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        if (arguments.Length < 2) {
            PlayerInteractionUtility.PrivateMessageOrServerLog("Usage: /setlevel <player_name|player_id|epic_username> <level>", sender);
            return;
        }
        
        if (!uint.TryParse(arguments[^1], out uint level)) {
            PlayerInteractionUtility.PrivateMessageOrServerLog("Invalid level specified. Must be a non-negative integer.", sender);
            return;
        }
        
        string searchValue = string.Join(" ", arguments.Take(arguments.Length - 1));
        if (!SearchUtility.TryGetPlayerByNameOrID(searchValue, out TABGPlayerServer? foundPlayer)) {
            PlayerInteractionUtility.PrivateMessageOrServerLog($"Player not found: {searchValue}", sender);
            return;
        }
        
        VisitorLog.VisitorLog.SetPermissionLevel(foundPlayer!.EpicUserName, level);
        PlayerInteractionUtility.PrivateMessageOrServerLog($"Set permission level {level} for player {foundPlayer.PlayerName}", sender);
    }
    
    [ChatCommand("kick", "Kicks a player from the server. Usage: /kick <player_name|player_id|epic_username>, <reason:optional>", 10)]
    public static void KickCommand(string[] arguments, TABGPlayerServer? sender) { }
    
    [ChatCommand("ban", "Bans a players Epic Id from the server. Usage: /ban <player_name|player_id|epic_username>, <reason:optional>, <duration_in_minutes:optional>", 10)]
    public static void BanCommand(string[] arguments, TABGPlayerServer? sender) { }
    
    [ChatCommand("ipban", "Bans a players IP from the server. Usage: /ipban <player_name|player_id|epic_username|ip>, <reason:optional>, <duration_in_minutes:optional>. Only works on Enet servers.", 10)]
    public static void IPBanCommand(string[] arguments, TABGPlayerServer? sender) { }
}