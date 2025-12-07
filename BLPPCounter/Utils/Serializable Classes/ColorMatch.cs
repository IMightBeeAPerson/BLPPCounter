using Newtonsoft.Json;

namespace BLPPCounter.Utils.Serializable_Classes
{
    public struct ColorMatch(int rank, string color)
    {
        [JsonProperty(nameof(Rank), Required = Required.DisallowNull)]
        public int Rank { get; set; } = rank;
        [JsonProperty(nameof(Color), Required = Required.DisallowNull)]
        public string Color { get; set; } = color;

        public override readonly string ToString() => $"#{Rank}, Color: #{Color}";
    }
}
