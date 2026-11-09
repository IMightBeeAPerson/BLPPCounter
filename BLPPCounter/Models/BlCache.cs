using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static GameplayModifiers;

namespace BLPPCounter.Models
{
    [UsedImplicitly]
    internal class BlCache : IModel
    {
        [JsonProperty("Entries")]
        public List<BlCacheEntry> Entries { get; set; } = null!;

        [JsonProperty("LastUpdated")]
        public DateTime LastUpdated { get; set; }
    }

    [UsedImplicitly]
    internal class BlCacheEntry : ICacheModel
    {
        [JsonProperty("LeaderboardId")]
        public string LeaderboardId { get; set; } = null!;

        [JsonProperty("SongInfo")]
        public BlCacheSongInfo SongInfo { get; set; } = null!;

        [JsonProperty("DifficultyInfo")]
        public BlCacheDifficultyInfo DifficultyInfo { get; set; } = null!;

        // QualificationInfo, ClanRankingContested

        [JsonIgnore]
        public string Mode => DifficultyInfo.Characteristic;

        [JsonIgnore]
        public BeatmapDifficulty Difficulty => DifficultyInfo.Difficulty;

        [JsonIgnore]
        public string Hash => SongInfo.Hash;

        public object Clone()
        {
            BlCacheEntry clone = new()
            {
                LeaderboardId = LeaderboardId,
                SongInfo = (BlCacheSongInfo)SongInfo.Clone(),
                DifficultyInfo = (BlCacheDifficultyInfo)DifficultyInfo.Clone(),
            };

            return clone;
        }
    }

    [UsedImplicitly]
    internal class BlCacheSongInfo : ModelId, ICloneable
    {
        [JsonProperty("id")]
        public override string Id { get; set; } = null!;

        [JsonProperty("hash")]
        public string Hash { get; set; } = null!;

        [JsonProperty("songName")]
        public string? SongName { get; set; } = null!;

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    [UsedImplicitly]
    internal class BlCacheDifficultyInfo : ModelId, IDeepCloneable
    {
        [JsonProperty("id")]
        public override string Id { get; set; } = null!;

        [JsonProperty("value")]
        public int DifficultyValue
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

        // mode, status

        [JsonProperty("modeName")]
        public string Characteristic { get; set; } = null!;

        [JsonProperty("difficultyName")]
        public string DifficultyName { get; set; } = null!;

        // nominatedTime, qualifiedTime, rankedTime

        [JsonProperty("stars")]
        public float Stars { get; set; }

        [JsonProperty("accRating")]
        public float AccRating { get; set; }

        [JsonProperty("techRating")]
        public float TechRating { get; set; }

        [JsonProperty("passRating")]
        public float PassRating { get; set; }

        [JsonProperty("maxScore")]
        public int MaxScore { get; set; }

        [JsonProperty("modifierValues")]
        public JObject ModifierValues { get; set; } = null!;

        [JsonIgnore]
        public Dictionary<string, float> Modifiers { get; private set; } = null!;

        [JsonProperty("modifierRatings")]
        public BlCacheModifierRatings ModifierRatings { get; set; } = null!;

        public float GetRating(PPType type, SongSpeed speed)
        {
            const int songSpeedLen = 4;

            int speedNum = speed switch
            {
                SongSpeed.Slower => 0,
                SongSpeed.Normal => 1,
                SongSpeed.Faster => 2,
                SongSpeed.SuperFast => 3,
                _ => -1,
            };

            int ppNum = type switch
            {
                PPType.Star => 0,
                PPType.Acc => 1 * songSpeedLen,
                PPType.Tech => 2 * songSpeedLen,
                PPType.Pass => 3 * songSpeedLen,
                _ => -1,
            };

            if (speedNum == -1 || ppNum == -1)
                throw new ArgumentException("Either the pp type or song speed given is invalid.");

            int num = speedNum + ppNum;
            return num switch
            {
                0 => ModifierRatings.SSStars,
                1 => Stars,
                2 => ModifierRatings.FSStars,
                3 => ModifierRatings.SFStars,
                4 => ModifierRatings.SSAccRating,
                5 => AccRating,
                6 => ModifierRatings.FSAccRating,
                7 => ModifierRatings.SFAccRating,
                8 => ModifierRatings.SSTechRating,
                9 => TechRating,
                10 => ModifierRatings.FSTechRating,
                11 => ModifierRatings.SFTechRating,
                12 => ModifierRatings.SSPassRating,
                13 => PassRating,
                14 => ModifierRatings.FSPassRating,
                15 => ModifierRatings.SFPassRating,
                _ => throw new ArgumentException("Either the pp type or song speed given is invalid."),
            };
        }

        public object Clone()
        {
            BlCacheDifficultyInfo clone = (BlCacheDifficultyInfo)MemberwiseClone();
            clone.ModifierValues = (JObject)ModifierValues.DeepClone();
            clone.ModifierRatings = (BlCacheModifierRatings)ModifierRatings.Clone();
            clone.OnDeserialized(default);

            return clone;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Modifiers =
            [
                with(
                    ((IEnumerable<KeyValuePair<string, JToken>>)ModifierValues).Select(p => new KeyValuePair<
                        string,
                        float
                    >(p.Key, (float)p.Value))
                ),
            ];
        }
    }

    [UsedImplicitly]
    internal sealed class BlCacheModifierRatings : IModel, ICloneable
    {
        [JsonProperty("ssStars")]
        public float SSStars { get; set; }

        [JsonProperty("ssAccRating")]
        public float SSAccRating { get; set; }

        [JsonProperty("ssTechRating")]
        public float SSTechRating { get; set; }

        [JsonProperty("ssPassRating")]
        public float SSPassRating { get; set; }

        [JsonProperty("fsStars")]
        public float FSStars { get; set; }

        [JsonProperty("fsAccRating")]
        public float FSAccRating { get; set; }

        [JsonProperty("fsTechRating")]
        public float FSTechRating { get; set; }

        [JsonProperty("fsPassRating")]
        public float FSPassRating { get; set; }

        [JsonProperty("sfStars")]
        public float SFStars { get; set; }

        [JsonProperty("sfAccRating")]
        public float SFAccRating { get; set; }

        [JsonProperty("sfTechRating")]
        public float SFTechRating { get; set; }

        [JsonProperty("sfPassRating")]
        public float SFPassRating { get; set; }

        public object Clone() => MemberwiseClone();
    }
}
