using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ComputeryLib.Helpers;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using AccountIDEntry = (byte PlayerIndex, string SteamID, string PlayFabID, Epic.OnlineServices.Utf8String EpicUserName, bool IsAdmin);

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class AccountIDBroadcastFix {
    public static readonly float SendInterval = Plugin.Config.Bind("Network", "AccountIDSendInterval", 2.5f, "Seconds between account ID packets sent to the same client. Too fast, clients will hit a rate limit and won't load in the cosmetics like Gangster mode.").Value;
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.ValidateSteamTicket))]
    private static void AssignDummySteamID(TABGPlayerServer player) { player.AssignAccountIDs("0", player.PlayFabID); }
    
    // Unfortunate force prefix override.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssignPlayerAccountIDsCommand), nameof(AssignPlayerAccountIDsCommand.Run), typeof(ServerClient), typeof(List<AccountIDEntry>), typeof(byte))]
    private static bool RunPrefix(ServerClient server, List<AccountIDEntry> PlayerIDs, byte recievingPlayer) {
        if (recievingPlayer != byte.MaxValue) {
            TABGPlayerServer? recipient = server.GameRoomReference.FindPlayerOrSpectator(recievingPlayer);
            if (recipient != null) { Enqueue(recipient, PlayerIDs); }
            return false;
        }
        
        foreach (TABGPlayerServer player in server.GameRoomReference.PlayersAndSpectators()) { Enqueue(player, PlayerIDs); }
        return false;
    }
    
    private static readonly ConditionalWeakTable<TABGPlayerServer, List<AccountIDEntry>> PendingByRecipient = new ConditionalWeakTable<TABGPlayerServer, List<AccountIDEntry>>();
    private static void Enqueue(TABGPlayerServer recipient, List<AccountIDEntry> entries) {
        List<AccountIDEntry> pending = PendingByRecipient.GetOrCreateValue(recipient);
        
        foreach (AccountIDEntry entry in entries) {
            int queuedIndex = pending.FindIndex(queued => queued.PlayerIndex == entry.PlayerIndex);
            if (queuedIndex != -1) { pending[queuedIndex] = entry; }
            else { pending.Add(entry); }
        }
    }
    
    private static float _nextSendTime;
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ServerClient), nameof(ServerClient.Update))]
    private static void UpdatePostfix(ServerClient __instance) {
        float now = Time.unscaledTime;
        if (now < _nextSendTime) { return; }
        _nextSendTime = now + SendInterval;
        
        GameRoom room = __instance.GameRoomReference;
        foreach ((TABGPlayerServer recipient, List<AccountIDEntry> pending) in PendingByRecipient) {
            if (room.FindPlayerOrSpectator(recipient.PlayerIndex) != recipient) {
                PendingByRecipient.Remove(recipient);
                continue;
            }
            
            pending.RemoveAll(entry => {
                TABGPlayerServer? subject = room.FindPlayerOrSpectator(entry.PlayerIndex);
                return subject == null || subject.PlayFabID != entry.PlayFabID;
            });
            if (pending.Count > 0) {
                TABGPlayerServer recipientPlayerIsSpectatingPlayer = recipient.IsSpectatingPlayer ?? recipient;
                int closestIndex = FindClosestEntryIndex(room, recipientPlayerIsSpectatingPlayer.PlayerPosition, pending);
                AccountIDEntry entry = pending[closestIndex];
                pending.RemoveAt(closestIndex);
                AssignPlayerAccountIDsCommandNoLog(__instance, [entry], recipient.PlayerIndex);
            }
            if (pending.Count == 0) { PendingByRecipient.Remove(recipient); }
        }
    }
    
    private static int FindClosestEntryIndex(GameRoom room, Vector3 viewPosition, List<AccountIDEntry> pending) {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < pending.Count; i++) {
            TABGPlayerServer subjectPlayer = room.FindPlayerOrSpectator(pending[i].PlayerIndex)!;
            float distance = (subjectPlayer.PlayerPosition - viewPosition).sqrMagnitude;
            if (distance >= closestDistance) { continue; }
            closestIndex = i;
            closestDistance = distance;
        }
        return closestIndex;
    }
    
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(AssignPlayerAccountIDsCommand), nameof(AssignPlayerAccountIDsCommand.Run), typeof(ServerClient), typeof(List<AccountIDEntry>), typeof(byte))]
    private static void AssignPlayerAccountIDsCommandNoLog(ServerClient server, List<AccountIDEntry> PlayerIDs, byte recievingPlayer) {
#pragma warning disable CS8321 // Local function is declared but never used
        IEnumerable<CodeInstruction> RemoveLog(IEnumerable<CodeInstruction> instructions) {
            CodeMatcher matcher = new CodeMatcher(instructions);
            
            // LandLog.Log($"Sending Player steam IDs to {recievingPlayer} of count: {PlayerIDs.Count}");
            matcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Sending Player steam IDs to {0} of count: {1}"));
            List<Label> labelsToPreserve = [.. matcher.Instruction.labels];
            matcher.RemoveInstructions(9);
            matcher.Instruction.labels.AddRange(labelsToPreserve);
            
            return matcher.InstructionEnumeration();
        }
#pragma warning restore CS8321 // Local function is declared but never used
    }
}
