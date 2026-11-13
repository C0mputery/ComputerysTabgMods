using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Error;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch(typeof(UnityTransportServer))]
public static class ReliableSendBufferFix {
    public static readonly int MaxBufferedReliablePacketsPerPlayer = Plugin.Config.Bind("Network", "MaxBufferedReliablePacketsPerPlayer", 1024, "Maximum held reliable packets held beyond the default buffer. 0 disables this.").Value;
    
    private static readonly List<int> PlayerIDBuffer = [];
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return MaxBufferedReliablePacketsPerPlayer > 0; }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(UnityTransportServer.SendMessageToClients), typeof(NativeArray<byte>), typeof(byte), typeof(byte[]), typeof(bool), typeof(EventCode))]
    private static bool QueueBehindHeldPackages(UnityTransportServer __instance, byte playerID, byte[] buffer, bool reliable, EventCode code) {
        if (!reliable || !__instance.m_BufferedPackagesDIC.ContainsKey(playerID)) { return true; }
        
        __instance.AddBufferedPackage(new UnityTransportServer.BufferedPackage { EventCode = code, Data = buffer, RecipentIndicies = [playerID] }, playerID);
        return false;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(UnityTransportServer.AddBufferedPackage))]
    private static bool CapHeldPackages(UnityTransportServer __instance, UnityTransportServer.BufferedPackage pkg, byte recipent) {
        if (!__instance.m_BufferedPackagesDIC.TryGetValue(recipent, out List<UnityTransportServer.BufferedPackage> held) || held.Count < MaxBufferedReliablePacketsPerPlayer) { return true; }
        
        Plugin.Logger.LogWarning($"Player ID {recipent} is at {MaxBufferedReliablePacketsPerPlayer} held reliable packets, dropping {pkg.EventCode}.");
        return false;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(nameof(UnityTransportServer.Update))]
    private static void FlushHeldPackages(UnityTransportServer __instance) {
        if (!__instance.m_isHosting) { return; }
        
        PlayerIDBuffer.Clear();
        PlayerIDBuffer.AddRange(__instance.m_BufferedPackagesDIC.Keys);
        foreach (int playerID in PlayerIDBuffer) {
            List<UnityTransportServer.BufferedPackage> held = __instance.m_BufferedPackagesDIC[playerID];
            if (!__instance.m_playerIDToConnection.TryGetValue((byte)playerID, out NetworkConnection connection)) {
                __instance.m_BufferedPackagesDIC.Remove(playerID);
                continue;
            }
            
            int sent = 0;
            while (sent < held.Count) {
                UnityTransportServer.BufferedPackage package = held[sent];
                byte[] data = new byte[package.Data.Length + 1];
                data[0] = (byte)package.EventCode;
                package.Data.CopyTo(data, 1);
                
                NativeArray<byte> nativeBuffer = new NativeArray<byte>(data, Allocator.Temp);
                int result = Send(__instance, connection, nativeBuffer);
                nativeBuffer.Dispose();
                
                if (result < 0) {
                    if ((StatusCode)result != StatusCode.NetworkSendQueueFull) {
                        Plugin.Logger.LogError($"Failed to resend held {package.EventCode} to player ID {playerID}: {(StatusCode)result}.");
                    }
                    break;
                }
                
                CountSentPackage(__instance, (byte)playerID);
                sent++;
            }
            
            held.RemoveRange(0, sent);
            if (held.Count == 0) {
                __instance.m_BufferedPackagesDIC.Remove(playerID);
                Plugin.Logger.LogInfo($"Caught up on held reliable packets for player ID {playerID}.");
            }
        }
    }
    
    private static int Send(UnityTransportServer server, NetworkConnection connection, NativeArray<byte> payload) {
        int result = server.m_networkDriver.BeginSend(server.m_reliablePipeline, connection, out DataStreamWriter writer);
        if (result < 0) { return result; }
        
#pragma warning disable Harmony003
        writer.WriteUShort((ushort)payload.Length);
#pragma warning restore Harmony003
        
        writer.WriteBytes(payload);
        return server.m_networkDriver.EndSend(writer);
    }
    
    private static void CountSentPackage(UnityTransportServer server, byte playerID) {
        server.m_NumberOfPackagesPerConnectionDic.TryGetValue(playerID, out byte count);
        server.m_NumberOfPackagesPerConnectionDic[playerID] = ++count;
        if (count >= 5) { server.m_networkDriver.ScheduleFlushSend(server.m_UpdateJob).Complete(); }
    }
}