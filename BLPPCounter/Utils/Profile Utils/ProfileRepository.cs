using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Utils.Enums;
using Newtonsoft.Json;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class ProfileRepository : IProfileRepository, IDisposable
    {
        private readonly object _stateLock = new();
        private readonly IProfileFactory _profileFactory;
        private readonly Dictionary<ProfileKey, Profile> _profiles = [];
        private readonly Dictionary<ProfileKey, Task<Profile>> _pendingCreations = [];
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly CancellationTokenSource _lifetimeSource = new();

        private bool _loaded;
        private int _disposed;

        public ProfileRepository(IProfileFactory profileFactory)
        {
            _profileFactory = profileFactory ?? throw new ArgumentNullException(nameof(profileFactory));
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeSource.Token,
                cancellationToken
            );

            await _loadLock.WaitAsync(linkedSource.Token).ConfigureAwait(false);

            try
            {
                lock (_stateLock)
                {
                    if (_loaded)
                        return;
                }

                if (!File.Exists(HelpfulPaths.PROFILE_DATA))
                {
                    lock (_stateLock)
                        _loaded = true;

                    return;
                }

                string json;

                using (StreamReader reader = File.OpenText(HelpfulPaths.PROFILE_DATA))
                {
                    json = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                linkedSource.Token.ThrowIfCancellationRequested();

                if (BetterString.IsNullOrWhiteSpace(json))
                {
                    lock (_stateLock)
                        _loaded = true;

                    return;
                }

                JsonSerializerSettings settings = CreateSerializerSettings();

                List<ProfilePersistenceData> data =
                    JsonConvert.DeserializeObject<List<ProfilePersistenceData>>(json, settings) ?? [];

                Dictionary<ProfileKey, Profile> restored = [];

                foreach (ProfilePersistenceData profileData in data)
                {
                    linkedSource.Token.ThrowIfCancellationRequested();

                    try
                    {
                        Profile profile = _profileFactory.Restore(profileData);

                        restored[profile.Key] = profile;
                    }
                    catch (Exception exception)
                    {
                        Plugin.Log.Error(
                            "Failed to restore profile "
                                + $"\"{profileData.UserID}\" "
                                + $"for {profileData.Leaderboard}:\n"
                                + exception
                        );
                    }
                }

                lock (_stateLock)
                {
                    foreach (KeyValuePair<ProfileKey, Profile> pair in restored)
                    {
                        _profiles[pair.Key] = pair.Value;
                    }

                    _loaded = true;
                }
            }
            catch (JsonException exception)
            {
                Plugin.Log.Error("Profiles failed to deserialize:\n" + exception);

                TryDeleteMalformedProfileFile();

                lock (_stateLock)
                    _loaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeSource.Token,
                cancellationToken
            );

            await LoadAsync(linkedSource.Token).ConfigureAwait(false);

            await _saveLock.WaitAsync(linkedSource.Token).ConfigureAwait(false);

            try
            {
                Profile[] profiles;

                lock (_stateLock)
                {
                    profiles = [.. _profiles.Values];
                }

                if (profiles.Length == 0)
                    return;

                List<ProfilePersistenceData> data = [.. profiles.Select(profile => profile.CreatePersistenceData())];

                string? directory = Path.GetDirectoryName(HelpfulPaths.PROFILE_DATA);

                if (!BetterString.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string temporaryPath = HelpfulPaths.PROFILE_DATA + ".tmp";

                JsonSerializer serializer = JsonSerializer.Create(CreateSerializerSettings());

                using (StreamWriter writer = new(temporaryPath))
                using (JsonTextWriter jsonWriter = new(writer) { Formatting = Formatting.Indented })
                {
                    serializer.Serialize(jsonWriter, data);

                    await jsonWriter.FlushAsync(linkedSource.Token).ConfigureAwait(false);
                }

                linkedSource.Token.ThrowIfCancellationRequested();

                if (File.Exists(HelpfulPaths.PROFILE_DATA))
                {
                    File.Delete(HelpfulPaths.PROFILE_DATA);
                }

                File.Move(temporaryPath, HelpfulPaths.PROFILE_DATA);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        public async Task<Profile> GetAsync(
            Leaderboards leaderboard,
            string userId,
            APCategory accSaberCategory = APCategory.None,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfDisposed();

            await LoadAsync(cancellationToken).ConfigureAwait(false);

            ProfileKey key = new(leaderboard, userId, NormalizeCategory(leaderboard, accSaberCategory));

            Task<Profile> creationTask;

            lock (_stateLock)
            {
                if (_profiles.TryGetValue(key, out Profile? existing))
                {
                    return existing;
                }

                if (!_pendingCreations.TryGetValue(key, out creationTask!))
                {
                    creationTask = CreateAndStoreAsync(key, cancellationToken);

                    _pendingCreations[key] = creationTask;
                }
            }

            return await creationTask.ConfigureAwait(false);
        }

        public bool TryGet(Leaderboards leaderboard, string userId, APCategory accSaberCategory, out Profile? profile)
        {
            ThrowIfDisposed();

            ProfileKey key = new(leaderboard, userId, NormalizeCategory(leaderboard, accSaberCategory));

            lock (_stateLock)
            {
                return _profiles.TryGetValue(key, out profile);
            }
        }

        public IReadOnlyList<Profile> GetLoadedProfilesSnapshot()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                return _profiles.Values.ToArray();
            }
        }

        public async Task ReloadAsync(Profile profile, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            await _profileFactory.ReloadAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _lifetimeSource.Cancel();

            _lifetimeSource.Dispose();
            _loadLock.Dispose();
            _saveLock.Dispose();
        }

        private async Task<Profile> CreateAndStoreAsync(ProfileKey key, CancellationToken cancellationToken)
        {
            try
            {
                using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetimeSource.Token
                );

                Profile profile = await _profileFactory.CreateAsync(key, linkedSource.Token).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                lock (_stateLock)
                {
                    if (_profiles.TryGetValue(key, out Profile? existing))
                    {
                        return existing;
                    }

                    _profiles[key] = profile;
                }

                return profile;
            }
            finally
            {
                lock (_stateLock)
                    _pendingCreations.Remove(key);
            }
        }

        private static APCategory NormalizeCategory(Leaderboards leaderboard, APCategory category)
        {
            if (leaderboard != Leaderboards.Accsaber)
            {
                return APCategory.None;
            }

            return category == APCategory.None ? APCategory.All : category;
        }

        private static JsonSerializerSettings CreateSerializerSettings()
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            };

            settings.Converters.Add(new ProfileUnusualModeDataConverter());

            return settings;
        }

        private static void TryDeleteMalformedProfileFile()
        {
            try
            {
                if (File.Exists(HelpfulPaths.PROFILE_DATA))
                {
                    File.Delete(HelpfulPaths.PROFILE_DATA);
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.Error("Failed to delete malformed profile file:\n" + exception);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(ProfileRepository));
            }
        }
    }
}
