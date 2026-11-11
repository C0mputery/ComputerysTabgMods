using System;
using System.Collections.Generic;
using System.IO;
using ComputeryLib.Utilities;
using ENet;
using Newtonsoft.Json;
using Landfall.Network;
using PlayFab;
using PlayFab.ClientModels;

namespace ComputeryLib.VisitorLog;

public static class VisitorLog {
    private static readonly string VisitorLogPath = Path.Combine(PersistantDataUtility.PersistentDataPath, "VisitorLog.json");
    private static Dictionary<string, VisitorInfo> _visitors = LoadVisitorLogFromFile();
    
    private static Dictionary<string, VisitorInfo> LoadVisitorLogFromFile() {
        if (!File.Exists(VisitorLogPath)) { return new Dictionary<string, VisitorInfo>(); }
        string json = File.ReadAllText(VisitorLogPath);
        try { return JsonConvert.DeserializeObject<Dictionary<string, VisitorInfo>>(json) ?? new Dictionary<string, VisitorInfo>(); }
        catch (JsonException) { return new Dictionary<string, VisitorInfo>(); }
    }

    private static void SaveVisitorLog() { File.WriteAllText(VisitorLogPath, JsonConvert.SerializeObject(_visitors, Formatting.Indented)); }
    
    private static void SyncFromFile() { _visitors = LoadVisitorLogFromFile(); }
    
    public static uint GetPermissionLevel(string epicUserName) {
        SyncFromFile();
        return _visitors.TryGetValue(epicUserName, out VisitorInfo visitorInfo) ? visitorInfo.PermissionLevel : 0;
    }

    public static void SetPermissionLevel(string epicUserName, uint level) {
        SyncFromFile();
        if (!_visitors.TryGetValue(epicUserName, out VisitorInfo visitorInfo)) { return; }
        visitorInfo.PermissionLevel = level;
        _visitors[epicUserName] = visitorInfo;
        SaveVisitorLog();
    }

    public static void Authenticate(Action onSuccess, Action onFailure) {
        PlayFabClientAPI.ForgetAllCredentials();
        LoginWithCustomIDRequest loginRequest = new LoginWithCustomIDRequest { CustomId = Guid.NewGuid().ToString(), CreateAccount = true };
        PlayFabClientAPI.LoginWithCustomID(loginRequest, loginResult => { onSuccess(); }, error => { onFailure(); });
    }
    
    public static void LogVisitor(TABGPlayerServer player) {
        DateTime currentTime = DateTime.UtcNow;
        
        if (!PlayFabClientAPI.IsClientLoggedIn()) {
            Authenticate(
                () => { TryToGetSteamFromPlayFab(player, currentTime); }, 
                () => { LogVisitor(player, "Unknown...", currentTime);
            });
            return;
        }
        
        TryToGetSteamFromPlayFab(player, currentTime);
    }

    private static void TryToGetSteamFromPlayFab(TABGPlayerServer player, DateTime currentTime) {
        GetLeaderboardAroundPlayerRequest getLeaderboardAroundPlayerRequest = new GetLeaderboardAroundPlayerRequest {
            StatisticName = "PlayerMatchLosses", // We are working under the assumption that this is the most common thing in the leaderboard to be populated.
            MaxResultsCount = 1,
            PlayFabId = player.PlayFabID,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowLinkedAccounts = true }
        };
        
        PlayFabClientAPI.GetLeaderboardAroundPlayer(getLeaderboardAroundPlayerRequest, result => {
            if (result.Leaderboard.Count > 0) {
                PlayerLeaderboardEntry entry = result.Leaderboard[0];
                foreach (LinkedPlatformAccountModel account in entry.Profile.LinkedAccounts) {
                    if (account.Platform != LoginIdentityProvider.Steam) { continue; }
                    string steamId = account.PlatformUserId;
                    LogVisitor(player, steamId, currentTime);
                    return;
                }
            }
            
            LogVisitor(player, "Unknown...", currentTime);
        }, error => {
            if (error.Error != PlayFabErrorCode.EntityTokenExpired && error.Error != PlayFabErrorCode.NotAuthenticated) {
                LogVisitor(player, "Unknown...", currentTime);
                return;
            }

            Authenticate(() => { TryToGetSteamFromPlayFab(player, currentTime); }, () => { LogVisitor(player, "Unknown...", currentTime); });
        });
    }

    private static void LogVisitor(TABGPlayerServer player, string steamId, DateTime currentTime) {
        SyncFromFile();
        if (!_visitors.TryGetValue(player.EpicUserName, out VisitorInfo visitorInfo)) {
            visitorInfo = new VisitorInfo { 
                FirstSeen = currentTime,
                LastSeen = currentTime
            };
        }

        DatedString.UpdateDatedStringSet(visitorInfo.DisplayNames, player.PlayerName, currentTime);
        DatedString.UpdateDatedStringSet(visitorInfo.SteamIds, steamId, currentTime);
        DatedString.UpdateDatedStringSet(visitorInfo.PlayfabIds, player.PlayFabID, currentTime);
        DatedString.UpdateDatedStringSet(visitorInfo.UnityIds, player.UnityID, currentTime);
        if (SearchUtility.TryGetPlayerIP(player, out string? ipAddress)) {
            DatedString.UpdateDatedStringSet(visitorInfo.IpAddresses, ipAddress!, currentTime);
        }
        visitorInfo.LastSeen = currentTime;
        
        _visitors[player.EpicUserName] = visitorInfo;
        SaveVisitorLog();
    }
}