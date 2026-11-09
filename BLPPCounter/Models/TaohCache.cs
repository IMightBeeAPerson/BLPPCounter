using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Map_Utils;
using Newtonsoft.Json;

namespace BLPPCounter.Models
{
    internal class TaohCache : ICacheModel
    {
        [JsonProperty("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("scoreSaberID")]
        public string? ScoreSaberID { get; set; }

        [JsonProperty("beatLeaderID")]
        public string? BeatLeaderID { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("difficulty")]
        public int DiffNum
        {
            get;
            set
            {
                field = value;
                Difficulty = Map.FromValue(value);
            }
        }

        [JsonIgnore]
        public BeatmapDifficulty Difficulty { get; private set; }

        [JsonProperty("characteristic")]
        public string Characteristic { get; set; } = null!;

        [JsonProperty("songCategory")]
        public int SongCategory { get; set; }

        [JsonProperty("starScoreSaber")]
        public float? StarScoreSaber { get; set; }

        [JsonProperty("starBeatLeader")]
        public float? StarBeatLeader { get; set; }

        [JsonProperty("starAccBeatLeader")]
        public float? StarAccBeatLeader { get; set; }

        [JsonProperty("starPassBeatLeader")]
        public float? StarPassBeatLeader { get; set; }

        [JsonProperty("starTechBeatLeader")]
        public float? StarTechBeatLeader { get; set; }

        [JsonProperty("complexityAccSaber")]
        public float? ComplexityAccSaber { get; set; }

        [JsonProperty("complexityAutoBalancer")]
        public float? ComplexityAutoBalancer { get; set; }

        [JsonIgnore]
        public string LeaderboardId => BeatLeaderID ?? ScoreSaberID ?? "";

        [JsonIgnore]
        public string Mode => Characteristic;

        public object Clone()
        {
            // There are no reference types, so there is no need to fully implement a deep copy.
            return MemberwiseClone();
        }
    }
}
