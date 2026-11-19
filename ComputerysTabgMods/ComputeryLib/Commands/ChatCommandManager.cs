using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using ComputeryLib.Helpers;
using ComputeryLib.Visitors;
using Landfall.Network;

namespace ComputeryLib.Commands;

public struct ChatCommandContext {
    public uint PermissionLevel;
    public string Description;
    public ChatCommandHandler Command;
}

public delegate void ChatCommandHandler(string[] arguments, TABGPlayerServer? sender);

public static class ChatCommandManager {
    private static readonly ConfigFile ChatCommandsConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "ChatCommands.cfg"), true);
    
    public static readonly Dictionary<string, ChatCommandContext> Commands = [];
    internal static bool HandleChatMessage(string message, TABGPlayerServer sender) {
        if (message.Length == 0 || message[0] != '/') { return false; }
        
        string messageNoSlash = message[1..];
        int firstSpaceIndex = messageNoSlash.IndexOf(' ');
        string commandName;
        string[] arguments;
        if (firstSpaceIndex == -1) {
            commandName = messageNoSlash.ToLower();
            arguments = [];
        }
        else {
            commandName = messageNoSlash[..firstSpaceIndex].ToLower();
            arguments = ParseArguments(messageNoSlash[firstSpaceIndex..]);
        }
        
        if (!Commands.TryGetValue(commandName, out ChatCommandContext chatCommandContext)) {
            PlayerInteractionHelper.PrivateMessage($"Unknown command: {commandName}, type /help for a list of commands you can use.", sender);
            return true;
        }
        
        uint userPermissionLevel = GetUserPermissionLevel(sender);
        if (userPermissionLevel < chatCommandContext.PermissionLevel) {
            PlayerInteractionHelper.PrivateMessage($"You do not have permission to use the /{commandName} command.", sender);
            return true;
        }
        
        try { chatCommandContext.Command(arguments, sender); }
        catch (Exception e) {
            Plugin.Logger.LogError($"Error executing command '{commandName}': {e.Message}");
            PlayerInteractionHelper.PrivateMessage($"An error occurred while executing the command: {e.Message}", sender);
        }
        
        return true;
    }
    
    private static string[] ParseArguments(string argumentsString) {
        List<string> arguments = [];
        StringBuilder current = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;
        
        foreach (char character in argumentsString) {
            if (character == '"') {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (!inQuotes && (character == ' ' || character == ',' || character == '\t')) {
                if (hasToken) { arguments.Add(current.ToString()); }
                current.Clear();
                hasToken = false;
            }
            else {
                current.Append(character);
                hasToken = true;
            }
        }
        
        if (hasToken) { arguments.Add(current.ToString()); }
        return arguments.ToArray();
    }
    
    internal static void RegisterCommands() {
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
        return VisitorLog.GetPermissionLevel(player.EpicUserName);
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class ChatCommandAttribute(string commandName, string description, uint defaultPermissionLevel) : Attribute {
    public readonly string CommandName = commandName;
    public readonly string Description = description;
    public readonly uint DefaultPermissionLevel = defaultPermissionLevel;
}