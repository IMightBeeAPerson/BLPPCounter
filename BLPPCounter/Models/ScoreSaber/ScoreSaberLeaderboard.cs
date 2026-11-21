using System;
using System.Collections.Generic;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Map_Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models.ScoreSaber;

/// <summary>
/// GET /api/v2/leaderboards/hash/{hash}/{mode}/{difficulty}
/// GET /api/v2/leaderboards/{id}
///
/// The v2 leaderboard-information response.
/// </summary>
internal class ScoreSaberLeaderboard : ModelId<long>, ICacheModel
{
    [JsonProperty("id")]
    public override long Id { get; set; }

    [JsonProperty("map")]
    public ScoreSaberMap Map { get; set; } = null!;

    [JsonProperty("difficulty")]
    public ScoreSaberLeaderboardDifficulty Difficulty { get; set; } = null!;

    [JsonProperty("maxScore")]
    public int MaxScore { get; set; }

    [JsonProperty("totalScores")]
    public int TotalScores { get; set; }

    [JsonProperty("dailyScores")]
    public int DailyScores { get; set; }

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = null!;

    [JsonProperty("realm")]
    public ScoreSaberRealm Realm { get; set; } = null!;

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }

    public string LeaderboardId => Map.BsId ?? Id.ToString();

    public string Mode => Difficulty.GameMode.Replace("Solo", "", System.StringComparison.OrdinalIgnoreCase);

    public string Hash => Map.Hash;

    BeatmapDifficulty ICacheModel.Difficulty => Difficulty.Difficulty;

    public object Clone()
    {
        ScoreSaberLeaderboard clone = new()
        {
            Id = Id,
            Map = (ScoreSaberMap)Map.Clone(),
            Difficulty = (ScoreSaberLeaderboardDifficulty)Difficulty.Clone(),
            MaxScore = MaxScore,
            TotalScores = TotalScores,
            DailyScores = DailyScores,
            CreatedAt = CreatedAt,
        };

        return clone;
    }
}

internal class ScoreSaberMap : ModelId<long>, ICloneable
{
    [JsonProperty("id")]
    public override long Id { get; set; }

    [JsonProperty("hash")]
    public string Hash { get; set; } = null!;

    [JsonProperty("bsid")]
    public string? BsId { get; set; }

    [JsonProperty("songName")]
    public string SongName { get; set; } = null!;

    [JsonProperty("songSubName")]
    public string SongSubName { get; set; } = null!;

    [JsonProperty("songAuthorName")]
    public string SongAuthorName { get; set; } = null!;

    [JsonProperty("levelAuthorName")]
    public string LevelAuthorName { get; set; } = null!;

    [JsonProperty("bpm")]
    public int bpm { get; set; } = 1;

    [JsonProperty("coverUrl")]
    public string CoverUrl { get; set; } = null!;

    [JsonProperty("verified")]
    public bool Verified { get; set; } = true;

    public object Clone()
    {
        return MemberwiseClone();
    }
}

internal class ScoreSaberLeaderboardDifficulty : ModelId<long>, ICloneable
{
    [JsonProperty("id")]
    public override long Id { get; set; }

    [JsonProperty("difficulty")]
    public int DiffNum
    {
        get => _diffNum;
        set
        {
            _diffNum = value;
            Difficulty = Map.FromValue(value);
        }
    }

    [JsonProperty("gameMode")]
    public string GameMode { get; set; } = null!;

    [JsonProperty("difficultyRaw")]
    public string? RawDifficulty { get; set; }

    [JsonIgnore]
    public BeatmapDifficulty Difficulty { get; private set; }

    private int _diffNum;

    public object Clone()
    {
        return MemberwiseClone();
    }
}
