using System;
using System.IO;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;

namespace ComputeryLib.ServerFailFixes;

public static class GhostLootFix {
    public static readonly bool RemoveBeforePickup = Plugin.Config.Bind("Network", "RemoveLootBeforePickup", true, "Removes items from the ground before the pickup gets accepted. Prevents ghost items if the client rejects the pickup for some reason. Will still void the item, but that would already happen!").Value;
    public static readonly float[] ResendDelays = Plugin.Config.Bind<float[]>("Network", "LootRemovalResendDelays", [30f, 60f, 600f], "Seconds after an item gets picked up to resend the pickup to every client. Used to further curb stomp ghost items in certain race conditions related to them spawning in.").Value;
    
    [HarmonyPatch]
    private static class RemoveBeforePickupPatch {
        [HarmonyPrepare] private static bool OnlyWhenEnabled() { return RemoveBeforePickup; }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RequestPickupCommand), nameof(RequestPickupCommand.PickUpGun))]
        private static void Prefix(GameRoom room, NetworkGun weapon, TABGPlayerServer player) {
            if (TrySendSoundPickup(room, weapon, player)) { SendRemoval(room.m_server, weapon.Index); }
        }
        
        // Early removal breaks the sound.
        // HACK HACK HACK
        private static bool TrySendSoundPickup(GameRoom room, NetworkGun weapon, TABGPlayerServer player) {
            TABGPlayerLootItem? held = player.Loot.Find(loot => loot.ItemCount > 0 && room.GetItem(loot.ItemIdentifier).weaponType is Pickup.WeaponType.Health or Pickup.WeaponType.Ammo);
            if (held == null) { return false; }
            int itemId = held.ItemIdentifier;
            
            Vector3 position = weapon.Position;
            byte[] drop = new byte[24];
            using (MemoryStream dropStream = new MemoryStream(drop))
            using (BinaryWriter dropWriter = new BinaryWriter(dropStream)) {
                dropWriter.Write(weapon.Index);
                dropWriter.Write(itemId);
                dropWriter.Write(0);
                dropWriter.Write(position.x);
                dropWriter.Write(position.y);
                dropWriter.Write(position.z);
            }
            
            byte[] accept = new byte[14];
            using (MemoryStream acceptStream = new MemoryStream(accept))
            using (BinaryWriter acceptWriter = new BinaryWriter(acceptStream)) {
                acceptWriter.Write(player.PlayerIndex);
                acceptWriter.Write(weapon.Index);
                acceptWriter.Write(itemId);
                acceptWriter.Write(0);
                acceptWriter.Write((byte)Pickup.EquipSlots.None);
            }
            
            room.m_server.SendMessageToClients(EventCode.ItemDrop, drop, player.PlayerIndex, true);
            room.m_server.SendMessageToClients(EventCode.WeaponPickUpAccepted, accept, player.PlayerIndex, true);
            return true;
        }
        
        private static bool IsSafeToStackZero(Pickup pickup) {
            return pickup.weaponType is Pickup.WeaponType.Health or Pickup.WeaponType.Ammo;
        }
    }
    
    [HarmonyPatch]
    private static class ResendRemovalPatch {
        [HarmonyPrepare] private static bool OnlyWhenEnabled() { return ResendDelays.Length > 0; }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameRoom), nameof(GameRoom.RemoveWeapon))]
        private static void Prefix(GameRoom __instance, NetworkGun? weapon) {
            if (weapon == null) { return; }
            ScheduleResend(__instance, weapon.Index, 0, 0f);
        }
        
        private static void ScheduleResend(GameRoom room, int index, int step, float elapsed) {
            if (step >= ResendDelays.Length) { return; }
            
            float target = ResendDelays[step];
            room.m_server.WaitThenDoAction(Math.Max(0f, target - elapsed), () => {
                if (room.GetWeapon(index) != null) { return; }
                SendRemoval(room.m_server, index);
                ScheduleResend(room, index, step + 1, Math.Max(elapsed, target));
            });
        }
    }
    
    private static void SendRemoval(ServerClient world, int index) {
        world.SendMessageToClients(EventCode.LootRemovedFromMap, BitConverter.GetBytes(index), byte.MaxValue, true);
    }
}