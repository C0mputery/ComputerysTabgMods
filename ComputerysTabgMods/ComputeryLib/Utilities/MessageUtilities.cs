using System;
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
        if (!GetValue(data, world, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player!, message!, true);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChatMessageCommand), nameof(ChatMessageCommand.Run))]
    private static void ChatMessageCommandPrefix(byte[] msgData, ServerClient world, byte sender) {
        if (!GetValue(msgData, world, sender, out TABGPlayerServer? player, out string? message)) { return; }
        OnChatMessage?.Invoke(player!, message!, false);
    }
    
    private static bool GetValue(byte[] data, ServerClient world, byte sender, out TABGPlayerServer? player, out string? message) {
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