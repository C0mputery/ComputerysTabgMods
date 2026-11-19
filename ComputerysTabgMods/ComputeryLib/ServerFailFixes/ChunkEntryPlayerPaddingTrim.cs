using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class ChunkEntryPlayerPaddingTrim {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ChunkEntryCommand), nameof(ChunkEntryCommand.Run))]
    private static IEnumerable<CodeInstruction> TrimPadding(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // world.SendMessageToClients(EventCode.ChunkEntry, buffer, _player.UpdateMessageQueue.Receivers, reliable: true);
        matcher.MatchForward(true,
            new CodeMatch(instruction => instruction.LoadsConstant(EventCode.ChunkEntry)),
            new CodeMatch(instruction => instruction.IsLdloc())
        );
        matcher.Advance(1);
        
        // buffer into TrimPlayerPadding(buffer, list)
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChunkEntryPlayerPaddingTrim), nameof(TrimPlayerPadding)))
        );
        
        return matcher.InstructionEnumeration();
    }
    
    private static int GetVanillaPlayerSize(List<TABGPlayerServer> players) {
        const int vanillaBasePlayerSize = 9 + 4 + 1;
        
        int gearSize = 0;
        int attachmentSize = 0;
        foreach (TABGPlayerServer player in players) {
            gearSize += sizeof(int) * player.GearData.Length;
            attachmentSize += sizeof(short) * player.Attachments.Length;
        }
        return players.Count * (vanillaBasePlayerSize + sizeof(int) * gearSize + sizeof(short) * TABGPlayerServer.EquipmentDataLength + attachmentSize);
    }
    
    public static int GetPlayerSize(List<TABGPlayerServer> players) {
        const int basePlayerSize = sizeof(byte) + sizeof(byte) + sizeof(int) + sizeof(float) + sizeof(bool) + sizeof(bool) + sizeof(int) + sizeof(byte) + sizeof(byte);
        
        int size = 0;
        foreach (TABGPlayerServer player in players) { size += basePlayerSize + sizeof(int) * player.GearData.Length + sizeof(short) * TABGPlayerServer.EquipmentDataLength + sizeof(short) * player.Attachments.Length; }
        return size;
    }
    
    private static byte[] TrimPlayerPadding(byte[] buffer, List<TABGPlayerServer> players) {
        int padding = GetVanillaPlayerSize(players) - GetPlayerSize(players);
        if (padding <= 0) { return buffer; }
        
        byte[] trimmed = new byte[buffer.Length - padding];
        Array.Copy(buffer, trimmed, trimmed.Length);
        return trimmed;
    }
}