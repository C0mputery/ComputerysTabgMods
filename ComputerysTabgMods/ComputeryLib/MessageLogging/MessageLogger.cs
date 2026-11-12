using System;
using System.IO;
using BepInEx.Logging;
using ComputeryLib.Utilities;

namespace ComputeryLib.MessageLogging;

public static class MessageLogger {
    private static readonly string MessageLogDirectory = Path.Combine(PersistantDataUtility.PersistentDataPath, "MessageLogs");
    private static readonly ManualLogSource Logger = new ManualLogSource("Player Messages");
    
    static MessageLogger() {
        if (!Directory.Exists(MessageLogDirectory)) { Directory.CreateDirectory(MessageLogDirectory); }
    }
    
    private static void LogMessage(string displayName, string epicID, string message, bool wasThrown) {
        string logFilePath = Path.Combine(MessageLogDirectory, $"{epicID}.log");
        string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {(wasThrown ? "[THROWN] " : "")}{message}";
        File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        Logger.LogInfo($"[{displayName}] {logEntry}");
    }
    
    public static void InitializeLogger() {
        MessageUtilities.OnChatMessage += (player, message, wasThrown) => { LogMessage(player.PlayerName, player.EpicUserName, message, wasThrown); };
    }
}