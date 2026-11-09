using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Counters
{
    internal sealed class ClanService : IClanService, IDisposable
    {
        private readonly object _stateLock = new();

        private readonly PluginConfig _config;
        private readonly BLAPI _beatLeaderApi;
        private readonly BLCalc _calculator;
        private readonly ITargeter _targeter;

        private readonly CancellationTokenSource _lifetimeSource = new();

        private readonly Dictionary<ClanMapKey, ClanCaptureResult> _cache = [];

        private readonly LinkedList<ClanMapKey> _cacheOrder = [];

        private readonly Dictionary<ClanRequestKey, Task<ClanCaptureResult>> _pendingRequests = [];

        private readonly Dictionary<string, int> _playerClanIds = [with(StringComparer.Ordinal)];

        private int _disposed;

        public ClanService(PluginConfig config, BLAPI beatLeaderApi, BLCalc calculator, ITargeter targeter)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _beatLeaderApi = beatLeaderApi ?? throw new ArgumentNullException(nameof(beatLeaderApi));

            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));
        }

        public async Task<ClanCaptureResult> GetCaptureAsync(
            MapSelection map,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();

            if (!map.IsUsable)
            {
                return ClanCaptureResult.LoadFailed();
            }

            if (TryGetCached(map, out ClanCaptureResult? cached))
            {
                return cached;
            }

            ClanCaptureResult loaded = await GetCaptureAsync(map.MapData.SongId, cancellationToken)
                .ConfigureAwait(false);

            if (loaded.IsSuccessful)
                AddToCache(map, loaded);

            return loaded.CreateSnapshot();
        }

        public async Task<ClanCaptureResult> GetCaptureAsync(
            string mapId,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();

            if (BetterString.IsNullOrWhiteSpace(mapId))
            {
                throw new ArgumentException("A BeatLeader map ID is required.", nameof(mapId));
            }

            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            string playerId = _targeter.Target.TargetID;

            ClanRequestKey requestKey = new(playerId, mapId);

            Task<ClanCaptureResult> requestTask;

            lock (_stateLock)
            {
                if (!_pendingRequests.TryGetValue(requestKey, out requestTask!))
                {
                    requestTask = LoadAndPublishAsync(requestKey, _lifetimeSource.Token);

                    _pendingRequests.Add(requestKey, requestTask);
                }
            }

            ClanCaptureResult result = await AwaitWithCancellationAsync(requestTask, cancellationToken)
                .ConfigureAwait(false);

            return result.CreateSnapshot();
        }

        public bool TryGetCached(MapSelection map, [NotNullWhen(true)] out ClanCaptureResult? result)
        {
            ThrowIfDisposed();

            if (!map.IsUsable)
            {
                result = null;
                return false;
            }

            ClanMapKey key = ClanMapKey.From(map);

            lock (_stateLock)
            {
                if (!_cache.TryGetValue(key, out ClanCaptureResult? cached))
                {
                    result = null;
                    return false;
                }

                MoveCacheEntryToEnd(key);

                result = cached.CreateSnapshot();

                return true;
            }
        }

        public void ClearCache()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                _cache.Clear();
                _cacheOrder.Clear();
                _playerClanIds.Clear();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();

            lock (_stateLock)
            {
                _cache.Clear();
                _cacheOrder.Clear();
                _pendingRequests.Clear();
                _playerClanIds.Clear();
            }
        }

        private async Task<ClanCaptureResult> LoadAndPublishAsync(
            ClanRequestKey requestKey,
            CancellationToken cancellationToken
        )
        {
            try
            {
                return await LoadAsync(requestKey.PlayerId, requestKey.MapId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("Failed to load clan capture data for " + $"map {requestKey.MapId}:\n{exception}");

                return ClanCaptureResult.LoadFailed();
            }
            finally
            {
                lock (_stateLock)
                {
                    _pendingRequests.Remove(requestKey);
                }
            }
        }

        private async Task<ClanCaptureResult> LoadAsync(
            string playerId,
            string mapId,
            CancellationToken cancellationToken
        )
        {
            int playerClanId = await GetPlayerClanIdAsync(playerId, cancellationToken).ConfigureAwait(false);

            string? rankingResponse = await _beatLeaderApi
                .CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_CLAN, mapId) + "?page=1&count=1",
                    quiet: true,
                    maxRetries: 1,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrWhiteSpace(rankingResponse))
            {
                return ClanCaptureResult.LoadFailed(playerClanId);
            }

            JToken rankingRoot = JToken.Parse(rankingResponse);

            int status = rankingRoot.SelectToken("difficulty.status")?.Value<int?>() ?? 0;

            if (status != 3)
            {
                return ClanCaptureResult.Unranked(playerClanId);
            }

            JToken? ownerRanking = rankingRoot["clanRanking"]?.Children().FirstOrDefault();

            if (ownerRanking is null)
            {
                return ClanCaptureResult.LoadFailed(playerClanId);
            }

            JToken? ownerClan = ownerRanking["clan"];

            int ownerClanId = ownerClan?.Value<int?>("id") ?? -1;

            string owningClan = ownerClan?.Value<string>("tag") ?? "None";

            float owningClanPp = ownerRanking.Value<float?>("pp") ?? 0f;

            bool mapCaptured = ownerClanId <= 0 || ownerClanId == playerClanId;

            if (playerClanId <= 0)
            {
                return new ClanCaptureResult(
                    mapCaptured ? ClanCaptureStatus.AlreadyCaptured : ClanCaptureStatus.Success,
                    mapCaptured ? 0f : owningClanPp,
                    [],
                    owningClan,
                    playerClanId
                );
            }

            string? clanScoresResponse = await _beatLeaderApi
                .CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_CLAN, mapId) + $"/clan/{playerClanId}" + "?count=100&page=1",
                    quiet: true,
                    maxRetries: 1,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrWhiteSpace(clanScoresResponse))
            {
                return new ClanCaptureResult(
                    mapCaptured ? ClanCaptureStatus.AlreadyCaptured : ClanCaptureStatus.Success,
                    mapCaptured ? 0f : owningClanPp,
                    [],
                    owningClan,
                    playerClanId
                );
            }

            JToken clanScoresRoot = JToken.Parse(clanScoresResponse);

            List<float> allClanScores = [];
            float playerPp = 0f;

            foreach (JToken score in clanScoresRoot["associatedScores"]?.Children() ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                float pp = score.Value<float?>("pp") ?? 0f;

                if (pp <= 0f)
                    continue;

                allClanScores.Add(pp);

                if (string.Equals(score.Value<string>("playerId"), playerId, StringComparison.Ordinal))
                {
                    playerPp = pp;
                }
            }

            List<float> otherClanScores = [.. allClanScores];

            if (playerPp > 0f)
            {
                otherClanScores.Remove(playerPp);
            }

            otherClanScores.Sort((first, second) => second.CompareTo(first));

            float neededPp = mapCaptured
                ? 0f
                : _calculator.GetNeededPlay([with(allClanScores)], owningClanPp, playerPp);

            return new ClanCaptureResult(
                mapCaptured ? ClanCaptureStatus.AlreadyCaptured : ClanCaptureStatus.Success,
                neededPp,
                otherClanScores,
                owningClan,
                playerClanId
            );
        }

        private async Task<int> GetPlayerClanIdAsync(string playerId, CancellationToken cancellationToken)
        {
            lock (_stateLock)
            {
                if (_playerClanIds.TryGetValue(playerId, out int cached))
                {
                    return cached;
                }
            }

            string? response = await _beatLeaderApi
                .CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_USERID_FULL, playerId),
                    quiet: true,
                    maxRetries: 1,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrWhiteSpace(response))
            {
                return -1;
            }

            int clanId = ParseClanId(JToken.Parse(response));

            lock (_stateLock)
            {
                _playerClanIds[playerId] = clanId;
            }

            return clanId;
        }

        private static int ParseClanId(JToken playerData)
        {
            JToken[] clans = [.. playerData["clans"]?.Children() ?? []];

            if (clans.Length == 0)
                return -1;

            if (clans.Length == 1)
            {
                return clans[0].Value<int?>("id") ?? -1;
            }

            string? preferredTag = playerData.Value<string>("clanOrder")?.Split(',').FirstOrDefault();

            if (BetterString.IsNullOrWhiteSpace(preferredTag))
            {
                return clans[0].Value<int?>("id") ?? -1;
            }

            JToken? preferredClan = clans.FirstOrDefault(clan =>
                string.Equals(clan.Value<string>("tag"), preferredTag, StringComparison.Ordinal)
            );

            return preferredClan?.Value<int?>("id") ?? clans[0].Value<int?>("id") ?? -1;
        }

        private void AddToCache(MapSelection map, ClanCaptureResult result)
        {
            int maximumEntries = Math.Max(0, _config.MapCache);

            if (maximumEntries == 0)
                return;

            ClanMapKey key = ClanMapKey.From(map);

            lock (_stateLock)
            {
                _cache[key] = result.CreateSnapshot();

                MoveCacheEntryToEnd(key);

                while (_cache.Count > maximumEntries)
                {
                    LinkedListNode<ClanMapKey>? first = _cacheOrder.First;

                    if (first is null)
                        break;

                    _cacheOrder.RemoveFirst();
                    _cache.Remove(first.Value);
                }
            }
        }

        private void MoveCacheEntryToEnd(ClanMapKey key)
        {
            LinkedListNode<ClanMapKey>? existing = _cacheOrder.Find(key);

            if (existing is not null)
                _cacheOrder.Remove(existing);

            _cacheOrder.AddLast(key);
        }

        private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task.ConfigureAwait(false);
            }

            TaskCompletionSource<T> cancellationSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                cancellationSource.TrySetCanceled(cancellationToken)
            );

            Task<T> completed = await Task.WhenAny(task, cancellationSource.Task).ConfigureAwait(false);

            return await completed.ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ClanService));
            }
        }

        private readonly struct ClanRequestKey : IEquatable<ClanRequestKey>
        {
            internal ClanRequestKey(string playerId, string mapId)
            {
                PlayerId = playerId;
                MapId = mapId;
            }

            internal string PlayerId { get; }

            internal string MapId { get; }

            public bool Equals(ClanRequestKey other)
            {
                return string.Equals(PlayerId, other.PlayerId, StringComparison.Ordinal)
                    && string.Equals(MapId, other.MapId, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is ClanRequestKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(PlayerId) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(MapId);
                }
            }
        }

        private readonly struct ClanMapKey : IEquatable<ClanMapKey>
        {
            private ClanMapKey(string hash, BeatmapDifficulty difficulty, string mode)
            {
                Hash = hash;
                Difficulty = difficulty;
                Mode = mode;
            }

            private string Hash { get; }

            private BeatmapDifficulty Difficulty { get; }

            private string Mode { get; }

            internal static ClanMapKey From(MapSelection map)
            {
                return new ClanMapKey(map.Hash, map.Difficulty, map.Mode);
            }

            public bool Equals(ClanMapKey other)
            {
                return Difficulty == other.Difficulty
                    && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Mode, other.Mode, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is ClanMapKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Hash);

                    hash = (hash * 397) ^ (int)Difficulty;

                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Mode);

                    return hash;
                }
            }
        }
    }
}
