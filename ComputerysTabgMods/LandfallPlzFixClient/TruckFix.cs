using HarmonyLib;
using UnityEngine;

namespace LandfallPlzFixClient;

[HarmonyPatch(typeof(WormSegment))]
public class WormSegmentPatch {
    [HarmonyPrefix]
    [HarmonyPatch(nameof(WormSegment.Start))]
    public static void StartPrefix(WormSegment __instance) {
        Rigidbody rigidbody = __instance.GetComponent<Rigidbody>();
        rigidbody.interpolation = RigidbodyInterpolation.None;
    }
}