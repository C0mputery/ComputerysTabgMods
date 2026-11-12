using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Landfall.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComputeryLib.ConfigImprovements;

[HarmonyPatch(typeof(GameSettings))]
public static class GameSettingsPatch {
    [HarmonyPrefix]
    [HarmonyPatch(MethodType.Constructor, typeof(TheRing), typeof(string))]
    public static bool GameSettingsConstructorPrefix(ref GameSettings __instance, TheRing ringRef, string overridePath) {
        SetDefaultValues(ref __instance, ringRef);
        LoadFromConfig(ref __instance, ringRef);
        return false;
    }
    
    private static void LoadFromConfig(ref GameSettings __instance, TheRing ringRef) {
        ConfigFile configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "Landfall.cfg"), true);
        
        __instance.TimeOfDay = Random.Range(0.0f, 1f);
        __instance.RingSizes = ringRef.ringSizes;
        __instance.RingSpeeds = ringRef.ringSpeeds;
        __instance.BaseRingTime = ringRef.RingBaseTime;
        
        // Server
        __instance.Relay = configFile.Bind("Server", "Relay", true, "Use the Unity Relay or not").Value; // make this enabled by default just for the sake of ease of use
        __instance.Port = configFile.Bind("Server", "Port", (ushort)7777, "Port for the server to use if the relay is not enabled").Value;
        __instance.Password = configFile.Bind("Server", "Password", "", "Password, leave blank for no password").Value;
        
        // Server List
        __instance.ServerName = configFile.Bind("Server List", "ServerName", "Default Server Name", "Name of the server. Allowed word list: https://github.com/landfallgames/tabg-word-list").Value;
        __instance.ServerDescription = configFile.Bind("Server List", "ServerDescription", "Description for server /n for new line", "Server Description").Value;
        
        // Gameplay
        __instance.MatchMode = (MatchMode)Enum.Parse(typeof(MatchMode), configFile.Bind("Gameplay", "TeamMode", "SQUAD", "Sets the MatchMode. Valid inputs: SQUAD, DUO, SOLO").Value);
        __instance.MaxPlayers = Mathf.Clamp(configFile.Bind("Gameplay", "MaxPlayers", 70, "Max players on server. Max being 253").Value, 1, 253);
        __instance.ForceStartTime = configFile.Bind("Gameplay", "ForceStartTime", 200f, "Seconds until force start the countdown").Value;
        __instance.Countdown = configFile.Bind("Gameplay", "Countdown", 20f, "Seconds it takes to start the game after Players have joined or force start triggered").Value;
        __instance.PlayersBeforeStarting = configFile.Bind("Gameplay", "PlayersToStart", 2, "Players to start countdown").Value;
        __instance.AutoTeam = configFile.Bind("Gameplay", "AutoTeam", false, "Forces auto teaming for all players").Value;
        __instance.AutoTeamNumberOfTeams = configFile.Bind("Gameplay", "AutoTeamNumberOfTeams", 2, "Number of teams to force players into when force auto teaming is enabled").Value;
        __instance.UseSouls = configFile.Bind("Gameplay", "UseSouls", true, "If a player should drop a soul when they run the DropAllLootCommand on death").Value;
        __instance.GameMode = (GameMode)Enum.Parse(typeof(GameMode), configFile.Bind("Gameplay", "GameMode", "BattleRoyale", "Sets the GameMode. Valid inputs: BattleRoyale, Brawl, Test, Bomb, Deception. Most of these do not work.").Value);
        __instance.AllowRejoins = configFile.Bind("Gameplay", "AllowRejoins", false, "Tries to allow rejoins, does not really work. Using this will break mid-match joining as well.").Value;
        __instance.UseTimedForceStart = configFile.Bind("Gameplay", "UseTimedForceStart", true, "Will start match with fewer players than PlayersToStart if waited longer than ForceStartTime").Value;
        __instance.MinPlayersToForceStart = configFile.Bind("Gameplay", "MinPlayersToForceStart", 2, "Players needed to start the force start timer").Value;
        __instance.SpawnBots = configFile.Bind("Gameplay", "SpawnBots", 0, "Tries to spawn this number of bots into the game, does not work properly").Value;
        
        // Battle Royale
        __instance.AllowSpectating = configFile.Bind("Battle Royale", "AllowSpectating", true, "Allows spectating in battle royale").Value;
        __instance.StripLootByPercentage = configFile.Bind("Battle Royale", "StripLootByPercentage", 0.1f, "0.0 - 1.0 percentage chance for loot spawn points to spawn loot. 0 being 0% and 1.0 being 100%").Value;
        __instance.CarSpawnRate = configFile.Bind("Battle Royale", "CarSpawnRate", 1f, "0.0 - 1.0 percentage chance for vehical spawn points to spawn a vehical. 0 being 0% and 1.0 being 100%").Value;
        __instance.AllowRespawnMinigame = configFile.Bind("Battle Royale", "AllowRespawnMinigame", true, "Enable or disable the respawn minigame in battle royale").Value;
        __instance.Use_Ring = configFile.Bind("Battle Royale", "Use_Ring", true, "If to use the ring in battle royale").Value;
        __instance.TimeBeforeFirstRing = configFile.Bind("Battle Royale", "TimeBeforeFirstRing", 70f, "Time before first ring").Value;
        
        // Brawl
        __instance.TimeUntilWeaponsDissapear = configFile.Bind("Brawl", "TimeUntilWeaponsDissapear", 10f, "Time until thrown grenades get removed from the map in brawl. Why is this even a setting???").Value;
        __instance.GroupsBeforeStarting = configFile.Bind("Brawl", "GroupsBeforeStarting", int.MaxValue, $"Groups before a brawl game starts. If set too {int.MaxValue} in solos it will be set to 10 and in squads it will be set to 5").Value;
        __instance.MaxNumberOfTeams = configFile.Bind("Brawl", "MaxNumberOfTeams", int.MaxValue, "Maximum teams in brawl").Value;
        __instance.NumberOfLivesPerTeam = configFile.Bind("Brawl", "NumberOfLivesPerTeam", 5, "Lives per team in brawl").Value;
        __instance.KillsToWin = configFile.Bind("Brawl", "KillsToWin", ushort.MaxValue, $"Kills to win in brawl. If set to {ushort.MaxValue} it will be 20 for solos and 30 for squads").Value;
        
        // Bomb
        __instance.RoundsToWin = configFile.Bind("Bomb", "RoundsToWin", 13, "Rounds to win in bomb").Value;
        __instance.BombTime = configFile.Bind("Bomb", "BombTime", 30f, "Bomb detonation time").Value;
        __instance.BombDefuseTime = configFile.Bind("Bomb", "BombDefuseTime", 5f, "Bomb defuse time").Value;
        __instance.RoundTime = configFile.Bind("Bomb", "RoundTime", 90, "Bomb round time").Value;
        
        // Misc
        __instance.DEBUG_DEATHMATCH = configFile.Bind("Misc", "DEBUG_DEATHMATCH", false, "Forces automatic respawns").Value;
        
        // Security
        __instance.UseAntiCheat = configFile.Bind("Security", "UseAntiCheat", false, "I do not think this functions properly on the community servers").Value;
        __instance.UseAntiCheatLogging = configFile.Bind("Security", "UseAntiCheatLogging", false, "Tries to Log a bunch of stuff to the anti-cheat, also likely does not work on community servers").Value;
        __instance.UseAntiCheatDebugLogging = configFile.Bind("Security", "UseAntiCheatDebugLogging", false, "Tries to Log a bunch of stuff to the anti-cheat, also likely does not work on community servers").Value;
        __instance.UseKicks = configFile.Bind("Security", "UseKicks", true, "Enables or disables the PlayerKickCommand from being able to kick players. This method mostly kicks players who do things that are clearly not possible on a unmodified client").Value;
        
        // Unused
        __instance.Admins = configFile.Bind("Unused", "Admins", "", "Entirely unused").Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        __instance.LANServer = configFile.Bind("Unused", "LANServer", false, "Entirely unused").Value;
        __instance.UsePlayFabStats = configFile.Bind("Unused", "UsePlayFabStats", false, "Entirely unused").Value;
        __instance.ServerBrowserIP = configFile.Bind("Unused", "ServerBrowserIP", "20.234.109.246", "Part of an old unused server list system. Was the IP for the server list API.").Value;
    }
    
    private static void SetDefaultValues(ref GameSettings __instance, TheRing ringRef) {
        __instance.Port = 7777;
        __instance.Password = "";
        __instance.Admins = [];
        __instance.ServerBrowserIP = "20.234.109.246";
        __instance.ServerName = "UNNAMED SERVER";
        __instance.ServerDescription = "";
        __instance.TimeOfDay = Random.Range(0.0f, 1f);
        __instance.MatchMode = MatchMode.SQUAD;
        __instance.MaxPlayers = 43;
        __instance.Countdown = 30f;
        __instance.RespawnTime = 15f;
        __instance.PlayersBeforeStarting = 30;
        __instance.GroupsBeforeStarting = int.MaxValue;
        __instance.RingSizes = ringRef.ringSizes;
        __instance.RingSpeeds = ringRef.ringSpeeds;
        __instance.BaseRingTime = ringRef.RingBaseTime;
        __instance.TimeBeforeFirstRing = 70f;
        __instance.MaxNumberOfTeams = int.MaxValue;
        __instance.NumberOfLivesPerTeam = 5;
        __instance.StripLootByPercentage = 0.1f;
        __instance.UseAntiCheat = true;
        __instance.UseAntiCheatLogging = false;
        __instance.UseAntiCheatDebugLogging = false;
        __instance.UseKicks = true;
        __instance.AutoTeam = false;
        __instance.AllowSpectating = true;
        __instance.GameMode = GameMode.BattleRoyale;
        __instance.Respawns = false;
        __instance.AutoTeamNumberOfTeams = 2;
        __instance.UseSouls = true;
        __instance.AllowRejoins = false;
        __instance.AllowRespawnMinigame = false;
        __instance.UsePlayFabStats = false;
        __instance.UseTimedForceStart = true;
        __instance.ForceStartTime = 10f;
        __instance.MinPlayersToForceStart = 5;
        __instance.CarSpawnRate = 1f;
        __instance.MaxTeamSize = 4;
        __instance.KillsToWin = ushort.MaxValue;
        __instance.TimeUntilWeaponsDissapear = 10f;
        __instance.SpawnBots = 0;
        __instance.DEBUG_DEATHMATCH = false;
        __instance.Use_Ring = true;
        __instance.RoundsToWin = 13;
        __instance.BombTime = 30f;
        __instance.BombDefuseTime = 5f;
        __instance.RoundTime = 90;
        __instance.LANServer = false;
        __instance.Relay = true;
    }
}