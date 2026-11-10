using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Models.MapModels;
using BLPPCounter.Utils.LangAddons;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.Map_Utils
{
    public sealed class Map
    {
        public const string SS_MODE_NAME = "SS_Diff";
        public const string AP_MODE_NAME = "AP_Diff";

        private readonly Dictionary<string, MapModel> _data;

        public Map(string hash)
        {
            Hash = NormalizeHash(hash);
            _data = [];
        }

        public Map(string hash, string songId, ICacheModel data)
            : this(hash)
        {
            Add(songId, data);
        }

        public Map(string hash, string mode, BeatmapDifficulty difficulty, string songId, ICacheModel data)
            : this(hash)
        {
            Add(mode, difficulty, songId, data);
        }

        public Map(Map other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            Hash = other.Hash;

            _data = other._data.ToDictionary(
                pair => pair.Key,
                pair => new MapModel(pair.Value),
                StringComparer.Ordinal
            );
        }

        public string Hash { get; }

        public void Add(string songId, ICacheModel data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Add(data.Mode, data.Difficulty, songId, data);
        }

        public void Add(string mode, BeatmapDifficulty difficulty, string songId, ICacheModel data)
        {
            if (BetterString.IsNullOrWhiteSpace(mode))
            {
                throw new ArgumentException("A map mode is required.", nameof(mode));
            }

            if (BetterString.IsNullOrWhiteSpace(songId))
            {
                throw new ArgumentException("A song ID is required.", nameof(songId));
            }

            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (!_data.TryGetValue(mode, out MapModel? model))
            {
                model = new MapModel(mode);
                _data.Add(mode, model);
            }

            model.MapData[difficulty] = new MapDiffModel(songId, difficulty, data);
        }

        public MapDiffModel? Get(string mode, BeatmapDifficulty difficulty)
        {
            return TryGet(mode, difficulty, out MapDiffModel? value) ? value : null;
        }

        public Dictionary<string, MapDiffModel> Get(BeatmapDifficulty difficulty)
        {
            return _data
                .Where(pair => pair.Value.MapData.ContainsKey(difficulty))
                .ToDictionary(pair => pair.Key, pair => pair.Value.MapData[difficulty], StringComparer.Ordinal);
        }

        public bool TryGet(string mode, BeatmapDifficulty difficulty, [NotNullWhen(true)] out MapDiffModel? value)
        {
            value = null;

            return !BetterString.IsNullOrWhiteSpace(mode)
                && _data.TryGetValue(mode, out MapModel? model)
                && model.MapData.TryGetValue(difficulty, out value);
        }

        public bool TryGet(BeatmapDifficulty difficulty, out Dictionary<string, MapDiffModel> value)
        {
            value = Get(difficulty);
            return value.Count > 0;
        }

        public bool ContainsMode(string? mode)
        {
            return !BetterString.IsNullOrWhiteSpace(mode) && _data.ContainsKey(mode);
        }

        public string[] GetModes()
        {
            return [.. _data.Keys];
        }

        public Map CreateSnapshot()
        {
            return new Map(this);
        }

        public void Combine(Map other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (!string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Cannot combine map {other.Hash} " + $"with map {Hash}.", nameof(other));
            }

            foreach (KeyValuePair<string, MapModel> pair in other._data)
            {
                if (!_data.TryGetValue(pair.Key, out MapModel? current))
                {
                    _data.Add(pair.Key, new MapModel(pair.Value));

                    continue;
                }

                foreach (KeyValuePair<BeatmapDifficulty, MapDiffModel> difficulty in pair.Value.MapData)
                {
                    current.MapData[difficulty.Key] = new MapDiffModel(difficulty.Value);
                }
            }
        }

        public static Map Combine(Map first, Map second)
        {
            if (first is null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            Map result = new(first);
            result.Combine(second);
            return result;
        }

        public static Map ConvertSSToTaoh(string hash, string songId, JToken scoreSaberData)
        {
            if (scoreSaberData is null)
            {
                throw new ArgumentNullException(nameof(scoreSaberData));
            }

            JToken difficultyData = scoreSaberData["difficulty"] ?? scoreSaberData;

            int difficultyNumber =
                difficultyData.Value<int?>("difficulty")
                ?? scoreSaberData.Value<int?>("difficulty")
                ?? throw new InvalidOperationException("ScoreSaber map data has no difficulty.");

            string mode =
                difficultyData.Value<string>("gameMode") ?? scoreSaberData.Value<string>("gameMode") ?? "SoloStandard";

            TaohCache cache = new()
            {
                Name = scoreSaberData.Value<string>("songName") ?? "",
                ScoreSaberID = songId,
                Hash = NormalizeHash(hash),
                DiffNum = difficultyNumber,
                Characteristic = mode.StartsWith("Solo", StringComparison.Ordinal) ? mode[4..] : mode,
                StarScoreSaber = scoreSaberData.Value<float?>("stars"),
            };

            return new Map(hash, SS_MODE_NAME, cache.Difficulty, songId, cache);
        }

        public static Map ConvertAPToTaoh(string hash, string songId, JToken accSaberData)
        {
            if (accSaberData is null)
            {
                throw new ArgumentNullException(nameof(accSaberData));
            }

            string difficulty =
                accSaberData.Value<string>("difficulty")
                ?? throw new InvalidOperationException("AccSaber map data has no difficulty.");

            string? categoryId = accSaberData.Value<string>("categoryId");

            TaohCache cache = new()
            {
                Name = accSaberData.Value<string>("songName") ?? "",
                ScoreSaberID =
                    accSaberData.Value<string>("scoreSaberLeaderboardId")
                    ?? accSaberData.Value<string>("leaderboardId"),
                Hash = NormalizeHash(hash),
                DiffNum = PathUtils.ReloadedDiffToDiffNum(difficulty),
                Characteristic = accSaberData.Value<string>("characteristic") ?? Profile_Utils.Profile.DEFAULT_MODE,
                SongCategory = CategoryIdToMask(categoryId),
                ComplexityAccSaber = accSaberData.Value<float?>("complexity"),
            };

            return new Map(hash, AP_MODE_NAME, cache.Difficulty, songId, cache);
        }

        public static BeatmapDifficulty FromValue(int value)
        {
            return (BeatmapDifficulty)(((value + 1) / 2) - 1);
        }

        public static int FromDiff(BeatmapDifficulty value)
        {
            return (((int)value + 1) * 2) - 1;
        }

        public static string NormalizeHash(string hash)
        {
            if (BetterString.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("A map hash is required.", nameof(hash));
            }

            return hash.Trim().ToUpperInvariant();
        }

        public override string ToString()
        {
            string output = $"Hash: {Hash}\n";

            foreach (KeyValuePair<string, MapModel> mode in _data)
            {
                output += $"---- {mode.Key}:\n";

                foreach (KeyValuePair<BeatmapDifficulty, MapDiffModel> difficulty in mode.Value.MapData)
                {
                    output += $"-------- {difficulty.Key}: " + $"{difficulty.Value.Data}\n";
                }
            }

            return output;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Hash);
        }

        private static int CategoryIdToMask(string? categoryId)
        {
            return categoryId switch
            {
                "b0000000-0000-0000-0000-000000000001" => 1 << 1,

                "b0000000-0000-0000-0000-000000000002" => 1 << 2,

                "b0000000-0000-0000-0000-000000000003" => 1 << 3,

                _ => 0,
            };
        }
    }
}
