using System.Collections.Generic;
using HarmonyLib;
using Landfall.Network;

namespace ComputeryLib.ServerFailFixes;

[HarmonyPatch]
public static class DeathDropUnstackFix {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DropAllLootCommand), nameof(DropAllLootCommand.Run), typeof(ServerClient), typeof(List<TABGPlayerServer>))]
    private static void RunPrefix(ServerClient world, List<TABGPlayerServer> players) {
        GameRoom room = world.GameRoomReference;
        foreach (TABGPlayerServer player in players) {
            List<TABGPlayerLootItem> loot = player.Loot;
            for (int i = loot.Count - 1; i >= 0; i--) {
                TABGPlayerLootItem item = loot[i];
                if (item.ItemCount <= 1) { continue; }
                Pickup pickup = room.GetItem(item.ItemIdentifier);
                if (pickup == null || InventoryUtility.IsStackable(pickup)) { continue; }
                
                int extraCopies = item.ItemCount - 1;
                item.Remove(extraCopies);
                for (int copy = 0; copy < extraCopies; copy++) { loot.Insert(i + 1, new TABGPlayerLootItem(item.ItemIdentifier, 1, item.ItemIndex)); }
            }
        }
    }
}
