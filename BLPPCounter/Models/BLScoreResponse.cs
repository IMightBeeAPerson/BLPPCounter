using BLPPCounter.Models.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Models
{
    internal class BLScoreResponse : IScoreModel
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("baseScore")]
        public int BaseScore { get; set; }

        [JsonProperty("modifiedScore")]
        public int ModifiedScore { get; set; }

        [JsonProperty("accuracy")]
        public float Accuracy { get; set; }

        [JsonProperty("playerId")]
        public string PlayerId { get; set; } = null!;

        [JsonProperty("pp")]
        public float Pp { get; set; }

        [JsonIgnore]
        public float LeaderboardPoints => Pp;

        [JsonProperty("withPp")]
        public bool WithPp { get; set; }

        [JsonProperty("bonusPp")]
        public float BonusPp { get; set; }

        [JsonProperty("passPP")]
        public float PassPp { get; set; }

        [JsonProperty("accPP")]
        public float AccPp { get; set; }

        [JsonProperty("techPP")]
        public float TechPp { get; set; }

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("responseRank")]
        public int ResponseRank { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }

        [JsonProperty("fcAccuracy")]
        public float FcAccuracy { get; set; }

        [JsonProperty("fcPp")]
        public float FcPp { get; set; }

        [JsonProperty("weight")]
        public float Weight { get; set; }

        [JsonProperty("replay")]
        public string Replay { get; set; } = null!;

        [JsonProperty("modifiers")]
        public string Modifiers { get; set; } = null!;

        [JsonProperty("badCuts")]
        public int BadCuts { get; set; }

        [JsonProperty("missedNotes")]
        public int MissedNotes { get; set; }

        [JsonProperty("bombCuts")]
        public int BombCuts { get; set; }

        [JsonProperty("wallsHit")]
        public int WallsHit { get; set; }

        [JsonProperty("pauses")]
        public int Pauses { get; set; }

        [JsonProperty("fullCombo")]
        public bool FullCombo { get; set; }

        [JsonProperty("platform")]
        public string Platform { get; set; } = null!;

        [JsonProperty("maxCombo")]
        public int MaxCombo { get; set; }

        [JsonProperty("maxStreak")]
        public int? MaxStreak { get; set; }

        // The schema represents this as an enum, but also references HMD.
        // Keeping it as JToken allows either numeric or string enum values.
        [JsonProperty("hmd")]
        public JToken Hmd { get; set; } = null!;

        // The schema represents this as an enum, but also references ControllerEnum.
        // Keeping it as JToken allows either numeric or string enum values.
        [JsonProperty("controller")]
        public JToken Controller { get; set; } = null!;

        [JsonProperty("leaderboardId")]
        public string LeaderboardId { get; set; } = null!;

        [JsonProperty("timeset")]
        public string Timeset { get; set; } = null!;

        [JsonProperty("timepost")]
        public int Timepost { get; set; }

        [JsonProperty("replaysWatched")]
        public int ReplaysWatched { get; set; }

        [JsonProperty("playCount")]
        public int PlayCount { get; set; }

        [JsonProperty("lastTryTime")]
        public int LastTryTime { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("originalId")]
        public int OriginalId { get; set; }

        [JsonProperty("player")]
        public JToken? Player { get; set; }

        [JsonIgnore]
        public string PlayerName => Player?["name"]?.ToString() ?? "";

        [JsonProperty("scoreImprovement")]
        public JToken? ScoreImprovement { get; set; }

        [JsonProperty("rankVoting")]
        public JToken? RankVoting { get; set; }

        [JsonProperty("metadata")]
        public JToken? Metadata { get; set; }

        [JsonProperty("offsets")]
        public JToken? Offsets { get; set; }

        [JsonProperty("sotwNominations")]
        public int SotwNominations { get; set; }

        // Bitmask enum in the schema. JToken is safest because the API may return
        // either integer or string enum values.
        [JsonProperty("status")]
        public JToken Status { get; set; } = null!;

        [JsonProperty("dance")]
        public JToken? Dance { get; set; }

        [JsonProperty("difficulty")]
        public JToken Difficulty { get; set; } = null!;

        [JsonProperty("song")]
        public JToken Song { get; set; } = null!;

        // Bitmask enum in the schema. JToken is safest because the API may return
        // either integer or string enum values.
        [JsonProperty("validContexts")]
        public JToken ValidContexts { get; set; } = null!;

        [JsonProperty("contextExtensions")]
        public JToken ContextExtensions { get; set; } = null!;

        [JsonProperty("externalStatuses")]
        public JToken? ExternalStatuses { get; set; }
    }
}
