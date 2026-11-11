using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using HarmonyLib;

namespace ComputeryLib.Utilities;

public static class PacketQueueUtility {
    private const int DefaultPacketQueueMaxSizeBytes = 10 * 1024 * 1024;

    public static readonly int IncomingPacketQueueMaxSizeBytes = Plugin.Config.Bind("Network", "IncomingPacketQueueMaxSizeBytes", DefaultPacketQueueMaxSizeBytes, "Maximum incoming EOS P2P packet queue size in bytes. The EOS default is 1048576 (1 MiB), but I've bumped it (10 Mib). Set to 0 for an unlimited queue.").Value;
    public static readonly int OutgoingPacketQueueMaxSizeBytes = Plugin.Config.Bind("Network", "OutgoingPacketQueueMaxSizeBytes", DefaultPacketQueueMaxSizeBytes, "Maximum outgoing EOS P2P packet queue size in bytes. The EOS default is 1048576 (1 MiB), but I've bumped it (10 Mib). Set to 0 for an unlimited queue.").Value;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Easy_AC_Server), nameof(Easy_AC_Server.InitEpic))]
    public static void InitEpicPostfix() {
        if (Easy_AC_Server.s_PlatformInterface == null) {
            Plugin.Logger.LogError("Failed to set the EOS P2P packet queue size likely bcs epic failed to init.");
            return;
        }

        P2PInterface p2pInterface = Easy_AC_Server.s_PlatformInterface.GetP2PInterface();
        SetPacketQueueSizeOptions setPacketQueueSizeOptions = new SetPacketQueueSizeOptions {
            IncomingPacketQueueMaxSizeBytes = (ulong)IncomingPacketQueueMaxSizeBytes,
            OutgoingPacketQueueMaxSizeBytes = (ulong)OutgoingPacketQueueMaxSizeBytes
        };

        Result result = p2pInterface.SetPacketQueueSize(ref setPacketQueueSizeOptions);
        if (result != Result.Success) {
            Plugin.Logger.LogError($"EOS_P2P_SetPacketQueueSize failed with {result}.");
            return;
        }

        Plugin.Logger.LogInfo($"Set EOS P2P packet queue size to incoming {IncomingPacketQueueMaxSizeBytes} bytes and outgoing {OutgoingPacketQueueMaxSizeBytes} bytes.");
    }
}
