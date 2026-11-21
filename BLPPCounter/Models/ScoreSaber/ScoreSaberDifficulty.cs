using System.Collections.Generic;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Map_Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models.ScoreSaber;

/// <summary>
/// GET /api/v2/leaderboards/hash/{hash}
///
/// Gets every ScoreSaber leaderboard difficulty associated with a map hash.
/// </summary>
internal class ScoreSaberDifficulty : IModel
{
    [JsonProperty("id")]
    public long Id { get; set; }

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

    [JsonProperty("rawDifficulty")]
    public string RawDifficulty { get; set; } = null!;

    [JsonProperty("gameMode")]
    public string GameMode { get; set; } = null!;

    [JsonIgnore]
    public BeatmapDifficulty Difficulty { get; private set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }

    private int _diffNum;
}
