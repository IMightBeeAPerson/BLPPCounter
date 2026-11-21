using System.Collections.Generic;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models.ScoreSaber;

/// <summary>
/// GET /api/v2/players/{id}/basic
/// </summary>
internal class ScoreSaberBasicPlayer : IModel
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("playerNameInGame")]
    public string PlayerNameInGame { get; set; } = null!;

    [JsonProperty("country")]
    public string Country { get; set; } = null!;

    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("avatar")]
    public string Avatar { get; set; } = null!;

    [JsonProperty("avatarVersion")]
    public long AvatarVersion { get; set; }

    [JsonProperty("permissions")]
    public long Permissions { get; set; }

    [JsonProperty("banned")]
    public bool Banned { get; set; }

    [JsonProperty("silenced")]
    public bool Silenced { get; set; }

    [JsonProperty("inactive")]
    public bool Inactive { get; set; }

    [JsonProperty("stats")]
    public ScoreSaberPlayerStats Stats { get; set; } = null!;
}

/// <summary>
/// GET /api/v2/players/{id}/profile
///
/// Contains the full player profile, global history, and aliases.
/// </summary>
internal class ScoreSaberPlayerProfile : IModel
{
    [JsonProperty("player")]
    public ScoreSaberPlayer Player { get; set; } = null!;

    [JsonProperty("history")]
    public List<ScoreSaberPlayerHistoryEntry> History { get; set; } = [];

    [JsonProperty("aliases")]
    public List<ScoreSaberPlayerAlias> Aliases { get; set; } = [];
}

/// <summary>
/// The full player object included in ScoreSaberPlayerProfile.
/// </summary>
internal class ScoreSaberPlayer : ScoreSaberBasicPlayer
{
    [JsonProperty("bio")]
    public string? Bio { get; set; }

    [JsonProperty("vanity")]
    public string? Vanity { get; set; }

    [JsonProperty("profileCustomization")]
    public ScoreSaberProfileCustomization ProfileCustomization { get; set; } = null!;

    /// <summary>
    /// Badge response fields are not relevant to the original v1 player
    /// endpoints and can evolve independently. Replace JToken with a
    /// concrete badge model if your mod needs to inspect badges.
    /// </summary>
    [JsonProperty("badges")]
    public List<JToken> Badges { get; set; } = [];

    /// <summary>
    /// Pinned scores contain nested score/leaderboard data.
    /// Replace JToken with a concrete model if the mod uses pinned scores.
    /// </summary>
    [JsonProperty("pinnedScores")]
    public List<JToken> PinnedScores { get; set; } = [];

    [JsonProperty("followers")]
    public long Followers { get; set; }

    [JsonProperty("following")]
    public long Following { get; set; }

    [JsonProperty("platformFriends")]
    public long PlatformFriends { get; set; }

    [JsonProperty("recentFollowers")]
    public List<ScoreSaberRecentPlayer> RecentFollowers { get; set; } = [];

    [JsonProperty("recentFollowing")]
    public List<ScoreSaberRecentPlayer> RecentFollowing { get; set; } = [];

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = null!;

    [JsonProperty("lastSeenAt")]
    public string LastSeenAt { get; set; } = null!;
}

/// <summary>
/// Current player statistics for the requested realm.
/// </summary>
internal class ScoreSaberPlayerStats : IModel
{
    [JsonProperty("realmId")]
    public long RealmId { get; set; }

    [JsonProperty("realmName")]
    public string RealmName { get; set; } = null!;

    [JsonProperty("rank")]
    public long Rank { get; set; }

    [JsonProperty("countryRank")]
    public long CountryRank { get; set; }

    [JsonProperty("rankChange")]
    public long? RankChange { get; set; }

    [JsonProperty("totalPP")]
    public float TotalPp { get; set; }

    [JsonProperty("plusOnePP")]
    public double? PlusOnePp { get; set; }

    // These are strings in the ScoreSaber v2 schema, rather than JSON numbers.
    [JsonProperty("totalScore")]
    public string TotalScore { get; set; } = null!;

    [JsonProperty("totalRankedScore")]
    public string TotalRankedScore { get; set; } = null!;

    [JsonProperty("totalPlayedLeaderboards")]
    public long TotalPlayedLeaderboards { get; set; }

    [JsonProperty("totalPlayedRankedLeaderboards")]
    public long TotalPlayedRankedLeaderboards { get; set; }

