using System.Collections.Generic;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Enums;
using Newtonsoft.Json;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class ProfilePersistenceData
    {
        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)]
        public Leaderboards Leaderboard { get; set; }

        [JsonProperty(nameof(AccSaberType), Required = Required.DisallowNull)]
        public APCategory AccSaberType { get; set; }

        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)]
        public float[] Scores { get; set; } = [];

        [JsonProperty(nameof(ScoreNames), Required = Required.DisallowNull)]
        public string[] ScoreNames { get; set; } = [];

        [JsonProperty(nameof(ScoreDiffs), Required = Required.DisallowNull)]
        public ulong[] ScoreDiffs { get; set; } = [];

        [JsonProperty(nameof(ScoreIDs), Required = Required.DisallowNull)]
        public string[] ScoreIDs { get; set; } = [];

        [JsonProperty(nameof(Ranks), Required = Required.DisallowNull)]
        public uint[] Ranks { get; set; } = [];

        [JsonProperty(nameof(APCategories), Required = Required.AllowNull)]
        public ulong[]? APCategories { get; set; }

        [JsonProperty(
            nameof(UnusualModes),
            Required = Required.DisallowNull,
            ItemConverterType = typeof(ProfileUnusualModeDataConverter)
        )]
        public List<ProfileUnusualModeData> UnusualModes { get; set; } = [];

        [JsonProperty(nameof(TotalPP), Required = Required.DisallowNull)]
        public float TotalPP { get; set; }

        [JsonProperty(nameof(UserID), Required = Required.DisallowNull)]
        public string UserID { get; set; } = "";

        [JsonProperty(nameof(PlusOne), Required = Required.DisallowNull)]
        public float PlusOne { get; set; } = -1f;

        public void Validate()
        {
            if (BetterString.IsNullOrWhiteSpace(UserID))
            {
                throw new JsonSerializationException("A persisted profile has no user ID.");
            }

            Scores ??= [];
            ScoreNames ??= [];
            ScoreDiffs ??= [];
            ScoreIDs ??= [];
            Ranks ??= [];
            UnusualModes ??= [];

            int scoreCount = Scores.Length;

            if (ScoreNames.Length != scoreCount || ScoreIDs.Length != scoreCount || Ranks.Length != scoreCount)
            {
                throw new JsonSerializationException(
                    "A persisted profile contains score arrays " + "with different lengths."
                );
            }

            BeatmapDifficulty[] difficulties = HelpfulMisc.UncompressEnums<BeatmapDifficulty>(ScoreDiffs);

            if (difficulties.Length != scoreCount)
            {
                throw new JsonSerializationException(
                    "A persisted profile contains a difficulty " + "array with an invalid length."
                );
            }

            if (
                Leaderboard == Leaderboards.Accsaber
                && AccSaberType == APCategory.All
                && APCategories is not null
                && APCategories.Length > 0
            )
            {
                APCategory[] categories = HelpfulMisc.UncompressEnums<APCategory>(APCategories);

                if (categories.Length != scoreCount)
                {
                    throw new JsonSerializationException(
                        "A persisted AccSaber profile contains " + "an invalid category array."
                    );
                }
            }

            foreach (ProfileUnusualModeData unusualMode in UnusualModes)
            {
                if (unusualMode.Index < 0 || unusualMode.Index >= scoreCount)
                {
                    throw new JsonSerializationException(
                        "A persisted profile contains an " + "out-of-range unusual-mode index."
                    );
                }

                unusualMode.Mode = BetterString.IsNullOrWhiteSpace(unusualMode.Mode)
                    ? Profile.DEFAULT_MODE
                    : unusualMode.Mode;
            }
        }
    }

    internal sealed class ProfileUnusualModeData
    {
        public ProfileUnusualModeData() { }

        public ProfileUnusualModeData(int index, string mode)
        {
            Index = index;
            Mode = mode;
        }

        [JsonProperty(nameof(Index))]
        public int Index { get; set; }

        [JsonProperty(nameof(Mode))]
        public string Mode { get; set; } = Profile.DEFAULT_MODE;
    }
}
