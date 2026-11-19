using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Commands;

[HarmonyPatch]
public static class ChatMessageCommandPatch {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ChatMessageCommand), nameof(ChatMessageCommand.Run))]
    public static bool ChatMessageCommandRunPrefix(byte[] msgData, ServerClient world, byte sender) { return Parse(msgData, sender); }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThrowChatMessageCommand), nameof(ThrowChatMessageCommand.Run))]
    public static bool ThrowChatMessageCommandRunPrefix(byte[] data, ServerClient world, byte sender) { return Parse(data, sender); }
    
    private static bool Parse(byte[] msgData, byte sender) {
        if (!MessageHelper.TryReadChatMessage(msgData, sender, out TABGPlayerServer? player, out string? message)) { return true; }
        return !ChatCommandManager.HandleChatMessage(message, player);
    }
}