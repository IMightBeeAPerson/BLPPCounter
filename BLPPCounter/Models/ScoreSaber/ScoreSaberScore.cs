using System.Collections.Generic;
using System.Runtime.Serialization;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models.ScoreSaber;

/// <summary>
/// A score returned from a v2 leaderboard or player-score endpoint.
/// </summary>
internal class ScoreSaberScore : IScoreModel
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("rank")]
    public int Rank { get; set; }

    [JsonProperty("unmodifiedScore")]
    public int UnmodifiedScore { get; set; }

    [JsonProperty("modifiedScore")]
    public int ModifiedScore { get; set; }

    /// <summary>
    /// Accuracy is returned as a percentage, for example 97.35.
    /// </summary>
    [JsonProperty("accuracy")]
    public float Accuracy { get; set; }

    [JsonProperty("pp")]
    public float Pp { get; set; }

    [JsonProperty("weight")]
    public float Weight { get; set; }

    [JsonProperty("mods")]
    public List<string> Mods { get; set; } = [];

    [JsonProperty("badCuts")]
    public int BadCuts { get; set; }

    [JsonProperty("missedNotes")]
    public int MissedNotes { get; set; }

    [JsonProperty("maxCombo")]
    public int MaxCombo { get; set; }

    [JsonProperty("fullCombo")]
    public bool FullCombo { get; set; }

    [JsonProperty("hasReplay")]
    public bool HasReplay { get; set; }

    [JsonProperty("replayViewCount")]
    public long? ReplayViewCount { get; set; }

    [JsonProperty("personalBest")]
    public bool PersonalBest { get; set; }

    [JsonProperty("legacyHmdId")]
    public int? LegacyHmdId { get; set; }

    [JsonProperty("version")]
    public string? Version { get; set; }

    [JsonProperty("playOutcome")]
    public string? PlayOutcome { get; set; }

    [JsonProperty("playOutcomeTime")]
    public double? PlayOutcomeTime { get; set; }

    [JsonProperty("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonProperty("hasHistory")]
    public bool? HasHistory { get; set; }

    [JsonProperty("player")]
    public ScoreSaberScorePlayer? Player { get; set; }

    [JsonProperty("device")]
    public ScoreSaberDevice? Device { get; set; }

    [JsonIgnore]
    public int MaxScore { get; set; }

    [JsonIgnore]
    public string? MapId { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }

    public float LeaderboardPoints => Pp;

    public string PlayerName => Player?.Name ?? "";

    [OnDeserialized]
    public void OnDeserialized(StreamingContext context)
    {
        MaxScore = (int)UnityEngine.Mathf.Round(ModifiedScore / Accuracy);
    }
}

internal class ScoreSaberPlayerScore : IModel
{
    [JsonProperty("score")]
    public ScoreSaberScore Score { get; set; } = null!;

    [JsonProperty("leaderboard")]
    public ScoreSaberLeaderboard Leaderboard { get; set; } = null!;
}

internal class ScoreSaberScorePlayer : IModel
{
    // ScoreSaber player IDs are strings; do not deserialize them into long.
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

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}

internal class ScoreSaberDevice : IModel
{
    [JsonProperty("hmd")]
    public string? Hmd { get; set; }

    [JsonProperty("controllerLeft")]
    public string? ControllerLeft { get; set; }

    [JsonProperty("controllerRight")]
    public string? ControllerRight { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
