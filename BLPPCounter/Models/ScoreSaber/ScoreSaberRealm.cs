using System;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BLPPCounter.Models.ScoreSaber
{
    internal sealed class ScoreSaberRealm : IModel
    {
        [JsonProperty("realmId")]
        public float RealmId { get; set; }

        [JsonProperty("realmName")]
        public string RealmName { get; set; } = string.Empty;

        [JsonProperty("leaderboardStatus")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScoreSaberLeaderboardStatus LeaderboardStatus { get; set; }

        [JsonProperty("positiveModifiers")]
        public bool PositiveModifiers { get; set; }

        [JsonProperty("stars")]
        public float Stars { get; set; }

        [JsonProperty("rankedAt")]
        public DateTimeOffset? RankedAt { get; set; }

        [JsonProperty("qualifiedAt")]
        public DateTimeOffset? QualifiedAt { get; set; }

        [JsonProperty("lovedAt")]
        public DateTimeOffset? LovedAt { get; set; }
    }

    public enum ScoreSaberLeaderboardStatus
    {
        UNRANKED,
        RANKED,
        QUALIFIED,
        LOVED,
    }
}
