using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Safety;
using BLPPCounter.Utils.Serializable_Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Zenject;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal sealed class Targeter : ITargeter, IInitializable, IDisposable
    {
        public const string NO_TARGET = "None";
        public const int MAX_LIST_LENGTH = 100;

        private readonly PluginConfig _config;
        private readonly BLAPI _beatLeaderApi;
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly IBackgroundTaskRunner _taskRunner;
        private readonly CancellationTokenSource _lifetimeSource = new();
        private readonly object _stateLock = new();

        private readonly List<CustomTarget> _clanSource = [];
        private readonly List<CustomTarget> _followerSource = [];

        private Task _initializationTask = Task.CompletedTask;
        private TargeterInfo? _target;
        private bool _initializationStarted;
        private int _disposed;

        public Targeter(
            PluginConfig config,
            BLAPI beatLeaderApi,
            IMainThreadDispatcher dispatcher,
            IBackgroundTaskRunner taskRunner
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _beatLeaderApi = beatLeaderApi ?? throw new ArgumentNullException(nameof(beatLeaderApi));

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
        }

        public event Action? Initialized;

        public event Action? TargetListReloaded;

        public bool IsInitialized
        {
            get
            {
                lock (_stateLock)
                    return _target is not null;
            }
        }

        public TargeterInfo Target
        {
            get
            {
                lock (_stateLock)
                {
                    return _target ?? throw new InvalidOperationException("Targeter has not completed initialization.");
                }
            }
        }

        public Task InitializationTask
        {
            get
            {
                lock (_stateLock)
                {
                    if (!_initializationStarted)
                    {
                        throw new InvalidOperationException("Targeter.Initialize has not been called.");
                    }

                    return _initializationTask;
                }
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
                _initializationTask = InitializeAsync(_lifetimeSource.Token);
            }

            _taskRunner.Forget(_initializationTask, "Initialize target information");
        }

        public async Task WaitUntilInitializedAsync(CancellationToken cancellationToken = default)
        {
            Task initializationTask = InitializationTask;

            if (!cancellationToken.CanBeCanceled)
            {
                await initializationTask.ConfigureAwait(false);
                return;
            }

            TaskCompletionSource<object?> cancellationSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
                cancellationSource.TrySetCanceled(cancellationToken)
            );

            Task completedTask = await Task.WhenAny(initializationTask, cancellationSource.Task).ConfigureAwait(false);

            await completedTask.ConfigureAwait(false);
        }

        public async Task ReloadCustomPlayersAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            CustomTarget[] currentTargets = await _dispatcher
                .InvokeAsync(() => _config.CustomTargets.ToArray(), cancellationToken)
                .ConfigureAwait(false);

            List<CustomTarget> refreshed = await ResolveTargetRanksAsync(
                    currentTargets,
                    preserveExistingOnFailure: true,
                    cancellationToken
                )
                .ConfigureAwait(false);

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        _config.CustomTargets = refreshed;
                        PublishSnapshot();
                        TargetListReloaded?.Invoke();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        public async Task ReloadFollowersAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            TargeterInfo current = Target;

            bool useSteamFriends = await _dispatcher
                .InvokeAsync(() => _config.UseSteamFriends, cancellationToken)
                .ConfigureAwait(false);

            List<CustomTarget>? followers = await FetchFollowersAsync(
                    current.PlayerID,
                    useSteamFriends,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (followers is null)
            {
                Plugin.Log.Warn("Follower refresh failed; retaining the existing list.");

                return;
            }

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        _followerSource.Clear();
                        _followerSource.AddRange(followers);

                        PublishSnapshot();
                        TargetListReloaded?.Invoke();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        public async Task SaveAllAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            List<CustomTarget> followers = await _dispatcher
                .InvokeAsync(CreateFollowerPersistenceSnapshot, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? directory = Path.GetDirectoryName(HelpfulPaths.TARGET_DATA);

                if (!BetterString.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                using StreamWriter writer = new(HelpfulPaths.TARGET_DATA);

                JsonSerializer serializer = new();
                serializer.Serialize(writer, followers);
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Failed to save follower targets:\n{exception}");
            }
        }

        public async Task AddTargetAsync(CustomTarget target, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        int existingIndex = _config.CustomTargets.FindIndex(existing => existing.ID == target.ID);

                        if (existingIndex >= 0)
                            _config.CustomTargets[existingIndex] = target;
                        else
                            _config.CustomTargets.Add(target);

                        SortTargets(_config.CustomTargets);

                        PublishSnapshot();
                        TargetListReloaded?.Invoke();
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        public Task AddTargetAsync(string name, string id, int rank, CancellationToken cancellationToken = default)
        {
            if (!long.TryParse(id, out long parsedId))
            {
                throw new ArgumentException($"\"{id}\" is not a valid player ID.", nameof(id));
            }

            return AddTargetAsync(new CustomTarget(name, parsedId, rank), cancellationToken);
        }

        public async Task<bool> DeleteTargetAsync(string id, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            if (!long.TryParse(id, out long parsedId))
                return false;

            return await _dispatcher
                .InvokeAsync(
                    () =>
                    {
                        int index = _config.CustomTargets.FindIndex(target => target.ID == parsedId);

                        if (index < 0)
                            return false;

                        _config.CustomTargets.RemoveAt(index);

                        if (_config.TargetID == parsedId)
                            ResetTarget();

                        PublishSnapshot();
                        TargetListReloaded?.Invoke();

                        return true;
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        public void SetTarget(string name, long id)
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (BetterString.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A target name is required.", nameof(name));
            }

            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "A target ID must be positive.");
            }

            _config.Target = name;
            _config.TargetID = id;

            PublishSnapshot();
        }

        public void SetTarget(CustomTarget target)
        {
            SetTarget(target.Name, target.ID);
        }

        public void ResetTarget()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _config.Target = NO_TARGET;
            _config.TargetID = -1;

            PublishSnapshot();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string playerId = await GetCurrentPlayerIdAsync(cancellationToken).ConfigureAwait(false);

                (string clanJson, string playerName) = await RequestClanAsync(playerId, cancellationToken)
                    .ConfigureAwait(false);

                List<CustomTarget> clanTargets = ParseClanTargets(clanJson);

                List<CustomTarget>? cachedFollowers = TryLoadCachedFollowers();

                bool useSteamFriends = await _dispatcher
                    .InvokeAsync(() => _config.UseSteamFriends, cancellationToken)
                    .ConfigureAwait(false);

                List<CustomTarget> followers =
                    cachedFollowers
                    ?? await FetchFollowersAsync(playerId, useSteamFriends, cancellationToken).ConfigureAwait(false)
                    ?? [];

                CustomTarget[] configuredTargets = await _dispatcher
                    .InvokeAsync(() => _config.CustomTargets.ToArray(), cancellationToken)
                    .ConfigureAwait(false);

                List<CustomTarget> customTargets = await ResolveTargetRanksAsync(
                        configuredTargets,
                        preserveExistingOnFailure: false,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                await _dispatcher
                    .InvokeAsync(
                        () =>
                        {
                            _clanSource.Clear();
                            _clanSource.AddRange(clanTargets);

                            _followerSource.Clear();
                            _followerSource.AddRange(followers);

                            _config.CustomTargets = customTargets;

                            PublishSnapshot(playerId, playerName);

                            Initialized?.Invoke();
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                await EnsureConfiguredTargetExistsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Plugin.Log.Debug("Target initialization was cancelled.");

                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Failed to initialize Targeter:\n{exception}");

                throw;
            }
        }

        private async Task EnsureConfiguredTargetExistsAsync(CancellationToken cancellationToken)
        {
            (long Id, string Name) = await _dispatcher
                .InvokeAsync(() => (_config.TargetID, _config.Target), cancellationToken)
                .ConfigureAwait(false);

            if (Id < 0)
            {
                await _dispatcher.InvokeAsync(ResetTarget, cancellationToken).ConfigureAwait(false);

                return;
            }

            TargeterInfo current = Target;
            string id = Id.ToString();

            if (
                current.IDtoNames.TryGetValue(id, out string? knownName)
                && string.Equals(knownName, Name, StringComparison.Ordinal)
            )
            {
                return;
            }

            CustomTarget? resolved = await ResolveTargetAsync(Id, cancellationToken).ConfigureAwait(false);

            if (resolved is null)
            {
                Plugin.Log.Warn($"Could not resolve saved target ID " + $"\"{Id}\".");

                await _dispatcher.InvokeAsync(ResetTarget, cancellationToken).ConfigureAwait(false);

                return;
            }

            await AddTargetAsync(resolved.Value, cancellationToken).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() => SetTarget(resolved.Value), cancellationToken).ConfigureAwait(false);
        }

        private async Task<CustomTarget?> ResolveTargetAsync(long id, CancellationToken cancellationToken)
        {
            string? json = await _beatLeaderApi
                .CallAPI_String(
                    string.Format(HelpfulPaths.BLAPI_USERID, id),
                    quiet: true,
                    maxRetries: 1,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (BetterString.IsNullOrWhiteSpace(json))
                return null;

            JToken player = JToken.Parse(json!);

            long playerId = player.Value<long?>("id") ?? id;
            int rank = player.Value<int?>("rank") ?? -1;
            string name = player.Value<string>("name") ?? playerId.ToString();

            return new CustomTarget(name, playerId, rank);
        }

        private async Task<List<CustomTarget>> ResolveTargetRanksAsync(
            IEnumerable<CustomTarget> targets,
            bool preserveExistingOnFailure,
            CancellationToken cancellationToken
        )
        {
            List<CustomTarget> resolved = [];

            foreach (CustomTarget target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!preserveExistingOnFailure && target.Rank is > 0 or -2)
                {
                    resolved.Add(target);
                    continue;
                }

                try
                {
                    CustomTarget? refreshed = await ResolveTargetAsync(target.ID, cancellationToken)
                        .ConfigureAwait(false);

                    if (refreshed is not null)
                    {
                        resolved.Add(refreshed.Value);
                        continue;
                    }

                    resolved.Add(preserveExistingOnFailure ? target : new CustomTarget(target.Name, target.ID, -2));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Warn($"Could not refresh custom target " + $"\"{target.ID}\": {exception.Message}");

                    resolved.Add(preserveExistingOnFailure ? target : new CustomTarget(target.Name, target.ID, -2));
                }
            }

            SortTargets(resolved);
            return resolved;
        }

        private async Task<List<CustomTarget>?> FetchFollowersAsync(
            string playerId,
            bool useSteamFriends,
            CancellationToken cancellationToken
        )
        {
            try
            {
                List<CustomTarget> followers = await RequestFollowersAsync(playerId, cancellationToken)
                    .ConfigureAwait(false);

                if (useSteamFriends)
                {
                    IReadOnlyList<string> steamFriendIds = await GetSteamFriendIdsAsync(cancellationToken)
                        .ConfigureAwait(false);

                    HashSet<long> existingIds = [.. followers.Select(target => target.ID)];

                    foreach (string id in steamFriendIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!long.TryParse(id, out long parsedId) || existingIds.Contains(parsedId))
                        {
                            continue;
                        }

                        CustomTarget? target = await ResolveTargetAsync(parsedId, cancellationToken)
                            .ConfigureAwait(false);

                        if (target is null || target.Value.Rank == 0)
                        {
                            continue;
                        }

                        followers.Add(target.Value);
                        existingIds.Add(parsedId);
                    }
                }

                SortTargets(followers);
                return followers;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Failed to retrieve follower information:\n" + exception);

                return null;
            }
        }

        private async Task<List<CustomTarget>> RequestFollowersAsync(
            string playerId,
            CancellationToken cancellationToken
        )
        {
            List<CustomTarget> followers = [];

            int page = 1;

            while (followers.Count < MAX_LIST_LENGTH)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int remaining = MAX_LIST_LENGTH - followers.Count;

                string? json = await _beatLeaderApi
                    .CallAPI_String(
                        string.Format(HelpfulPaths.BLAPI_FOLLOWERS, playerId, page, remaining),
                        quiet: true,
                        ct: cancellationToken
                    )
                    .ConfigureAwait(false);

                if (BetterString.IsNullOrWhiteSpace(json))
                    break;

                JToken root = JToken.Parse(json!);
                JToken? data = root["data"] ?? root;

                List<CustomTarget> pageTargets =
                [
                    .. data.Children()
                        .Select(player => new CustomTarget(
                            player.Value<string>("name") ?? "Unknown",
                            player.Value<long?>("id") ?? 0,
                            player.Value<int?>("rank") ?? -1
                        ))
                        .Where(target => target.ID > 0),
                ];

                if (pageTargets.Count == 0)
                    break;

                followers.AddRange(pageTargets);
                page++;
            }

            return [.. followers.GroupBy(target => target.ID).Select(group => group.First()).Take(MAX_LIST_LENGTH)];
        }

        private async Task<(string Data, string PlayerName)> RequestClanAsync(
            string playerId,
            CancellationToken cancellationToken
        )
        {
            try
            {
                string? playerJson = await _beatLeaderApi
                    .CallAPI_String(string.Format(HelpfulPaths.BLAPI_USERID, playerId), ct: cancellationToken)
                    .ConfigureAwait(false);

                if (BetterString.IsNullOrWhiteSpace(playerJson))
                    return ("", "");

                JToken player = JToken.Parse(playerJson!);

                string playerName = player.Value<string>("name") ?? "";

                string? clan = player.Value<string>("clanOrder")?.Split(',').FirstOrDefault();

                if (BetterString.IsNullOrWhiteSpace(clan))
                    return ("", playerName);

                string? clanJson = await _beatLeaderApi
                    .CallAPI_String(
                        string.Format(HelpfulPaths.BLAPI_CLAN_PLAYERS, clan, MAX_LIST_LENGTH),
                        ct: cancellationToken
                    )
                    .ConfigureAwait(false);

                return (clanJson ?? "", playerName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Warn("An error occurred while retrieving clan data: " + exception.Message);

                Plugin.Log.Debug(exception);
                return ("", "");
            }
        }

        private async Task<IReadOnlyList<string>> GetSteamFriendIdsAsync(CancellationToken cancellationToken)
        {
            Task<IReadOnlyList<string>> operation = await _dispatcher
                .InvokeAsync(
                    () => BS_Utils.Gameplay.GetUserInfo.GetPlatformUserModel().GetUserFriendsUserIds(false),
                    cancellationToken
                )
                .ConfigureAwait(false);

            IReadOnlyList<string>? friends = await operation.ConfigureAwait(false);

            return friends ?? [];
        }

        private async Task<string> GetCurrentPlayerIdAsync(CancellationToken cancellationToken)
        {
            Task<UserInfo> operation = await _dispatcher
                .InvokeAsync(BS_Utils.Gameplay.GetUserInfo.GetUserAsync, cancellationToken)
                .ConfigureAwait(false);

            UserInfo user = await operation.ConfigureAwait(false);

            if (BetterString.IsNullOrWhiteSpace(user.platformUserId))
            {
                throw new InvalidOperationException("The current platform user has no player ID.");
            }

            return user.platformUserId;
        }

        private static List<CustomTarget> ParseClanTargets(string clanJson)
        {
            if (BetterString.IsNullOrWhiteSpace(clanJson))
                return [];

            try
            {
                JToken root = JToken.Parse(clanJson);
                JToken? data = root["data"] ?? root;

                List<CustomTarget> targets =
                [
                    .. data.Children()
                        .Select(player => new CustomTarget(
                            player.Value<string>("name") ?? "Unknown",
                            player.Value<long?>("id") ?? 0,
                            player.Value<int?>("rank") ?? -1
                        ))
                        .Where(target => target.ID > 0),
                ];

                SortTargets(targets);
                return targets;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Failed to parse clan target data:\n" + exception);

                return [];
            }
        }

        private List<CustomTarget>? TryLoadCachedFollowers()
        {
            if (!File.Exists(HelpfulPaths.TARGET_DATA))
                return null;

            try
            {
                using StreamReader reader = File.OpenText(HelpfulPaths.TARGET_DATA);

                JsonSerializer serializer = new();

                if (serializer.Deserialize(reader, typeof(List<CustomTarget>)) is not List<CustomTarget> targets)
                {
                    throw new JsonSerializationException("Follower cache deserialized as null.");
                }

                SortTargets(targets);
                return targets;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("Failed to load cached follower targets.");

                Plugin.Log.Error(exception);

                if (exception is JsonSerializationException)
                {
                    Plugin.Log.Error($"Deleting malformed file " + $"\"{HelpfulPaths.TARGET_DATA}\".");

                    File.Delete(HelpfulPaths.TARGET_DATA);
                }

                return null;
            }
        }

        private List<CustomTarget> CreateFollowerPersistenceSnapshot()
        {
            MainThreadDispatcher.AssertOnMainThread();

            return
            [
                .. _followerSource
                    .Where(target => target.Rank >= 0)
                    .Select(target => new CustomTarget(target.Name, target.ID, target.Rank)),
            ];
        }

        private void PublishSnapshot(string? playerId = null, string? playerName = null)
        {
            MainThreadDispatcher.AssertOnMainThread();

            TargeterInfo? previous;

            lock (_stateLock)
                previous = _target;

            playerId ??= previous?.PlayerID ?? "";
            playerName ??= previous?.PlayerName ?? "";

            Dictionary<long, CustomTarget> uniqueTargets = [];

            AddUniqueTargets(uniqueTargets, _clanSource);
            AddUniqueTargets(uniqueTargets, _followerSource);
            AddUniqueTargets(uniqueTargets, _config.CustomTargets);

            Dictionary<string, string> idToNames = uniqueTargets.Values.ToDictionary(
                target => target.ID.ToString(),
                target => target.Name
            );

            HashSet<long> usedIds = [.. uniqueTargets.Keys];

            TargetPair[] clanTargets = BuildTargetPairs(_clanSource);

            TargetPair[] followerTargets = BuildTargetPairs(_followerSource);

            TargetPair[] customTargets = BuildTargetPairs(_config.CustomTargets);

            TargeterInfo snapshot = new(
                playerId,
                playerName,
                _config.TargetID,
                _config.Target,
                clanTargets,
                followerTargets,
                customTargets,
                usedIds,
                new ReadOnlyDictionary<string, string>(idToNames)
            );

            lock (_stateLock)
                _target = snapshot;
        }

        private static void AddUniqueTargets(
            IDictionary<long, CustomTarget> destination,
            IEnumerable<CustomTarget> targets
        )
        {
            foreach (CustomTarget target in targets)
            {
                if (target.ID <= 0)
                    continue;

                destination[target.ID] = target;
            }
        }

        private static TargetPair[] BuildTargetPairs(IEnumerable<CustomTarget> targets)
        {
            return
            [
                .. targets
                    .Where(target => target.ID > 0)
                    .GroupBy(target => target.ID)
                    .Select(group => group.First())
                    .OrderBy(target => target.Rank)
                    .Select(target => new TargetPair(target.ID.ToString(), target.Rank)),
            ];
        }

        private static void SortTargets(List<CustomTarget> targets)
        {
            targets.Sort((first, second) => first.Rank.CompareTo(second.Rank));
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(Targeter));
            }
        }
    }

    internal sealed class TargeterInfo
    {
        internal TargeterInfo(
            string playerId,
            string playerName,
            long configuredTargetId,
            string configuredTargetName,
            IReadOnlyList<TargetPair> clanTargets,
            IReadOnlyList<TargetPair> followerTargets,
            IReadOnlyList<TargetPair> customTargets,
            IReadOnlyCollection<long> usedIds,
            IReadOnlyDictionary<string, string> idToNames
        )
        {
            PlayerID = playerId;
            PlayerName = playerName;
            ConfiguredTargetID = configuredTargetId;
            ConfiguredTargetName = configuredTargetName;
            ClanTargets = clanTargets;
            FollowerTargets = followerTargets;
            CustomTargets = customTargets;
            UsedIDs = usedIds;
            IDtoNames = idToNames;
        }

        public string PlayerID { get; }

        public string PlayerName { get; }

        public long ConfiguredTargetID { get; }

        public string ConfiguredTargetName { get; }

        public IReadOnlyList<TargetPair> ClanTargets { get; }

        public IReadOnlyList<TargetPair> FollowerTargets { get; }

        public IReadOnlyList<TargetPair> CustomTargets { get; }

        public IReadOnlyCollection<long> UsedIDs { get; }

        public IReadOnlyDictionary<string, string> IDtoNames { get; }

        public string TargetID => ConfiguredTargetID < 0 ? PlayerID : ConfiguredTargetID.ToString();

        public string TargetName => ConfiguredTargetName == Targeter.NO_TARGET ? PlayerName : ConfiguredTargetName;
    }

    internal readonly record struct TargetPair(string Id, int Rank);
}
