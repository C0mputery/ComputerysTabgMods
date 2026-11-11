using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ComputeryLib.Utilities;
using Landfall.Network;

namespace ComputeryLib.Commands;

public struct ChatCommandContext {
    public int PermissionLevel;
    public string Description;
    public ChatCommandHandler Command;
}

public delegate void ChatCommandHandler(string[] arguments, TABGPlayerServer? sender);

public static class ChatCommandManager {
    private static readonly ConfigFile ChatCommandsConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "ChatCommands.cfg"), true);

    public static readonly Dictionary<string, ChatCommandContext> Commands = new();
    public static bool HandleChatMessage(string message, TABGPlayerServer sender) {
        if (message.Length == 0 || message[0] != '/') { return false; }
        
        string messageLowerNoSlash = message[1..];
        int firstSpaceIndex = messageLowerNoSlash.IndexOf(' ');
        string commandName;
        string[] arguments;
        if (firstSpaceIndex == -1) {
            commandName = messageLowerNoSlash.ToLower();
            arguments = [];
        }
        else {
            commandName = messageLowerNoSlash[..firstSpaceIndex].ToLower();
            string argumentsString = messageLowerNoSlash[firstSpaceIndex..].Trim();
            arguments = argumentsString.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        }
        
        if (!Commands.TryGetValue(commandName, out ChatCommandContext chatCommandContext)) {
            PlayerInteractionUtility.PrivateMessage($"Unknown command: {commandName}, type /help for a list of commands you can use.", sender);
            return true;
        }
        
        uint userPermissionLevel = GetUserPermissionLevel(sender);
        if (userPermissionLevel < chatCommandContext.PermissionLevel) {
            PlayerInteractionUtility.PrivateMessage($"You do not have permission to use the /{commandName} command.", sender);
            return true;
        }
        
        try { chatCommandContext.Command(arguments, sender); }
        catch (Exception e) {
            Plugin.Logger.LogError($"Error executing command '{commandName}': {e.Message}");
            PlayerInteractionUtility.PrivateMessage($"An error occurred while executing the command: {e.Message}", sender);
        }
        
        return true;
    }

    public static void HandleConsoleMessage(string message) {
        string[] parts = message.ToLower().Split(' ');
        string commandName = parts[0].ToLower();
        string[] arguments = parts.Skip(1).ToArray();

        if (!Commands.TryGetValue(commandName, out ChatCommandContext chatCommandContext)) {
            Plugin.Logger.LogInfo($"Unknown command: {commandName}, type help for a list of commands you can use."); 
            return;
        }
        chatCommandContext.Command(arguments, null);
    }

    public static void RegisterCommands() {
        Commands.Clear();
        
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            foreach (Type type in assembly.GetTypes()) {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                    IEnumerable<ChatCommandAttribute>? attributes = method.GetCustomAttributes<ChatCommandAttribute>();
                    foreach (ChatCommandAttribute attribute in attributes) {
                        if (attribute == null) { continue; }

                        string commandName = attribute.CommandName.ToLower();

                        if (Commands.ContainsKey(commandName)) {
                            Plugin.Logger.LogWarning($"Command '{commandName}' has already been registered. Skipping duplicate registration from method '{method.Name}' in type '{type.FullName}'.");
                            continue;
                        }

                        Commands[commandName] = new ChatCommandContext {
                            Command = (ChatCommandHandler)Delegate.CreateDelegate(typeof(ChatCommandHandler), method),
                            Description = attribute.Description,
                            PermissionLevel = ChatCommandsConfig.Bind("Permissions", commandName, attribute.DefaultPermissionLevel, $"Permission level for the /{commandName} command.\n{attribute.Description}").Value
                        };
                    }
                }
            }
        }
    }

    public static uint GetUserPermissionLevel(TABGPlayerServer player) { 
        return VisitorLog.VisitorLog.GetPermissionLevel(player.EpicUserName);
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class ChatCommandAttribute(string commandName, string description, int defaultPermissionLevel) : Attribute {
    public readonly string CommandName = commandName;
    public readonly string Description = description;
    public readonly int DefaultPermissionLevel = defaultPermissionLevel;
}
