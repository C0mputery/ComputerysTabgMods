using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComputeryLib.Utilities;
using Landfall.Network;

namespace ComputeryLib.Commands;

public static class BasicCommands {
    [ChatCommand("help", "Displays the description of a command or lists all commands you can use.", 0)]
    public static void HelpCommand(string[] arguments, TABGPlayerServer? sender) {
        if (arguments.Length == 0) {
            StringBuilder commandListBuilder = new StringBuilder();
            uint userPermissionLevel = sender != null ? ChatCommandManager.GetUserPermissionLevel(sender) : uint.MaxValue;
            foreach (KeyValuePair<string, ChatCommandContext> command in ChatCommandManager.Commands) {
                if (userPermissionLevel >= command.Value.PermissionLevel) { commandListBuilder.Append($"{command.Key}, "); }
            }
            string commandList = commandListBuilder.ToString().TrimEnd(',', ' ');
            PlayerInteractionUtility.PrivateMessageOrConsoleLog($"Available commands: {commandList}", sender);
        } 
        else {
            string commandName = arguments[0].ToLower();
            if (ChatCommandManager.Commands.TryGetValue(commandName, out ChatCommandContext chatCommandContext)) { PlayerInteractionUtility.PrivateMessageOrConsoleLog($"/{commandName}: {chatCommandContext.Description}", sender); } 
            else { PlayerInteractionUtility.PrivateMessageOrConsoleLog($"No description found for command: {commandName}", sender); }
        }
    }
    
    [ChatCommand("start", "Starts the game with default countdown or specified countdown in seconds.", 1)]
    public static void StartCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        if (world!.GameRoomReference.CurrentGameState != GameState.WaitingForPlayers && world.GameRoomReference.CurrentGameState != GameState.CountDown) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog("Game is already in progress.", sender);
            return;
        }
        
        if (arguments.Length < 1) {
            world.GameRoomReference.StartCountDown();
            return;
        }
        
        if (!float.TryParse(arguments[0], out float timeInSeconds) || timeInSeconds < 0) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog("Invalid time specified. Must be a non-negative number.", sender);
            return;
        }
        if (timeInSeconds == 0) {
            timeInSeconds = float.Epsilon;
        }

        world.GameRoomReference.StartCountDown(timeInSeconds);
    }

    [ChatCommand("setlevel", "Sets the permission level of a player. Usage: /setlevel <player_name|player_id|epic_username> <level>", 10)]
    public static void SetLevelCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        if (arguments.Length < 2) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog("Usage: /setlevel <player_name|player_id|epic_username> <level>", sender);
            return;
        }

        if (!uint.TryParse(arguments[^1], out uint level)) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog("Invalid level specified. Must be a non-negative integer.", sender);
            return;
        }

        string searchValue = string.Join(" ", arguments.Take(arguments.Length - 1));
        if (!SearchUtility.TryGetPlayerByNameOrID(searchValue, out TABGPlayerServer? foundPlayer)) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog($"Player not found: {searchValue}", sender);
            return;
        }

        VisitorLog.VisitorLog.SetPermissionLevel(foundPlayer!.EpicUserName, level);
        PlayerInteractionUtility.PrivateMessageOrConsoleLog($"Set permission level {level} for player {foundPlayer.PlayerName}", sender);
    }

    [ChatCommand("give", "Gives an item. Usage: /give <item_id> [quantity] or console: give <player> <item_id> [quantity]", 10)]
    public static void GiveCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }

        TABGPlayerServer targetPlayer;
        int itemIdArgumentIndex;

        if (sender == null) {
            if (arguments.Length < 2) {
                PlayerInteractionUtility.PrivateMessageOrConsoleLog("Usage: give <player> <item_id>", sender);
                return;
            }

            if (!SearchUtility.TryGetPlayerByNameOrID(arguments[0], out TABGPlayerServer? foundPlayer)) {
                PlayerInteractionUtility.PrivateMessageOrConsoleLog($"Player not found: {arguments[0]}", sender);
                return;
            }

            targetPlayer = foundPlayer!;
            itemIdArgumentIndex = 1;
        }
        else {
            if (arguments.Length < 1) {
                PlayerInteractionUtility.PrivateMessageOrConsoleLog("Usage: /give <item_id>", sender);
                return;
            }

            targetPlayer = sender;
            itemIdArgumentIndex = 0;
        }

        if (!int.TryParse(arguments[itemIdArgumentIndex], out int itemId)) {
            PlayerInteractionUtility.PrivateMessageOrConsoleLog("Invalid item ID. Must be a number.", sender);
            return;
        }

        int quantity = 1;
        int quantityArgumentIndex = itemIdArgumentIndex + 1;
        if (arguments.Length > quantityArgumentIndex) {
            if (!int.TryParse(arguments[quantityArgumentIndex], out quantity) || quantity < 1) {
                PlayerInteractionUtility.PrivateMessageOrConsoleLog("Invalid quantity. Must be a positive number.", sender);
                return;
            }
        }

        int remaining = quantity;
        while (remaining > 0) {
            byte packetQuantity = (byte)Math.Min(remaining, byte.MaxValue);
            world!.GivePlayerWeapon(targetPlayer.PlayerIndex, itemId, packetQuantity);
            remaining -= packetQuantity;
        }

        PlayerInteractionUtility.PrivateMessageOrConsoleLog($"Gave item {itemId} x{quantity} to {targetPlayer.PlayerName}", sender);
    }
    
    [ChatCommand("kick", "Kicks a player from the server. Usage: /kick <player_name|player_id|epic_username>, <reason:optional>", 10)]
    public static void KickCommand(string[] arguments, TABGPlayerServer? sender) {
        
    }
    
    [ChatCommand("ban", "Bans a players Epic Id from the server. Usage: /ban <player_name|player_id|epic_username>, <reason:optional>, <duration_in_minutes:optional>", 10)]
    public static void BanCommand(string[] arguments, TABGPlayerServer? sender) {
        
    }

    [ChatCommand("ipban", "Bans a players IP from the server. Usage: /ipban <player_name|player_id|epic_username|ip>, <reason:optional>, <duration_in_minutes:optional>. Only works on Enet servers.", 10)]
    public static void IPBanCommand(string[] arguments, TABGPlayerServer? sender) {
        
    }
}