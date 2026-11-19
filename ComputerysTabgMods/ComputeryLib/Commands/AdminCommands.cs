using System;
using System.Diagnostics.CodeAnalysis;
using ComputeryLib.Helpers;
using ComputeryLib.Visitors;
using Epic.OnlineServices.AntiCheatCommon;
using Landfall.Network;

namespace ComputeryLib.Commands;

public static class AdminCommands {
    [ChatCommand("setlevel", "Sets the permission level of a player. Usage: /setlevel <player_name|player_id> <level>", 10)]
    public static void SetLevelCommand(string[] arguments, TABGPlayerServer? sender) {
        if (arguments.Length < 2) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Usage: /setlevel <player_name|player_id> <level>", sender);
            return;
        }
        
        if (!uint.TryParse(arguments[1], out uint level)) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Invalid level specified. Must be a non-negative integer.", sender);
            return;
        }
        
        if (!SearchHelper.TryGetPlayerByNameOrID(arguments[0], sender, out TABGPlayerServer? foundPlayer)) { return; }
        
        if (sender != null) {
            uint senderLevel = ChatCommandManager.GetUserPermissionLevel(sender);
            if (level >= senderLevel) {
                PlayerInteractionHelper.PrivateMessageOrServerLog($"You can only set levels below your own ({senderLevel}).", sender);
                return;
            }
            if (foundPlayer.PlayerIndex != sender.PlayerIndex && VisitorLog.GetPermissionLevel(foundPlayer.EpicUserName) >= senderLevel) {
                PlayerInteractionHelper.PrivateMessageOrServerLog($"You cannot change the level of {foundPlayer.PlayerName}, their permission level is equal or higher than yours.", sender);
                return;
            }
        }
        
        VisitorLog.SetPermissionLevel(foundPlayer.EpicUserName, level);
        PlayerInteractionHelper.PrivateMessageOrServerLog($"Set permission level {level} for player {foundPlayer.PlayerName}", sender);
    }
    
    [ChatCommand("kick", "Kicks a player from the server. Usage: /kick <player_name|player_id> [reason]", 10)]
    public static void KickCommand(string[] arguments, TABGPlayerServer? sender) {
        if (arguments.Length < 1) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Usage: /kick <player_name|player_id> [reason]", sender);
            return;
        }
        
        if (!TryResolveModerationTarget(arguments[0], sender, out TABGPlayerServer? target)) { return; }
        string? reason = arguments.Length > 1 && arguments[1].Length > 0 ? arguments[1] : null;
        
        ServerClient world = WorldHelper.GetWorld();
        
        VisitorLog.RecordKick(target.EpicUserName, reason, sender?.PlayerName ?? "Server");
        PlayerKickCommand.Run(target, world, KickReason.BackendViolation);
        Plugin.Logger.LogInfo($"{sender?.PlayerName ?? "Server"} kicked {target.PlayerName} ({target.EpicUserName}). Reason: {reason ?? "none"}");
        PlayerInteractionHelper.PrivateMessageOrServerLog($"Kicked {target.PlayerName}.", sender);
    }
    
    [ChatCommand("ban", "Bans a players Epic Id from the server. Usage: /ban <player_name|player_id> [reason] [duration_in_minutes]", 10)]
    public static void BanCommand(string[] arguments, TABGPlayerServer? sender) {
        const string usage = "Usage: /ban <player_name|player_id> [reason] [duration_in_minutes]";
        if (arguments.Length < 1 || arguments.Length > 3) {
            PlayerInteractionHelper.PrivateMessageOrServerLog(usage, sender);
            return;
        }
        
        string? reason = null;
        DateTime? expires = null;
        for (int i = 1; i < arguments.Length; i++) {
            string argument = arguments[i];
            if (argument.Length == 0) { continue; }
            
            if (expires == null && uint.TryParse(argument, out uint minutes)) {
                if (minutes == 0) {
                    PlayerInteractionHelper.PrivateMessageOrServerLog("Invalid duration specified. Must be a positive number of minutes.", sender);
                    return;
                }
                expires = DateTime.UtcNow.AddMinutes(minutes);
            } else if (reason == null) {
                reason = argument;
            } else {
                PlayerInteractionHelper.PrivateMessageOrServerLog(usage, sender);
                return;
            }
        }
        
        if (!TryResolveModerationTarget(arguments[0], sender, out TABGPlayerServer? target)) { return; }
        
        ServerClient world = WorldHelper.GetWorld();
        
        VisitorLog.SetBan(target.EpicUserName, reason, expires, sender?.PlayerName ?? "Server");
        PlayerKickCommand.Run(target, world, expires.HasValue ? KickReason.TemporaryBanned : KickReason.PermanentBanned);
        
        string duration = expires.HasValue ? $"until {expires.Value:u}" : "permanently";
        Plugin.Logger.LogInfo($"{sender?.PlayerName ?? "Server"} banned {target.PlayerName} ({target.EpicUserName}) {duration}. Reason: {reason ?? "none"}");
        PlayerInteractionHelper.PrivateMessageOrServerLog($"Banned {target.PlayerName} {duration}.", sender);
    }
    
    [ChatCommand("broadcast", "Puts a message in the kill feed. Usage: /broadcast <message> [colour]", 10)]
    public static void BroadcastCommand(string[] arguments, TABGPlayerServer? sender) {
        if (arguments.Length < 1 || arguments.Length > 2) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Usage: /broadcast <message> [colour]", sender);
            return;
        }
        
        PlayerColour colour = PlayerColour.White;
        if (arguments.Length > 1 && (!Enum.TryParse(arguments[1], true, out colour) || !Enum.IsDefined(typeof(PlayerColour), colour))) {
            PlayerInteractionHelper.PrivateMessageOrServerLog($"Unknown colour: {arguments[1]}. Colours: {string.Join(", ", Enum.GetNames(typeof(PlayerColour)))}", sender);
            return;
        }
        
        string message = "<\u7FE9=99><\u5A0F\uFFEC\uFFED\uFFE9=999>" + TextHelper.Reformat(arguments[0]);
        PlayerInteractionHelper.Broadcast(message, colour, null);
        Plugin.Logger.LogInfo($"{sender?.PlayerName ?? "Server"} broadcast: {message}");
    }
    
    private static bool TryResolveModerationTarget(string argument, TABGPlayerServer? sender, [NotNullWhen(true)] out TABGPlayerServer? target) {
        if (!SearchHelper.TryGetPlayerByNameOrID(argument, sender, out target)) { return false; }
        
        if (sender != null) {
            if (target.PlayerIndex == sender.PlayerIndex) {
                PlayerInteractionHelper.PrivateMessageOrServerLog("You cannot target yourself.", sender);
                return false;
            }
            if (VisitorLog.GetPermissionLevel(target.EpicUserName) >= ChatCommandManager.GetUserPermissionLevel(sender)) {
                PlayerInteractionHelper.PrivateMessageOrServerLog($"You cannot target {target.PlayerName}, their permission level is equal or higher than yours.", sender);
                return false;
            }
        }
        
        return true;
    }
}