using HarmonyLib;
using UnityEngine;

namespace LandfallPlzFixClient {
    [HarmonyPatch(typeof(Dropper))]
    public class DropperDesyncFixPatch {
        private static float _flightStartTime;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Dropper.StartFlying))]
        public static void StartFlyingPostfix() { _flightStartTime = Time.time; }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Dropper.ClientInit))]
        public static void ClientInitPostfix() { _flightStartTime = Time.time; }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Dropper.SyncProgress))]
        public static void SyncProgressPostfix(Dropper __instance, float newPlaneProgress) {
            _flightStartTime = Time.time - (newPlaneProgress * __instance.totalFlyingTime);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Dropper.LateUpdate))]
        public static void LateUpdatePostfix(Dropper __instance) {
            if (__instance.isFlying) {
                __instance.progress = (Time.time - _flightStartTime) / __instance.totalFlyingTime;
                Vector3 vector = __instance.startPos + __instance.pathToTravel * __instance.progress;
                __instance.dropperObject.position = vector;
            }
        }
    }
}