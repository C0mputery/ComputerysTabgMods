using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ComputeryLib.Visitors;

public struct DatedString {
    public string Value { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    
    public static void UpdateDatedStringSet(List<DatedString> set, string value, DateTime currentTime) {
        if (set.Count > 0) {
            int lastIndex = set.Count - 1;
            DatedString lastEntry = set[lastIndex];
            if (lastEntry.Value == value) {
                lastEntry.LastSeen = currentTime;
                set[lastIndex] = lastEntry;
                return;
            }
        }
        set.Add(new DatedString {
            Value = value,
            FirstSeen = currentTime,
            LastSeen = currentTime
        });
    }
}

public enum ModerationActionType { Kick, Ban }

public class ModerationAction {
    [JsonConverter(typeof(StringEnumConverter))]
    public ModerationActionType Type { get; set; }
    public string? Reason { get; set; }
    public string IssuedBy { get; set; } = "Server";
    public DateTime IssuedAt { get; set; }
    public DateTime? Expires { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class VisitorInfo {
    public List<DatedString> DisplayNames { get; set; } = [];
    public List<DatedString> SteamIds { get; set; } = [];
    public List<DatedString> PlayfabIds { get; set; } = [];
    public List<DatedString> UnityIds { get; set; } = [];
    public List<DatedString> IpAddresses { get; set; } = [];
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public uint PermissionLevel { get; set; }
    public List<ModerationAction> ModerationHistory { get; set; } = [];
}