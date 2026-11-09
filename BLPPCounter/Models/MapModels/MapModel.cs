using System;
using System.Collections.Generic;
using System.Linq;
using BLPPCounter.Models.Base;
using Newtonsoft.Json;

namespace BLPPCounter.Models.MapModels
{
#pragma warning disable IDE0290
    public class MapModel : IModel
    {
        [JsonProperty("mode")]
        public string Mode { get; set; } = null!;

        [JsonProperty("mapData")]
        public Dictionary<BeatmapDifficulty, MapDiffModel> MapData { get; set; } = [];

        public MapModel(string mode)
        {
            Mode = mode;
        }

        public MapModel(MapModel other)
        {
            Mode = other.Mode;
            MapData =
            [
                with(
                    other.MapData.Select(kvp => new KeyValuePair<BeatmapDifficulty, MapDiffModel>(
                        kvp.Key,
                        new(kvp.Value)
                    ))
                ),
            ];
        }
    }

    public class MapDiffModel : IModel, IEquatable<MapDiffModel>, IComparable, IComparable<MapDiffModel>
    {
        [JsonProperty("songId")]
        public string SongId { get; set; } = null!;

        [JsonProperty("difficulty")]
        public BeatmapDifficulty Difficulty { get; set; }

        [JsonProperty("data")]
        public ICacheModel Data { get; set; } = null!;

        public MapDiffModel(string songId, BeatmapDifficulty diff, ICacheModel data)
        {
            SongId = songId;
            Difficulty = diff;
            Data = data;
        }

        public MapDiffModel(MapDiffModel other)
        {
            SongId = other.SongId;
            Difficulty = other.Difficulty;
            Data = (ICacheModel)other.Data.Clone();
        }

        public int CompareTo(MapDiffModel other)
        {
            return Difficulty.CompareTo(other.Difficulty);
        }

        public bool Equals(MapDiffModel other)
        {
            return SongId.Equals(other.SongId) && Difficulty.Equals(other.Difficulty);
        }

        public int CompareTo(object obj)
        {
            return obj is MapDiffModel other ? CompareTo(other) : -1;
        }
    }
}
