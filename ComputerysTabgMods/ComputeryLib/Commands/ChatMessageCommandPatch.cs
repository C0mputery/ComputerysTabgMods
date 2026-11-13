using ComputeryLib.Utilities;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Commands;

[HarmonyPatch]
public static class ChatMessageCommandPatch {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ChatMessageCommand), nameof(ChatMessageCommand.Run))]
    public static bool ChatMessageCommandRunPrefix(byte[] msgData, ServerClient world, byte sender) { return Parse(msgData, world, sender); }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThrowChatMessageCommand), nameof(ThrowChatMessageCommand.Run))]
    public static bool ThrowChatMessageCommandRunPrefix(byte[] data, ServerClient world, byte sender) { return Parse(data, world, sender); }
    
    private static bool Parse(byte[] msgData, ServerClient world, byte sender) {
        if (!MessageUtilities.TryReadChatMessage(msgData, world, sender, out TABGPlayerServer? player, out string? message)) { return true; }
        return !ChatCommandManager.HandleChatMessage(message, player);
    }
}