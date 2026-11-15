using System.Collections.Generic;
using System.Linq;
using Landfall.Network;

namespace ComputeryLib.Helpers;

public static class RoomHelper {
    public static TABGPlayerServer? FindPlayerOrSpectator(this GameRoom room, byte playerIndex) { return room.FindPlayer(playerIndex) ?? room.FindSpectator(playerIndex); }
    
    public static IEnumerable<TABGPlayerServer> PlayersAndSpectators(this GameRoom room) { return room.Players.Union(room.Spectators); }
}