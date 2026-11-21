using System.Collections.Generic;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models.ScoreSaber;

/// <summary>
/// Used by score-list endpoints:
///
/// GET /api/v2/leaderboards/{id}/scores
/// GET /api/v2/leaderboards/hash/{hash}/{mode}/{difficulty}/scores
/// GET /api/v2/players/{id}/scores
/// </summary>
internal class ScoreSaberPagedScores<T> : IModel
{
    [JsonProperty("data")]
    public List<T> Data { get; set; } = [];

    [JsonProperty("metadata")]
    public ScoreSaberPaginationMetadata? Metadata { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}

/// <summary>
/// Used by score-list endpoints:
///
/// GET /api/v2/leaderboards/{id}/scores
/// GET /api/v2/leaderboards/hash/{hash}/{mode}/{difficulty}/scores
/// </summary>
internal class ScoreSaberPagedScores : ScoreSaberPagedScores<ScoreSaberScore> { }

/// <summary>
/// Pagination metadata is included where ScoreSaber provides it.
/// Keeping fields nullable makes the model work if a particular endpoint
/// omits one or more metadata properties.
/// </summary>
internal class ScoreSaberPaginationMetadata : IModel
{
    [JsonProperty("total")]
    public long? Total { get; set; }

    [JsonProperty("page")]
    public int? Page { get; set; }

    [JsonProperty("itemsPerPage")]
    public int? ItemsPerPage { get; set; }

    [JsonProperty("totalPages")]
    public int? TotalPages { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken>? AdditionalData { get; set; }
}