    [JsonProperty("totalSubmittedPlays")]
    public long TotalSubmittedPlays { get; set; }

    [JsonProperty("totalReplayViews")]
    public long TotalReplayViews { get; set; }

    [JsonProperty("averageAccuracy")]
    public float AverageAccuracy { get; set; }

    [JsonProperty("weightedAverageAccuracy")]
    public float WeightedAverageAccuracy { get; set; }

    [JsonProperty("completionAccuracy")]
    public float CompletionAccuracy { get; set; }

    [JsonProperty("device")]
    public ScoreSaberDevice? Device { get; set; }
}

/// <summary>
/// User-controlled player-profile presentation settings.
/// </summary>
internal class ScoreSaberProfileCustomization : IModel
{
    [JsonProperty("backgroundImage")]
    public string? BackgroundImage { get; set; }

    [JsonProperty("backgroundImageVersion")]
    public long? BackgroundImageVersion { get; set; }

    [JsonProperty("accentColor")]
    public string? AccentColor { get; set; }

    [JsonProperty("accentForegroundColor")]
    public string? AccentForegroundColor { get; set; }

    [JsonProperty("accentForegroundActiveColor")]
    public string? AccentForegroundActiveColor { get; set; }

    [JsonProperty("supporterNameColorEnabled")]
    public bool SupporterNameColorEnabled { get; set; }

    [JsonProperty("badgeOrder")]
    public List<long>? BadgeOrder { get; set; }

    [JsonProperty("badgeComments")]
    public Dictionary<string, string>? BadgeComments { get; set; }

    [JsonProperty("statOrder")]
    public List<string>? StatOrder { get; set; }

    [JsonProperty("enabledStatIds")]
    public List<string>? EnabledStatIds { get; set; }

    [JsonProperty("chartMetricIds")]
    public List<string>? ChartMetricIds { get; set; }

    [JsonProperty("sectionOrder")]
    public List<string>? SectionOrder { get; set; }
}

/// <summary>
/// One historical global-stat snapshot from the profile endpoint.
/// </summary>
internal class ScoreSaberPlayerHistoryEntry : IModel
{
    [JsonProperty("rank")]
    public long Rank { get; set; }

    [JsonProperty("totalPP")]
    public float TotalPp { get; set; }

    [JsonProperty("totalScore")]
    public string TotalScore { get; set; } = null!;

    [JsonProperty("totalRankedScore")]
    public string TotalRankedScore { get; set; } = null!;

    [JsonProperty("totalPlayedLeaderboards")]
    public long TotalPlayedLeaderboards { get; set; }

    [JsonProperty("totalPlayedRankedLeaderboards")]
    public long TotalPlayedRankedLeaderboards { get; set; }

    [JsonProperty("totalSubmittedPlays")]
    public long TotalSubmittedPlays { get; set; }

    [JsonProperty("totalReplayViews")]
    public long TotalReplayViews { get; set; }

    [JsonProperty("averageAccuracy")]
    public float AverageAccuracy { get; set; }

    [JsonProperty("weightedAverageAccuracy")]
    public float WeightedAverageAccuracy { get; set; }

    [JsonProperty("completionAccuracy")]
    public float CompletionAccuracy { get; set; }

    [JsonProperty("estimated")]
    public bool Estimated { get; set; }

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = null!;
}

internal class ScoreSaberPlayerAlias : IModel
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("alias")]
    public string Alias { get; set; } = null!;

    [JsonProperty("disabled")]
    public bool Disabled { get; set; }

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = null!;
}

/// <summary>
/// Used for recentFollowers and recentFollowing.
/// It is intentionally separate from ScoreSaberBasicPlayer because the
/// API does not include permissions on these compact player records.
/// </summary>
internal class ScoreSaberRecentPlayer : IModel
{
    [JsonProperty("id")]
    public string Id { get; set; } = null!;

    [JsonProperty("name")]
    public string Name { get; set; } = null!;

    [JsonProperty("playerNameInGame")]
    public string PlayerNameInGame { get; set; } = null!;

    [JsonProperty("country")]
    public string Country { get; set; } = null!;

    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("avatar")]
    public string Avatar { get; set; } = null!;

    [JsonProperty("avatarVersion")]
    public long AvatarVersion { get; set; }
}
