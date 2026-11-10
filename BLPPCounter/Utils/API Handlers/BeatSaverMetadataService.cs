using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.API_Handlers
{
    internal sealed class BeatSaverMetadataService : IBeatSaverMetadataService, IDisposable
    {
        private readonly HttpClient _client;
        private readonly Throttler _throttler = new(50, 10);

        private int _disposed;

        public BeatSaverMetadataService(PluginConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(config.APITimeout) };
        }

        public async Task<BeatSaverMapMetadata?> GetMapMetadataAsync(
            string hash,
            BeatmapDifficulty difficulty,
            string characteristic,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();

            if (BetterString.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("A map hash is required.", nameof(hash));
            }

            if (BetterString.IsNullOrWhiteSpace(characteristic))
            {
                characteristic = Profile_Utils.Profile.DEFAULT_MODE;
            }

            await _throttler.Call(cancellationToken).ConfigureAwait(false);

            string path = string.Format(HelpfulPaths.BSAPI_HASH, hash);

            using HttpResponseMessage response = await _client.GetAsync(path, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Plugin.Log.Warn(
                    $"BeatSaver metadata request failed with " + $"{(int)response.StatusCode} for hash {hash}."
                );

                return null;
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (BetterString.IsNullOrWhiteSpace(json))
                return null;

            JObject root = JObject.Parse(json);

            string? mapKey = root.Value<string>("id");

            if (BetterString.IsNullOrWhiteSpace(mapKey))
            {
                Plugin.Log.Warn($"BeatSaver returned no map key for hash " + $"{hash}.");

                return null;
            }

            JToken? versions = root["versions"];

            JToken? version =
                versions
                    ?.Children()
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.Value<string>("hash"), hash, StringComparison.OrdinalIgnoreCase)
                    )
                ?? versions?.Children().FirstOrDefault();

            JToken? selectedDifficulty = version
                ?["diffs"]?.Children()
                .FirstOrDefault(candidate =>
                    DifficultyMatches(candidate, difficulty) && CharacteristicMatches(candidate, characteristic)
                );

            if (selectedDifficulty is null)
            {
                Plugin.Log.Warn(
                    $"BeatSaver returned no matching difficulty " + $"for {hash}, {difficulty}, " + $"{characteristic}."
                );

                return new BeatSaverMapMetadata(mapKey!, Leaderboards.None);
            }

            Leaderboards ranked = Leaderboards.None;

            if (HasPositiveNumber(selectedDifficulty["blStars"]))
            {
                ranked |= Leaderboards.Beatleader;
            }

            if (HasPositiveNumber(selectedDifficulty["stars"]))
            {
                ranked |= Leaderboards.Scoresaber;
            }

            return new BeatSaverMapMetadata(mapKey!, ranked);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _client.Dispose();
            _throttler.Dispose();
        }

        private static bool DifficultyMatches(JToken data, BeatmapDifficulty difficulty)
        {
            string? returnedDifficulty = data.Value<string>("difficulty");

            if (BetterString.IsNullOrWhiteSpace(returnedDifficulty))
            {
                return false;
            }

            string expected = difficulty.ToString();

            return string.Equals(returnedDifficulty, expected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    returnedDifficulty!.Replace("_", ""),
                    expected.Replace("Plus", "+"),
                    StringComparison.OrdinalIgnoreCase
                )
                || string.Equals(returnedDifficulty.Replace("+", "Plus"), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CharacteristicMatches(JToken data, string characteristic)
        {
            string? returnedCharacteristic = data.Value<string>("characteristic");

            return string.Equals(returnedCharacteristic, characteristic, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasPositiveNumber(JToken? token)
        {
            return token is not null && token.Type != JTokenType.Null && token.Value<float?>() is > 0f;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(BeatSaverMetadataService));
            }
        }
    }
}
