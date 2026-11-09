using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BeatLeader.Models.Replay;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.LangAddons;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal sealed class LocalReplayService : ILocalReplayService, IDisposable
    {
        private readonly object _stateLock = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private Dictionary<string, string> _cachedHeaders = [with(StringComparer.Ordinal)];

        private bool _loaded;
        private int _disposed;

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                lock (_stateLock)
                {
                    if (_loaded)
                        return;
                }

                Dictionary<string, string> headers = await Task.Run(
                        () => LoadHeaders(cancellationToken),
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                lock (_stateLock)
                {
                    _cachedHeaders = headers;
                    _loaded = true;
                }

                Plugin.Log.Info($"Loaded {headers.Count} local replays.");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<string?> GetReplayNameAsync(
            string userId,
            string hash,
            string mode,
            string difficulty,
            CancellationToken cancellationToken = default
        )
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);

            string key = GetKey(userId, hash, mode, difficulty);

            lock (_stateLock)
            {
                return _cachedHeaders.TryGetValue(key, out string? replayName) ? replayName : null;
            }
        }

        public string? GetReplayPath(string replayName)
        {
            ThrowIfDisposed();

            if (BetterString.IsNullOrWhiteSpace(replayName))
            {
                return null;
            }

            string path = Path.Combine(HelpfulPaths.BL_REPLAY_FOLDER, replayName);

            if (File.Exists(path))
                return path;

            path = Path.Combine(HelpfulPaths.BL_REPLAY_CACHE_FOLDER, replayName);

            return File.Exists(path) ? path : null;
        }

        public void Clear()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                _cachedHeaders.Clear();
                _loaded = false;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            lock (_stateLock)
            {
                _cachedHeaders.Clear();
                _loaded = false;
            }

            _loadLock.Dispose();
        }

        private Dictionary<string, string> LoadHeaders(CancellationToken cancellationToken)
        {
            Plugin.Log.Info("Loading local replays...");

            Dictionary<string, string> headers = [with(StringComparer.Ordinal)];

            if (!File.Exists(HelpfulPaths.BL_REPLAY_HEADERS))
            {
                Plugin.Log.Warn("The BeatLeader replay-header file does " + "not exist. No local replays were loaded.");

                return headers;
            }

            try
            {
                JObject replayHeaders = JObject.Parse(File.ReadAllText(HelpfulPaths.BL_REPLAY_HEADERS));

                Dictionary<string, int> cachedScores = [with(StringComparer.Ordinal)];

                foreach (KeyValuePair<string, JToken?> entry in replayHeaders)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    JToken? value = entry.Value;

                    if (value is null)
                        continue;

                    string? playerId = value.Value<string>("PlayerID");

                    string? songHash = value.Value<string>("SongHash");

                    string? songMode = value.Value<string>("SongMode");

                    string? songDifficulty = value.Value<string>("SongDifficulty");

                    if (
                        BetterString.IsNullOrWhiteSpace(playerId)
                        || BetterString.IsNullOrWhiteSpace(songHash)
                        || BetterString.IsNullOrWhiteSpace(songMode)
                        || BetterString.IsNullOrWhiteSpace(songDifficulty)
                    )
                    {
                        continue;
                    }

                    string key = GetKey(playerId, songHash, songMode, songDifficulty);

                    if (!headers.TryGetValue(key, out string? existingReplay))
                    {
                        headers[key] = entry.Key;
                        continue;
                    }

                    int existingScore = GetReplayScore(existingReplay);

                    int candidateScore = GetReplayScore(entry.Key);

                    cachedScores[key] = Math.Max(existingScore, candidateScore);

                    if (candidateScore > existingScore)
                    {
                        headers[key] = entry.Key;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Failed to load local replays: " + exception.Message);

                Plugin.Log.Debug(exception);
            }

            return headers;
        }

        private int GetReplayScore(string replayName)
        {
            string? path = GetReplayPath(replayName);

            if (path is null)
                return -1;

            try
            {
                byte[] data = File.ReadAllBytes(path);

                return ReplayDecoder.TryDecodeReplayInfo(data, out ReplayInfo? info) && info is not null
                    ? info.score
                    : -1;
            }
            catch (Exception exception)
            {
                Plugin.Log.Debug($"Could not read replay " + $"\"{replayName}\": {exception}");

                return -1;
            }
        }

        private static string GetKey(string userId, string hash, string mode, string difficulty)
        {
            return string.Join("-", userId, hash.ToUpperInvariant(), mode, difficulty);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(LocalReplayService));
            }
        }
    }
}
