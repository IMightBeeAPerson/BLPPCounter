using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class ProfileUnusualModeDataConverter : JsonConverter<ProfileUnusualModeData>
    {
        public override ProfileUnusualModeData ReadJson(
            JsonReader reader,
            Type objectType,
            ProfileUnusualModeData? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer
        )
        {
            JObject data = JObject.Load(reader);

            int index =
                data.Value<int?>(nameof(ProfileUnusualModeData.Index))
                ?? data.Value<int?>("Item1")
                ?? throw new JsonSerializationException("An unusual-mode entry has no index.");

            string mode =
                data.Value<string>(nameof(ProfileUnusualModeData.Mode))
                ?? data.Value<string>("Item2")
                ?? Profile.DEFAULT_MODE;

            return new ProfileUnusualModeData(index, mode);
        }

        public override void WriteJson(JsonWriter writer, ProfileUnusualModeData? value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(ProfileUnusualModeData.Index));
            writer.WriteValue(value.Index);

            writer.WritePropertyName(nameof(ProfileUnusualModeData.Mode));
            writer.WriteValue(value.Mode);

            writer.WriteEndObject();
        }
    }
}
