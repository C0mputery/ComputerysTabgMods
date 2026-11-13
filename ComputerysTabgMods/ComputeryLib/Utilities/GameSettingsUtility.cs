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
public static class GameSettingsUtility {
    [HarmonyPrefix]
    [HarmonyPatch(MethodType.Constructor, typeof(TheRing), typeof(string))]
    public static bool GameSettingsConstructorPrefix(ref GameSettings __instance, TheRing ringRef, string overridePath) {
        SetDefaultValues(ref __instance, ringRef);
        LoadFromConfig(ref __instance, ringRef);
        return false;
    }
    
    private static readonly ConfigFile LandfallConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, "Landfall.cfg"), true);
    
    // Server
    public static readonly bool Relay = LandfallConfig.Bind("Server", "Relay", true, "Use the Unity Relay or not").Value; // make this enabled by default just for the sake of ease of use
    public static readonly ushort Port = LandfallConfig.Bind("Server", "Port", (ushort)7777, "Port for the server to use if the relay is not enabled").Value;
    public static readonly string Password = LandfallConfig.Bind("Server", "Password", "", "Password, leave blank for no password").Value;
    
    // Server List
    public static readonly string ServerName = LandfallConfig.Bind("Server List", "ServerName", "Default Server Name", "Name of the server. Allowed word list: https://github.com/landfallgames/tabg-word-list").Value;
    public static readonly string ServerDescription = LandfallConfig.Bind("Server List", "ServerDescription", "Description for server /n for new line", "Server Description").Value;
    
    // Gameplay
    public static readonly MatchMode MatchMode = (MatchMode)Enum.Parse(typeof(MatchMode), LandfallConfig.Bind("Gameplay", "TeamMode", "SQUAD", "Sets the MatchMode. Valid inputs: SQUAD, DUO, SOLO").Value);
    public static readonly int MaxPlayers = Mathf.Clamp(LandfallConfig.Bind("Gameplay", "MaxPlayers", 70, "Max players on server. Max being 253").Value, 1, 253);
    public static readonly float ForceStartTime = LandfallConfig.Bind("Gameplay", "ForceStartTime", 200f, "Seconds until force start the countdown").Value;
    public static readonly float Countdown = LandfallConfig.Bind("Gameplay", "Countdown", 20f, "Seconds it takes to start the game after Players have joined or force start triggered").Value;
    public static readonly int PlayersBeforeStarting = LandfallConfig.Bind("Gameplay", "PlayersToStart", 2, "Players to start countdown").Value;
    public static readonly bool AutoTeam = LandfallConfig.Bind("Gameplay", "AutoTeam", false, "Forces auto teaming for all players").Value;
    public static readonly int AutoTeamNumberOfTeams = LandfallConfig.Bind("Gameplay", "AutoTeamNumberOfTeams", 2, "Number of teams to force players into when force auto teaming is enabled").Value;
    public static readonly bool UseSouls = LandfallConfig.Bind("Gameplay", "UseSouls", true, "If a player should drop a soul when they run the DropAllLootCommand on death").Value;
    public static readonly GameMode GameMode = (GameMode)Enum.Parse(typeof(GameMode), LandfallConfig.Bind("Gameplay", "GameMode", "BattleRoyale", "Sets the GameMode. Valid inputs: BattleRoyale, Brawl, Test, Bomb, Deception. Most of these do not work.").Value);
    public static readonly bool AllowRejoins = LandfallConfig.Bind("Gameplay", "AllowRejoins", false, "Tries to allow rejoins, does not really work. Using this will break mid-match joining as well.").Value;
    public static readonly bool UseTimedForceStart = LandfallConfig.Bind("Gameplay", "UseTimedForceStart", true, "Will start match with fewer players than PlayersToStart if waited longer than ForceStartTime").Value;
    public static readonly int MinPlayersToForceStart = LandfallConfig.Bind("Gameplay", "MinPlayersToForceStart", 2, "Players needed to start the force start timer").Value;
    public static readonly int SpawnBots = LandfallConfig.Bind("Gameplay", "SpawnBots", 0, "Tries to spawn this number of bots into the game, does not work properly").Value;
    
    // Battle Royale
    public static readonly bool AllowSpectating = LandfallConfig.Bind("Battle Royale", "AllowSpectating", true, "Allows spectating in battle royale").Value;
    public static readonly float StripLootByPercentage = LandfallConfig.Bind("Battle Royale", "StripLootByPercentage", 0.1f, "0.0 - 1.0 percentage chance for loot spawn points to spawn loot. 0 being 0% and 1.0 being 100%").Value;
    public static readonly float CarSpawnRate = LandfallConfig.Bind("Battle Royale", "CarSpawnRate", 1f, "0.0 - 1.0 percentage chance for vehical spawn points to spawn a vehical. 0 being 0% and 1.0 being 100%").Value;
    public static readonly bool AllowRespawnMinigame = LandfallConfig.Bind("Battle Royale", "AllowRespawnMinigame", true, "Enable or disable the respawn minigame in battle royale").Value;
    public static readonly bool Use_Ring = LandfallConfig.Bind("Battle Royale", "Use_Ring", true, "If to use the ring in battle royale").Value;
    public static readonly float TimeBeforeFirstRing = LandfallConfig.Bind("Battle Royale", "TimeBeforeFirstRing", 70f, "Time before first ring").Value;
    
    // Brawl
    public static readonly float TimeUntilWeaponsDissapear = LandfallConfig.Bind("Brawl", "TimeUntilWeaponsDissapear", 10f, "Time until thrown grenades get removed from the map in brawl. Why is this even a setting???").Value;
    public static readonly int GroupsBeforeStarting = LandfallConfig.Bind("Brawl", "GroupsBeforeStarting", int.MaxValue, $"Groups before a brawl game starts. If set too {int.MaxValue} in solos it will be set to 10 and in squads it will be set to 5").Value;
    public static readonly int MaxNumberOfTeams = LandfallConfig.Bind("Brawl", "MaxNumberOfTeams", int.MaxValue, "Maximum teams in brawl").Value;
    public static readonly int NumberOfLivesPerTeam = LandfallConfig.Bind("Brawl", "NumberOfLivesPerTeam", 5, "Lives per team in brawl").Value;
    public static readonly ushort KillsToWin = LandfallConfig.Bind("Brawl", "KillsToWin", ushort.MaxValue, $"Kills to win in brawl. If set to {ushort.MaxValue} it will be 20 for solos and 30 for squads").Value;
    
    // Bomb
    public static readonly int RoundsToWin = LandfallConfig.Bind("Bomb", "RoundsToWin", 13, "Rounds to win in bomb").Value;
    public static readonly float BombTime = LandfallConfig.Bind("Bomb", "BombTime", 30f, "Bomb detonation time").Value;
    public static readonly float BombDefuseTime = LandfallConfig.Bind("Bomb", "BombDefuseTime", 5f, "Bomb defuse time").Value;
    public static readonly int RoundTime = LandfallConfig.Bind("Bomb", "RoundTime", 90, "Bomb round time").Value;
    
    // Misc
    public static readonly bool DEBUG_DEATHMATCH = LandfallConfig.Bind("Misc", "DEBUG_DEATHMATCH", false, "Forces automatic respawns").Value;
    
    // Security
    public static readonly bool UseAntiCheat = LandfallConfig.Bind("Security", "UseAntiCheat", false, "I do not think this functions properly on the community servers").Value;
    public static readonly bool UseAntiCheatLogging = LandfallConfig.Bind("Security", "UseAntiCheatLogging", false, "Tries to Log a bunch of stuff to the anti-cheat, also likely does not work on community servers").Value;
    public static readonly bool UseAntiCheatDebugLogging = LandfallConfig.Bind("Security", "UseAntiCheatDebugLogging", false, "Tries to Log a bunch of stuff to the anti-cheat, also likely does not work on community servers").Value;
    public static readonly bool UseKicks = LandfallConfig.Bind("Security", "UseKicks", true, "Enables or disables the PlayerKickCommand from being able to kick players. This method mostly kicks players who do things that are clearly not possible on a unmodified client").Value;
    
    // Unused
    public static readonly string[] Admins = LandfallConfig.Bind("Unused", "Admins", "", "Entirely unused").Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
    public static readonly bool LANServer = LandfallConfig.Bind("Unused", "LANServer", false, "Entirely unused").Value;
    public static readonly bool UsePlayFabStats = LandfallConfig.Bind("Unused", "UsePlayFabStats", false, "Entirely unused").Value;
    public static readonly string ServerBrowserIP = LandfallConfig.Bind("Unused", "ServerBrowserIP", "20.234.109.246", "Part of an old unused server list system. Was the IP for the server list API.").Value;
    
    private static void LoadFromConfig(ref GameSettings __instance, TheRing ringRef) {
        __instance.TimeOfDay = Random.Range(0.0f, 1f);
        __instance.RingSizes = ringRef.ringSizes;
        __instance.RingSpeeds = ringRef.ringSpeeds;
        __instance.BaseRingTime = ringRef.RingBaseTime;
        
        // Server
        __instance.Relay = Relay;
        __instance.Port = Port;
        __instance.Password = Password;
        
        // Server List
        __instance.ServerName = ServerName;
        __instance.ServerDescription = ServerDescription;
        
        // Gameplay
        __instance.MatchMode = MatchMode;
        __instance.MaxPlayers = MaxPlayers;
        __instance.ForceStartTime = ForceStartTime;
        __instance.Countdown = Countdown;
        __instance.PlayersBeforeStarting = PlayersBeforeStarting;
        __instance.AutoTeam = AutoTeam;
        __instance.AutoTeamNumberOfTeams = AutoTeamNumberOfTeams;
        __instance.UseSouls = UseSouls;
        __instance.GameMode = GameMode;
        __instance.AllowRejoins = AllowRejoins;
        __instance.UseTimedForceStart = UseTimedForceStart;
        __instance.MinPlayersToForceStart = MinPlayersToForceStart;
        __instance.SpawnBots = SpawnBots;
        
        // Battle Royale
        __instance.AllowSpectating = AllowSpectating;
        __instance.StripLootByPercentage = StripLootByPercentage;
        __instance.CarSpawnRate = CarSpawnRate;
        __instance.AllowRespawnMinigame = AllowRespawnMinigame;
        __instance.Use_Ring = Use_Ring;
        __instance.TimeBeforeFirstRing = TimeBeforeFirstRing;
        
        // Brawl
        __instance.TimeUntilWeaponsDissapear = TimeUntilWeaponsDissapear;
        __instance.GroupsBeforeStarting = GroupsBeforeStarting;
        __instance.MaxNumberOfTeams = MaxNumberOfTeams;
        __instance.NumberOfLivesPerTeam = NumberOfLivesPerTeam;
        __instance.KillsToWin = KillsToWin;
        
        // Bomb
        __instance.RoundsToWin = RoundsToWin;
        __instance.BombTime = BombTime;
        __instance.BombDefuseTime = BombDefuseTime;
        __instance.RoundTime = RoundTime;
        
        // Misc
        __instance.DEBUG_DEATHMATCH = DEBUG_DEATHMATCH;
        
        // Security
        __instance.UseAntiCheat = UseAntiCheat;
        __instance.UseAntiCheatLogging = UseAntiCheatLogging;
        __instance.UseAntiCheatDebugLogging = UseAntiCheatDebugLogging;
        __instance.UseKicks = UseKicks;
        
        // Unused
        __instance.Admins = Admins;
        __instance.LANServer = LANServer;
        __instance.UsePlayFabStats = UsePlayFabStats;
        __instance.ServerBrowserIP = ServerBrowserIP;
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
