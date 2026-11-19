using System;
using Epic.OnlineServices;
using Epic.OnlineServices.AntiCheatCommon;
using Epic.OnlineServices.Auth;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Visitors;

[HarmonyPatch(typeof(RoomInitRequestCommand))]
public static class OnVerifiesEpicTokenPatch {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoomInitRequestCommand.OnVerifiesEpicToken))]
    private static void OnVerifiesEpicTokenPostfix(ref VerifyIdTokenCallbackInfo data) {
        if (data.ResultCode != Result.Success || data.ClientData is not TABGPlayerServer tabgPlayerServer) { return; }
        if (VisitorLog.TryGetActiveBan(tabgPlayerServer.EpicUserName, out string? reason, out DateTime? expires)) {
            Plugin.Logger.LogInfo($"Banned player {tabgPlayerServer.PlayerName} ({tabgPlayerServer.EpicUserName}) tried to join. Reason: {reason ?? "none"}, expires: {(expires.HasValue ? expires.Value.ToString("u") : "never")}");
            ServerClient world = RoomInitRequestCommand.m_TempWorld;
            PlayerKickCommand.Run(tabgPlayerServer, world, expires.HasValue ? KickReason.TemporaryBanned : KickReason.PermanentBanned);
            if (world.GameRoomReference.CurrentGameSettings.UseKicks) { return; }
        }
        VisitorLog.LogVisitor(tabgPlayerServer);
    }
}