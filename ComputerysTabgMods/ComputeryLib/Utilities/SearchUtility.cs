using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ENet;
using Landfall.Network;

namespace ComputeryLib.Utilities;

public static class SearchUtility {
    public static bool TryGetPlayerByNameOrID(string searchValue, TABGPlayerServer? sender, [NotNullWhen(true)] out TABGPlayerServer? foundPlayer) {
        foundPlayer = null;
        
        if (!WorldUtility.TryGetWorld(out ServerClient? world)) {
            PlayerInteractionUtility.PrivateMessageOrServerLog("The world is not available yet.", sender);
            return false;
        }
        
        GameRoom room = world.GameRoomReference;
        List<TABGPlayerServer> players = room.Players;
        List<TABGPlayerServer> exactMatches = players.FindAll(p => string.Equals(p.PlayerName, searchValue, StringComparison.OrdinalIgnoreCase));
        if (byte.TryParse(searchValue, out byte playerIndex) && room.FindPlayer(playerIndex) is { } indexMatch && !exactMatches.Contains(indexMatch)) { exactMatches.Add(indexMatch); }
        if (exactMatches.Count == 1) {
            foundPlayer = exactMatches[0];
            return true;
        }
        if (exactMatches.Count > 1) {
            PlayerInteractionUtility.PrivateMessageOrServerLog($"More than one player matches \"{searchValue}\". Use their player id instead.", sender);
            return false;
        }
        
        string[] playerNames = players.Select(p => p.PlayerName.ToLower()).ToArray();
        int closestMatchIndex = GetClosestMatch(searchValue.ToLower(), playerNames);
        if (closestMatchIndex != -1) {
            foundPlayer = players[closestMatchIndex];
            return true;
        }
        
        PlayerInteractionUtility.PrivateMessageOrServerLog($"Player not found: {searchValue}", sender);
        return false;
    }
    
    public static bool TryGetPlayerIP(TABGPlayerServer player, [NotNullWhen(true)] out string? ipAddress) {
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
        
        for (int i = 0; i < names.Length; i++) {
            string? gun = names[i];
            int distance = ComputeLevenshteinDistance(normalizedInput, gun.ToLower());
            if (distance >= minDistance) { continue; }
            minDistance = distance;
            bestMatchIndex = i;
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