using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

public static class MidMatchJoiningUtility {
    public static readonly bool AllowMidMatchJoining = Plugin.Config.Bind("Gameplay", "AllowMidMatchJoining", false, "When enabled, players can join a match after it has started.").Value;
    
    public static void ApplyPatches() {
        if (AllowMidMatchJoining) { Plugin.Harmony.PatchAll(typeof(MidMatchJoiningUtility)); }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.AcceptingPlayers))]
    private static void AcceptingPlayersPostfix(GameRoom __instance, ref bool __result) {
        if (__instance.CurrentGameState == GameState.Started) { __result = true; }
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RoomInitRequestCommand), nameof(RoomInitRequestCommand.Run))]
    private static IEnumerable<CodeInstruction> AllowJoinAfterStart(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        CodeMatcher matcher = new CodeMatcher(instructions, generator);
        
        matcher.MatchForward(
            false,
            new CodeMatch(OpCodes.Switch),
            new CodeMatch(code => code.opcode == OpCodes.Br || code.opcode == OpCodes.Br_S)
        );
        Label[] caseTargets = (Label[])matcher.Operand;
        Label bookingCheck = caseTargets[(int)GameState.WaitingForPlayers];
        
        // Shared labels get dropped bcs MonoMod emits switch tables via labels.Distinct().
        Label flyingBookingCheck = generator.DefineLabel();
        Label startedBookingCheck = generator.DefineLabel();
        caseTargets[(int)GameState.Flying] = flyingBookingCheck;
        caseTargets[(int)GameState.Started] = startedBookingCheck;
        
        matcher.MatchForward(false, new CodeMatch(code => code.labels.Contains(bookingCheck)));
        matcher.AddLabels([flyingBookingCheck, startedBookingCheck]);
        
        return matcher.InstructionEnumeration();
    }
    
    // Landfall never sends the actual weapons with this packet in Battle Royale. Hard coded at 0.
    // They still pull the number of weapons and allocate space in the buffer for it, though
    // Meaning the packet gets too big to actually send over the wire, even if it's all just empty.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CurrentGameWorldCommand), nameof(CurrentGameWorldCommand.Run))]
    private static IEnumerable<CodeInstruction> RemoveWeaponReservation(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new CodeMatcher(instructions);
        
        // int bytesForWeapons = 4 + count * 24;
        matcher.MatchForward(false, 
            new CodeMatch(instruction => instruction.LoadsConstant(4)),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(instruction => instruction.LoadsConstant(24)),
            new CodeMatch(OpCodes.Mul),
            new CodeMatch(OpCodes.Add),
            new CodeMatch(instruction => instruction.IsStloc())
        );
        
        // int bytesForWeapons = 4; 
        matcher.Advance(1);
        matcher.RemoveInstructions(4);
        
        return matcher.InstructionEnumeration();
    }
    
    // Dropper and chunk stuff
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CurrentGameWorldCommand), nameof(CurrentGameWorldCommand.Run))]
    private static void CatchUpWorldInit(ServerClient world, byte playerID) {
        GameRoom room = world.GameRoomReference;
        if (room.CurrentGameState != GameState.Started) { return; }
        
        TABGPlayerServer player = room.FindPlayer(playerID);
        Dropper? plane = world.m_SpawnedPlane;
        if (plane != null) {
            player.PlayerObject.FetchComponent<ServerFollowDropper>();
            PlaneUpdateCommand.Run(null, world);
        }
        else {
            player.Dropped();
            world.ForceChunkEntry(player);
        }
        
        // Jank solution to make it so that people's weapons show up
        // Don't know why the like current game world command doesn't properly send the stuff over
        world.WaitThenDoAction(2f, () => ResyncWeapons(world, playerID));
    }
    
    private static void ResyncWeapons(ServerClient world, byte joinerID) {
        GameRoom room = world.GameRoomReference;
        TABGPlayerServer? joiner = room.FindPlayer(joinerID);
        if (joiner?.PlayerObject == null) { return; }
        
        foreach (ChunkDataServer chunk in joiner.AreaOfInterest.GetAreasOfInterest()) {
            List<TABGPlayerServer>? players = ServerChunks.Instance.GetPlayers(chunk);
            if (players == null) { continue; }
            
            foreach (TABGPlayerServer other in players) {
                if (other == joiner || other.IsDead || !other.HasDropped) { continue; }
                world.SendMessageToClients(EventCode.WeaponChanged, BuildWeaponChanged(other), joinerID, true);
            }
        }
    }
    
    private static readonly Dictionary<byte, short> Throwables = [];
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WeaponChangedCommand), nameof(WeaponChangedCommand.Run))]
    private static void StoreThrowable(byte[] msgData) { Throwables[msgData[0]] = BitConverter.ToInt16(msgData, msgData.Length - 2); }
    
    private static byte[] BuildWeaponChanged(TABGPlayerServer player) {
        byte[] buffer = new byte[2 + 5 * 2 + 1 + player.Attachments.Length * 2 + 2];
        using MemoryStream stream = new MemoryStream(buffer);
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(player.PlayerIndex);
        writer.Write((byte)player.Equipment[5]);
        for (int i = 0; i < 5; i++) { writer.Write(player.Equipment[i]); }
        writer.Write((byte)player.Attachments.Length);
        foreach (short attachment in player.Attachments) { writer.Write(attachment); }
        writer.Write(Throwables.GetValueOrDefault(player.PlayerIndex, (short)-1));
        return buffer;
    }
}