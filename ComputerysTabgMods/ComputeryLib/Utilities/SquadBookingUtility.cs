using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class SquadBookingUtility {
    public static readonly bool AllowJoinWithoutBooking = Plugin.Config.Bind("Gameplay", "AllowJoinWithoutBooking", true, "Let's squad members skip the squad host requirement. Fixes a weird client issue as well, where for some reason in certain cases they can just never connect to the server.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AllowJoinWithoutBooking; }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RoomInitRequestCommand), nameof(RoomInitRequestCommand.Run))]
    private static IEnumerable<CodeInstruction> AllowMissingBooking(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GameRoom), nameof(GameRoom.GetReservedSquadSlots))));
        matcher.Set(OpCodes.Call, AccessTools.Method(typeof(SquadBookingUtility), nameof(GetReservedOrFreeSlot)));
        return matcher.InstructionEnumeration();
    }
    
    private static byte GetReservedOrFreeSlot(GameRoom room, ulong squadKey) {
        byte reserved = room.GetReservedSquadSlots(squadKey);
        if (reserved > 0) { return reserved; }
        return room.Players.Count + room.GetAllReservedSlots() >= room.CurrentGameSettings.MaxPlayers ? (byte)0 : (byte)1;
    }
}