using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Safety;
using Newtonsoft.Json;
using Zenject;

namespace BLPPCounter.Utils.Map_Utils
{
    internal sealed class MapRepository : IMapRepository, IInitializable, IDisposable
    {
        private readonly object _stateLock = new();
        private readonly PluginConfig _config;
        private readonly ILeaderboardApiProvider _apiProvider;
        private readonly IBackgroundTaskRunner _taskRunner;
        private readonly CancellationTokenSource _lifetimeSource = new();

        private readonly Dictionary<string, Map> _maps = [with(StringComparer.OrdinalIgnoreCase)];

        private readonly Dictionary<LoadKey, Task> _pendingLoads = [];

        private Task _initializationTask = Task.CompletedTask;

        private bool _initializationStarted;
        private int _disposed;

        public MapRepository(PluginConfig config, ILeaderboardApiProvider apiProvider, IBackgroundTaskRunner taskRunner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
        }

        public Task InitializationTask
        {
            get
            {
                lock (_stateLock)
                    return _initializationTask;
            }
        }

        public void Initialize()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_initializationStarted)
                    return;

                _initializationStarted = true;

                _initializationTask = LoadDiskCachesAsync(_lifetimeSource.Token);
            }

            _taskRunner.Forget(_initializationTask, "Initialize map repository");
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            Initialize();

            await AwaitWithCancellationAsync(InitializationTask, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Map?> GetAsync(
            string hash,
            string? mode,
            Leaderboards leaderboard,
            bool forceRemoteLoad = false,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();

            string normalizedHash = Map.NormalizeHash(hash);

            await InitializeAsync(cancellationToken).ConfigureAwait(false);

            if (TryGetUsableSnapshot(normalizedHash, mode, out Map? cached))
            {
                return cached;
            }

            if (!forceRemoteLoad && !_config.HuntLoads)
            {
                return TryGet(normalizedHash, out Map? partial) ? partial : null;
            }

            Task loadTask = GetOrCreateLoadTask(normalizedHash, leaderboard);

            await AwaitWithCancellationAsync(loadTask, cancellationToken).ConfigureAwait(false);

            if (TryGetUsableSnapshot(normalizedHash, mode, out Map? loaded))
            {
                return loaded;
            }

            return TryGet(normalizedHash, out Map? result) ? result : null;
        }

        public bool TryGet(string hash, out Map? map)
        {
            ThrowIfDisposed();

            string normalizedHash = Map.NormalizeHash(hash);

            lock (_stateLock)
            {
                if (!_maps.TryGetValue(normalizedHash, out Map? stored))
                {
                    map = null;
                    return false;
                }

                map = stored.CreateSnapshot();
                return true;
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();

            lock (_stateLock)
                _maps.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
        }

        private Task GetOrCreateLoadTask(string hash, Leaderboards leaderboard)
        {
            LoadKey key = new(hash, leaderboard);

            lock (_stateLock)
            {
                if (_pendingLoads.TryGetValue(key, out Task? existing))
                {
                    return existing;
                }

                Task created = LoadFromApiAsync(key, _lifetimeSource.Token);

                _pendingLoads.Add(key, created);

                return created;
            }
        }

        private async Task LoadFromApiAsync(LoadKey key, CancellationToken cancellationToken)
        {
            try
            {
                APIHandler api = _apiProvider.Get(key.Leaderboard);

                Dictionary<string, Map> isolatedResult = [with(StringComparer.OrdinalIgnoreCase)];

                await api.AddMap(isolatedResult, key.Hash, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (
                    !isolatedResult.TryGetValue(key.Hash, out Map? loaded)
                    && !isolatedResult.TryGetValue(key.Hash.ToLowerInvariant(), out loaded)
                )
                {
                    return;
                }

                AddOrCombine(loaded);
            }
            finally
            {
                lock (_stateLock)
                    _pendingLoads.Remove(key);
            }
        }

        private async Task LoadDiskCachesAsync(CancellationToken cancellationToken)
        {
            List<Map> maps = await Task.Run(() => LoadDiskCaches(cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            foreach (Map map in maps)
                AddOrCombine(map);
        }

        private static List<Map> LoadDiskCaches(CancellationToken cancellationToken)
        {
            List<Map> maps = [];

            LoadBeatLeaderCache(maps, cancellationToken);

            LoadTaohCache(maps, cancellationToken);

            return maps;
        }

        private static void LoadBeatLeaderCache(ICollection<Map> maps, CancellationToken cancellationToken)
        {
            if (!File.Exists(HelpfulPaths.BL_CACHE_FILE))
            {
                return;
            }

            try
            {
                BlCache? cache = JsonConvert.DeserializeObject<BlCache>(File.ReadAllText(HelpfulPaths.BL_CACHE_FILE));

                if (cache?.Entries is null)
                {
                    Plugin.Log.Warn("BeatLeader cache was empty or invalid.");

                    return;
                }

                foreach (BlCacheEntry entry in cache.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    maps.Add(new Map(entry.SongInfo.Hash, entry.LeaderboardId, entry));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Failed to load the BeatLeader cache: " + exception.Message);

                Plugin.Log.Debug(exception);
            }
        }

        private static void LoadTaohCache(ICollection<Map> maps, CancellationToken cancellationToken)
        {
            if (!File.Exists(HelpfulPaths.TAOHABLE_DATA))
            {
                return;
            }

            try
            {
                List<TaohCache>? entries = JsonConvert.DeserializeObject<List<TaohCache>>(
                    File.ReadAllText(HelpfulPaths.TAOHABLE_DATA)
                );

                if (entries is null)
                {
                    Plugin.Log.Warn("Taoh cache was empty or invalid.");

                    return;
                }

                foreach (TaohCache entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool scoreSaberRanked = entry.ScoreSaberID is not null;

                    bool accSaberRanked = entry.ComplexityAccSaber is not null;

                    if (!scoreSaberRanked && !accSaberRanked)
                    {
                        continue;
                    }

                    string songId = entry.ScoreSaberID ?? entry.BeatLeaderID ?? "";

                    Map map = new(entry.Hash);

                    if (scoreSaberRanked)
                    {
                        map.Add(Map.SS_MODE_NAME, entry.Difficulty, songId, entry);
                    }

                    if (accSaberRanked)
                    {
                        map.Add(Map.AP_MODE_NAME, entry.Difficulty, songId, entry);
                    }

                    maps.Add(map);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("Failed to load the Taoh cache: " + exception.Message);

                Plugin.Log.Debug(exception);
            }
        }

        private bool TryGetUsableSnapshot(string hash, string? mode, out Map? map)
        {
            lock (_stateLock)
            {
                if (
                    !_maps.TryGetValue(hash, out Map? stored)
                    || (!BetterString.IsNullOrWhiteSpace(mode) && !stored.ContainsMode(mode))
                )
                {
                    map = null;
                    return false;
                }

                map = stored.CreateSnapshot();
                return true;
            }
        }

        private void AddOrCombine(Map map)
        {
            lock (_stateLock)
            {
                if (_maps.TryGetValue(map.Hash, out Map? current))
                {
                    current.Combine(map);
                }
                else
                {
                    _maps.Add(map.Hash, map.CreateSnapshot());
                }
            }
        }

        private static async Task AwaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                await task.ConfigureAwait(false);
                return;
            }

            TaskCompletionSource<object?> cancellationSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                cancellationSource.TrySetCanceled(cancellationToken)
            );

            Task completed = await Task.WhenAny(task, cancellationSource.Task).ConfigureAwait(false);

            await completed.ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(MapRepository));
            }
        }

        private readonly struct LoadKey : IEquatable<LoadKey>
        {
            internal LoadKey(string hash, Leaderboards leaderboard)
            {
                Hash = hash;
                Leaderboard = leaderboard;
            }

            internal string Hash { get; }

            internal Leaderboards Leaderboard { get; }

            public bool Equals(LoadKey other)
            {
                return Leaderboard == other.Leaderboard
                    && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object? obj)
            {
                return obj is LoadKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.OrdinalIgnoreCase.GetHashCode(Hash) * 397) ^ (int)Leaderboard;
                }
            }
        }
    }
}
