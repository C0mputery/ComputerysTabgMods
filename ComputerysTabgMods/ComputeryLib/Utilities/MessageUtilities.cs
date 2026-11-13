using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class MessageUtilities {
    /// <summary>
    ///     This does include commands!
    /// </summary>
    public static event Action<TABGPlayerServer, string, bool>? OnChatMessage;
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThrowChatMessageCommand), nameof(ThrowChatMessageCommand.Run))]
    private static void ThrowChatMessageCommandPostfix(byte[] data, ServerClient world, byte sender) {
        if (!TryReadChatMessage(data, world, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player, message, true);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChatMessageCommand), nameof(ChatMessageCommand.Run))]
    private static void ChatMessageCommandPostfix(byte[] msgData, ServerClient world, byte sender) {
        if (!TryReadChatMessage(msgData, world, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player, message, false);
    }
    
    internal static bool TryReadChatMessage(byte[] data, ServerClient world, byte sender, [NotNullWhen(true)] out TABGPlayerServer? player, [NotNullWhen(true)] out string? message) {
        player = world.GameRoomReference.FindPlayer(sender);
        if (player == null) {
            message = null;
            return false;
        }
        int length = data[1];
        message = Encoding.Unicode.GetString(data, 2, length);
        return true;
    }
}