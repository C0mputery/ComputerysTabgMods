using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.Utilities;

[HarmonyPatch]
public static class PickupDistanceUtility {
    public static readonly bool DisablePickupDistanceCheck = Plugin.Config.Bind("Gameplay", "DisablePickupDistanceCheck", true, "Disables the server-side distance check when players pick up items. This defaults to on because this leads to ghost items a lot, Also, who the fuck is cheating in this way? Also, it can happen very easily if you're picking up something like a heavy barrel").Value;
    
    [HarmonyPrepare] private static bool OnlyWhenEnabled() { return DisablePickupDistanceCheck; }
    
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RequestPickupCommand), "CanPickUpGun")]
    private static bool CanPickUpGunPrefix(ref bool __result) {
        __result = true;
        return false;
    }
}
