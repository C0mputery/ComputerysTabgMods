using System;
using System.Collections.Generic;
using System.Linq;
using ENet;
using Landfall.Network;

namespace ComputeryLib.Utilities;

public static class SearchUtility {
    public static bool TryGetPlayerByNameOrID(string searchValue, out TABGPlayerServer? foundPlayer) {
        List<TABGPlayerServer> players = WorldUtility.GetWorld().GameRoomReference.Players;
        foreach (TABGPlayerServer player in players) {
            if (player.PlayerName.ToLower() != searchValue.ToLower() && player.PlayerIndex.ToString() != searchValue && player.EpicUserName != searchValue) { continue; }
            foundPlayer = player;
            return true;
        }
        
        string[] playerNames = players.Select(p => p.PlayerName.ToLower()).ToArray();
        int closestMatchIndex = GetClosestMatch(searchValue.ToLower(), playerNames);
        if (closestMatchIndex != -1) {
            foundPlayer = players[closestMatchIndex];
            return true;
        }
        
        foundPlayer = null;
        return false;
    }
    
    public static bool TryGetPlayerIP(TABGPlayerServer player, out string? ipAddress) {
        if (ServerClient.m_Server is EnetServer server) {
            if (server.m_IndexToENetPeerDic.TryGetValue(player.PlayerIndex, out Peer enetPeer)) {
                ipAddress = enetPeer.IP;
                return true;
            }
        }
        ipAddress = null;
        return false;
    }
    
    public static int GetClosestMatch(string userInput, string[] names) {
        if (names.Length == 0) { return -1; }
        
        int bestMatchIndex = -1;
        
        int minDistance = int.MaxValue;
        
        string normalizedInput = userInput.ToLower().Trim();
        
        for (int index = 0; index < names.Length; index++) {
            string? gun = names[index];
            int distance = ComputeLevenshteinDistance(normalizedInput, gun.ToLower());
            if (distance >= minDistance) { continue; }
            minDistance = distance;
            bestMatchIndex = index;
        }
        
        if (minDistance > normalizedInput.Length / 2) { return -1; }
        
        return bestMatchIndex;
    }
    
    private static int ComputeLevenshteinDistance(string s, string t) {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        
        if (n == 0) { return m; }
        if (m == 0) { return n; }
        
        for (int i = 0; i <= n; i++) { d[i, 0] = i; }
        for (int j = 0; j <= m; j++) { d[0, j] = j; }
        
        for (int i = 1; i <= n; i++) {
            for (int j = 1; j <= m; j++) {
                int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}