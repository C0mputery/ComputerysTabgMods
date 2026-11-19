using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class MidMatchJoiningUtility {
    public static readonly bool AllowMidMatchJoining = Plugin.Config.Bind("Gameplay", "AllowMidMatchJoining", false, "When enabled, players can join a match after it has started.").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return AllowMidMatchJoining; }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.AcceptingPlayers))]
    private static void AcceptingPlayersPostfix(GameRoom __instance, ref bool __result) {
        if (__instance.CurrentGameState == GameState.Started) { __result = true; }
    }
    
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RoomInitRequestCommand), nameof(RoomInitRequestCommand.Run))]
    private static IEnumerable<CodeInstruction> AllowJoinAfterStart(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        CodeMatcher matcher = new CodeMatcher(instructions, generator);
        
        matcher.MatchForward(false,
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
        
        foreach (TABGPlayerServer other in room.PlayersAndSpectators()) {
            if (other == player) { continue; }
            int kills = Math.Min((int)other.NumberOfKills, 10);
            for (int i = 0; i < kills; i++) { FakePlayerHelper.SpoofKill(other, playerID); }
        }
        
        // Jank solution to make it so that people's weapons show up
        // Don't know why the like current game world command doesn't properly send the stuff over
        world.WaitThenDoAction(2f, () => ResyncWeapons(playerID));
    }
    
    private static void ResyncWeapons(byte joinerID) {
        ServerClient world = WorldHelper.GetWorld();
        GameRoom room = world.GameRoomReference;
        TABGPlayerServer? joiner = room.FindPlayer(joinerID);
        if (joiner?.PlayerObject == null) { return; }
        
        foreach (ChunkDataServer chunk in joiner.AreaOfInterest.GetAreasOfInterest()) {
            List<TABGPlayerServer>? players = ServerChunks.Instance.GetPlayers(chunk);
            if (players == null) { continue; }
            
            foreach (TABGPlayerServer other in players) {
                if (other == joiner || other.IsDead || !other.HasDropped) { continue; }
                short throwableIdentifier = Throwables.TryGetValue(other, out StrongBox<short> throwable) ? throwable.Value : (short)-1;
                world.SendMessageToClients(EventCode.WeaponChanged, PacketHelper.BuildWeaponChanged(other, throwableIdentifier), joinerID, true);
            }
        }
    }
    
    private static readonly ConditionalWeakTable<TABGPlayerServer, StrongBox<short>> Throwables = new ConditionalWeakTable<TABGPlayerServer, StrongBox<short>>();
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WeaponChangedCommand), nameof(WeaponChangedCommand.Run))]
    private static void StoreThrowable(byte[] msgData, ServerClient world) {
        TABGPlayerServer? player = world.GameRoomReference.FindPlayer(msgData[0]);
        if (player == null) { return; }
        Throwables.GetOrCreateValue(player).Value = BitConverter.ToInt16(msgData, msgData.Length - 2);
    }
}