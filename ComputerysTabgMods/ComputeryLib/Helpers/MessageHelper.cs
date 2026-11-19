using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Helpers;

[HarmonyPatch]
public static class MessageHelper {
    /// <summary>
    /// This does include commands!
    /// </summary>
    public static event Action<TABGPlayerServer, string, bool>? OnChatMessage;
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThrowChatMessageCommand), nameof(ThrowChatMessageCommand.Run))]
    private static void ThrowChatMessageCommandPostfix(byte[] data, ServerClient world, byte sender) {
        if (!TryReadChatMessage(data, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player, message, true);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChatMessageCommand), nameof(ChatMessageCommand.Run))]
    private static void ChatMessageCommandPostfix(byte[] msgData, ServerClient world, byte sender) {
        if (!TryReadChatMessage(msgData, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player, message, false);
    }
    
    internal static bool TryReadChatMessage(byte[] data, byte sender, [NotNullWhen(true)] out TABGPlayerServer? player, [NotNullWhen(true)] out string? message) {
        const int lengthOffset = 1;
        const int messageOffset = 2;
        
        player = WorldHelper.GetWorld().GameRoomReference.FindPlayerOrSpectator(sender);
        if (player == null) {
            message = null;
            return false;
        }
        int length = data[lengthOffset];
        message = Encoding.Unicode.GetString(data, messageOffset, length);
        return true;
    }
}