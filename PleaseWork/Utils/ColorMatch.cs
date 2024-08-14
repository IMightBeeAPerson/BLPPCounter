using Newtonsoft.Json;

namespace PleaseWork.Utils
{
    public struct ColorMatch
    {
        [JsonProperty(nameof(Rank), Required = Required.DisallowNull)]
        public int Rank { get; set; }
        [JsonProperty(nameof(Color), Required = Required.DisallowNull)]
        public string Color { get; set; }

        public ColorMatch(int rank, string color)
        {
            Rank = rank;
            Color = color;
        }

        public override string ToString() => $"#{Rank}, Color: #{Color}";
    }
}
