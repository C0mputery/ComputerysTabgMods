using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ComputeryLib.Helpers;
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
            PlayerInteractionHelper.PrivateMessageOrServerLog($"Available commands: {commandList}", sender);
        }
        else {
            string commandName = arguments[0].ToLower();
            if (ChatCommandManager.Commands.TryGetValue(commandName, out ChatCommandContext chatCommandContext)) { PlayerInteractionHelper.PrivateMessageOrServerLog($"/{commandName}: {chatCommandContext.Description}", sender); }
            else { PlayerInteractionHelper.PrivateMessageOrServerLog($"No description found for command: {commandName}", sender); }
        }
    }
    
    [ChatCommand("list", "Lists all connected players.", 0)]
    public static void ListCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("The world is not available yet.", sender);
            return;
        }
        
        bool showIds = sender == null || ChatCommandManager.GetUserPermissionLevel(sender) >= 1;
        List<TABGPlayerServer> players = world.GameRoomReference.PlayersAndSpectators().ToList();
        IEnumerable<string> entries = players.Select(p => showIds ? $"{p.PlayerName} ({p.PlayerIndex})" : p.PlayerName);
        PlayerInteractionHelper.PrivateMessageOrServerLog($"Players ({players.Count}): {string.Join(", ", entries)}", sender);
    }
    
    [ChatCommand("start", "Starts the game with default countdown or specified countdown in seconds.", 1)]
    public static void StartCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        if (world.GameRoomReference.CurrentGameState != GameState.WaitingForPlayers && world.GameRoomReference.CurrentGameState != GameState.CountDown) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Game is already in progress.", sender);
            return;
        }
        
        if (arguments.Length < 1) {
            world.GameRoomReference.StartCountDown();
            return;
        }
        
        if (!float.TryParse(arguments[0], out float timeInSeconds) || timeInSeconds < 0) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Invalid time specified. Must be a non-negative number.", sender);
            return;
        }
        if (timeInSeconds == 0) {
            timeInSeconds = float.Epsilon;
        }
        
        world.GameRoomReference.StartCountDown(timeInSeconds);
    }
    
    private const int MaxGiveQuantity = 25500;
    
    [ChatCommand("give", "Gives an item. Usage: /give <item_id> [quantity] or from the server: give <player_name|player_id> <item_id> [quantity]", 10)]
    public static void GiveCommand(string[] arguments, TABGPlayerServer? sender) {
        if (!WorldHelper.TryGetWorld(out ServerClient? world)) {
            Plugin.Logger.LogError("World client is not available?");
            return;
        }
        
        bool isNonPlayerSender = sender == null;
        int requiredArgumentCount = isNonPlayerSender ? 2 : 1;
        if (arguments.Length < requiredArgumentCount) {
            string usage = isNonPlayerSender ? "Usage: give <player_name|player_id> <item_id> [quantity]" : "Usage: /give <item_id> [quantity]";
            PlayerInteractionHelper.PrivateMessageOrServerLog(usage, sender);
            return;
        }
        
        TABGPlayerServer? targetPlayer = sender;
        string[] itemArguments = arguments;
        if (targetPlayer == null) {
            if (!SearchHelper.TryGetPlayerByNameOrID(arguments[0], sender, out targetPlayer)) { return; }
            itemArguments = [.. arguments.Skip(1)];
        }
        
        if (!int.TryParse(itemArguments[0], out int itemId)) {
            PlayerInteractionHelper.PrivateMessageOrServerLog("Invalid item ID. Must be a number.", sender);
            return;
        }
        
        int quantity = 1;
        bool hasQuantityArgument = itemArguments.Length > 1;
        if (hasQuantityArgument) {
            if (!int.TryParse(itemArguments[1], out int parsedQuantity) || parsedQuantity < 1) {
                PlayerInteractionHelper.PrivateMessageOrServerLog("Invalid quantity. Must be a positive number.", sender);
                return;
            }
            quantity = Math.Min(parsedQuantity, MaxGiveQuantity);
        }
        
        for (int remaining = quantity; remaining > 0; remaining -= byte.MaxValue) {
            world.GivePlayerWeapon(targetPlayer.PlayerIndex, itemId, (byte)Math.Min(remaining, byte.MaxValue));
        }
        
        PlayerInteractionHelper.PrivateMessageOrServerLog($"Gave item {itemId} x{quantity} to {targetPlayer.PlayerName}", sender);
    }
}