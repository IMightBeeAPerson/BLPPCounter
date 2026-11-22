using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class Profile
    {
        public const string DEFAULT_MODE = "Standard";
        public const int PLAYS_TO_SHOW = 10;

        private static readonly float[] AccSaberConstants = CreateAccSaberConstants();

        private readonly object _stateLock = new();
        private readonly PluginConfig _config;
        private readonly ISessionFactory _sessionFactory;

        private List<Play> _scores;
        private float[] _weightedScores;
        private float _totalPp;
        private float _plusOne;
        private int _pageNumber = 1;

        internal Profile(
            ProfileKey key,
            IEnumerable<Play> scores,
            float totalPp,
            PluginConfig config,
            ISessionFactory sessionFactory
        )
        {
            Key = key;

            _config = config ?? throw new ArgumentNullException(nameof(config));

            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

            _scores =
            [
                .. (scores ?? throw new ArgumentNullException(nameof(scores)))
                    .OrderByDescending(play => play.Pp)
                    .Take(GetPlusOneCount(key.Leaderboard)),
            ];

            _totalPp = totalPp;
            _weightedScores = CreateWeightedScores(_scores);

            _plusOne = CalculatePlusOneInternal();

            CurrentSession = _sessionFactory.Create(key.Leaderboard, key.UserId, totalPp);
        }

        public event Action? Changed;

        public ProfileKey Key { get; }

        public Leaderboards Leaderboard => Key.Leaderboard;

        public string UserId => Key.UserId;

        public APCategory AccSaberCategory => Key.AccSaberCategory;

        public bool IsAccSaberOverall => Leaderboard == Leaderboards.Accsaber && AccSaberCategory == APCategory.All;

        public Session CurrentSession { get; private set; }

        public float TotalPp
        {
            get
            {
                lock (_stateLock)
                    return _totalPp;
            }
        }

        public float PlusOne
        {
            get
            {
                lock (_stateLock)
                    return _plusOne;
            }
        }

        public int ScoreCount
        {
            get
            {
                lock (_stateLock)
                    return _scores.Count;
            }
        }

        public int PageNumber
        {
            get
            {
                lock (_stateLock)
                    return _pageNumber;
            }
        }

        public string PpLabel => Leaderboard == Leaderboards.Accsaber ? "AP" : "PP";

        /// <summary>
        /// Returns a snapshot of every play currently stored in this profile.
        /// </summary>
        /// <remarks>
        /// The returned array is a copy of the profile's internal score collection, so
        /// callers may enumerate or modify the array without affecting profile state.
        /// </remarks>
        /// <returns>
        /// An array containing the profile's stored plays, ordered from highest to lowest raw PP/AP.
        /// </returns>
        public Play[] GetScoresSnapshot()
        {
            lock (_stateLock)
                return [.. _scores];
        }

        /// <summary>
        /// Returns an atomic snapshot of the profile's current state.
        /// </summary>
        /// <remarks>
        /// The snapshot includes the profile key, total PP/AP, <see cref="PlusOne"/>,
        /// current page number, and a copy of the stored plays.
        /// </remarks>
        /// <returns>A snapshot representing the profile's current state.</returns>
        public ProfileSnapshot GetSnapshot()
        {
            lock (_stateLock)
            {
                return new ProfileSnapshot(Key, _totalPp, _plusOne, _pageNumber, [.. _scores]);
            }
        }

        /// <summary>
        /// Returns the plays displayed on the currently selected page.
        /// </summary>
        /// <remarks>
        /// Each page contains at most <see cref="PLAYS_TO_SHOW"/> plays. The current
        /// page number is clamped to the valid range before the plays are retrieved.
        /// </remarks>
        /// <returns>
        /// A copied array of plays for the current page, or an empty array when no plays
        /// are available for that page.
        /// </returns>
        public Play[] GetCurrentPageSnapshot()
        {
            lock (_stateLock)
            {
                ClampPageNumber();

                int start = (_pageNumber - 1) * PLAYS_TO_SHOW;

                if (start >= _scores.Count)
                    return [];

                return [.. _scores.Skip(start).Take(PLAYS_TO_SHOW)];
            }
        }

        /// <summary>
        /// Moves the selected score page one page toward the beginning of the profile.
        /// </summary>
        /// <remarks>
        /// The page number cannot move below the first page. Raises <see cref="Changed"/>
        /// after updating the page selection.
        /// </remarks>
        public void PageUp()
        {
            lock (_stateLock)
            {
                _pageNumber--;
                ClampPageNumber();
            }

            NotifyChanged();
        }

        /// <summary>
        /// Moves the selected score page one page toward the end of the profile.
        /// </summary>
        /// <remarks>
        /// The page number cannot move past the final available page. Raises
        /// <see cref="Changed"/> after updating the page selection.
        /// </remarks>
        public void PageDown()
        {
            lock (_stateLock)
            {
                _pageNumber++;
                ClampPageNumber();
            }

            NotifyChanged();
        }

        /// <summary>
        /// Moves the selected score page to the first page.
        /// </summary>
        /// <remarks>
        /// Raises <see cref="Changed"/> after updating the page selection.
        /// </remarks>
        public void PageTop()
        {
            lock (_stateLock)
                _pageNumber = 1;

            NotifyChanged();
        }

        /// <summary>
        /// Calculates the weighted PP/AP contribution of a prospective raw PP/AP score.
        /// </summary>
        /// <param name="rawPp">
        /// The unweighted PP/AP value of the prospective play.
        /// </param>
        /// <returns>
        /// The score's weighted PP/AP contribution at the position where it would be
        /// inserted into this profile; otherwise, <c>-1f</c> when <paramref name="rawPp"/>
        /// is invalid or cannot enter the leaderboard's stored top-play limit.
        /// </returns>
        /// <remarks>
        /// The score is rounded according to the configured decimal precision before
        /// its insertion position and weighting are calculated.
        /// </remarks>
        public float GetWeightedPp(float rawPp)
        {
            lock (_stateLock)
            {
                if (float.IsNaN(rawPp) || float.IsInfinity(rawPp) || rawPp <= 0f)
                {
                    return -1f;
                }

                float normalizedPp = Round(rawPp);

                if (_scores.Count >= GetPlusOneCount(Leaderboard) && _scores[^1].Pp >= normalizedPp)
                {
                    return -1f;
                }

                int index = FindInsertionIndex(normalizedPp);
                return Round(GetWeight(index + 1) * normalizedPp);
            }
        }

        /// <summary>
        /// Calculates the net increase to this profile's total PP/AP from a prospective
        /// weighted PP/AP value.
        /// </summary>
        /// <param name="weightedPp">
        /// The weighted PP/AP value to evaluate.
        /// </param>
        /// <returns>
        /// The total profile PP/AP gained by setting the prospective play, including
        /// reweighting of lower-ranked plays; otherwise, <c>-1f</c> when
        /// <paramref name="weightedPp"/> is invalid or cannot produce a score that enters
        /// the stored top plays.
        /// </returns>
        /// <remarks>
        /// This method returns the change in the profile total, not the resulting total
        /// profile PP/AP. The weighted value is converted to raw PP/AP based on its
        /// calculated weighted insertion position.
        /// </remarks>
        public float GetProfilePp(float weightedPp)
        {
            lock (_stateLock)
            {
                if (float.IsNaN(weightedPp) || float.IsInfinity(weightedPp) || weightedPp <= 0f)
                {
                    return -1f;
                }

                int insertionIndex = FindWeightedInsertionIndex(weightedPp);
                float rawPp = weightedPp / GetWeight(insertionIndex + 1);

                return GetProfilePpRawInternal(rawPp);
            }
        }

        /// <summary>
        /// Calculates the net increase to this profile's total PP/AP from a prospective
        /// raw PP/AP score.
        /// </summary>
        /// <param name="rawPp">
        /// The unweighted PP/AP value of the prospective play.
        /// </param>
        /// <returns>
        /// The total profile PP/AP gained by setting the prospective play, including
        /// the new play's weighted value, reweighting of lower plays, and replacement
        /// or displacement of existing top plays; otherwise, <c>-1f</c> when
        /// <paramref name="rawPp"/> is invalid or cannot enter the stored top plays.
        /// </returns>
        /// <remarks>
        /// This method returns the net change to <see cref="TotalPp"/>, rather than the
        /// resulting total profile PP/AP.
        /// </remarks>
        public float GetProfilePpRaw(float rawPp)
        {
            lock (_stateLock)
            {
                return GetProfilePpRawInternal(rawPp);
            }
        }

        /// <summary>
        /// Adds a new play to the profile, or replaces an existing matching play when
        /// the supplied score is an improvement.
        /// </summary>
        /// <param name="rawPp">
        /// The unweighted PP/AP earned by the play.
        /// </param>
        /// <param name="mapName">
        /// The display name of the beatmap.
        /// </param>
        /// <param name="hash">
        /// The beatmap hash used when recording the play in the current session.
        /// </param>
        /// <param name="mapKey">
        /// The leaderboard or BeatSaver key identifying the beatmap.
        /// </param>
        /// <param name="difficulty">
        /// The beatmap difficulty on which the play was set.
        /// </param>
        /// <param name="mode">
        /// The beatmap characteristic or mode. If null, empty, or whitespace, this is
        /// stored as <see cref="DEFAULT_MODE"/>.
        /// </param>
        /// <param name="accSaberCategory">
        /// The AccSaber category associated with the play, when applicable.
        /// </param>
        /// <param name="addToSession">
        /// <see langword="true"/> to also add the accepted play to <see cref="CurrentSession"/>;
        /// otherwise, <see langword="false"/> to update only this profile.
        /// </param>
        /// <param name="cancellationToken">
        /// A token used to cancel the asynchronous session-update operation.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the play was accepted and added or updated in the
        /// profile; otherwise, <see langword="false"/> when the PP/AP is invalid, the
        /// matching stored play is already equal or better, or the score cannot enter
        /// the profile's stored top plays.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="mapName"/>, <paramref name="hash"/>, or
        /// <paramref name="mapKey"/> is null, empty, or whitespace.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is cancelled before the
        /// profile update begins or while the session play is being recorded.
        /// </exception>
        /// <remarks>
        /// When a play is accepted, the profile total is updated by its net PP/AP gain.
        /// That gain accounts for the play's weighted contribution, reweighting of lower
        /// scores, replacement of an older matching score, and removal of a score pushed
        /// past the leaderboard's top-play limit. The <see cref="Changed"/> event is
        /// raised after the local profile update and again if an asynchronously retrieved
        /// session rank is applied.
        /// </remarks>
        public async Task<bool> AddPlayAsync(
            float rawPp,
            string mapName,
            string hash,
            string mapKey,
            BeatmapDifficulty difficulty,
            string mode,
            APCategory accSaberCategory = APCategory.None,
            bool addToSession = true,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (float.IsNaN(rawPp) || float.IsInfinity(rawPp) || rawPp <= 0f)
            {
                return false;
            }

            if (BetterString.IsNullOrWhiteSpace(mapName))
            {
                throw new ArgumentException("A map name is required.", nameof(mapName));
            }

            if (BetterString.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("A map hash is required.", nameof(hash));
            }

            if (BetterString.IsNullOrWhiteSpace(mapKey))
            {
                throw new ArgumentException("A map key is required.", nameof(mapKey));
            }

            mode = BetterString.IsNullOrWhiteSpace(mode) ? DEFAULT_MODE : mode;

            float normalizedPp = Round(rawPp);
            float profilePpGained;
            float oldPp;
            Play insertedPlay;

            lock (_stateLock)
            {
                int duplicateIndex = FindPlayIndex(mapKey, difficulty, mode);

                if (duplicateIndex >= 0 && _scores[duplicateIndex].Pp >= normalizedPp)
                {
                    return false;
                }

                if (
                    duplicateIndex < 0
                    && _scores.Count >= GetPlusOneCount(Leaderboard)
                    && _scores[^1].Pp >= normalizedPp
                )
                {
                    return false;
                }

                float weightedTotalBefore = _weightedScores.Sum();

                oldPp = duplicateIndex >= 0 ? _scores[duplicateIndex].Pp : 0f;

                if (duplicateIndex >= 0)
                {
                    _scores.RemoveAt(duplicateIndex);
                }

                int insertIndex = FindInsertionIndex(normalizedPp);

                insertedPlay = new Play(mapName, mapKey, difficulty, mode, normalizedPp, 0, 0f, Round(oldPp))
                {
                    AccSaberCategory = accSaberCategory,
                };

                _scores.Insert(insertIndex, insertedPlay);

                int maximumScores = GetPlusOneCount(Leaderboard);

                if (_scores.Count > maximumScores)
                {
                    _scores.RemoveRange(maximumScores, _scores.Count - maximumScores);
                }

                _weightedScores = CreateWeightedScores(_scores);

                float weightedTotalAfter = _weightedScores.Sum();

                // This is the complete net profile change, including:
                // - the new play's contribution;
                // - the changed weighting of lower plays;
                // - removal of an old improved score;
                // - removal of a play pushed out of the top 100.
                profilePpGained = Round(weightedTotalAfter - weightedTotalBefore);

                _totalPp = Round(_totalPp + profilePpGained);
                _plusOne = CalculatePlusOneInternal();
                ClampPageNumber();

                int storedPlayIndex = FindPlayIndex(mapKey, difficulty, mode);

                if (storedPlayIndex >= 0)
                {
                    Play storedPlay = _scores[storedPlayIndex];
                    storedPlay.ProfilePpGained = profilePpGained;
                    _scores[storedPlayIndex] = storedPlay;
                    insertedPlay = storedPlay;
                }
            }

            NotifyChanged();

            if (!addToSession)
            {
                return true;
            }

            uint rank = await CurrentSession
                .AddPlayAsync(
                    mapName,
                    hash,
                    mapKey,
                    difficulty,
                    mode,
                    normalizedPp,
                    0,
                    profilePpGained,
                    oldPp,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (rank == 0)
            {
                return true;
            }

            bool rankUpdated;

            lock (_stateLock)
            {
                int playIndex = FindPlayIndex(mapKey, difficulty, mode);

                rankUpdated = playIndex >= 0 && ApproximatelyEqual(_scores[playIndex].Pp, insertedPlay.Pp);

                if (rankUpdated)
                {
                    Play updatedPlay = _scores[playIndex];
                    updatedPlay.Rank = rank;
                    _scores[playIndex] = updatedPlay;
                }
            }

            if (rankUpdated)
            {
                NotifyChanged();
            }

            return true;
        }

        /// <summary>
        /// Calculates the net increase to the profile's total PP/AP produced by a
        /// prospective raw PP/AP score.
        /// </summary>
        /// <param name="rawPp">The unweighted PP/AP value of the prospective play.</param>
        /// <returns>
        /// The rounded change in total profile PP/AP after inserting the score and
        /// reweighting affected plays; or <c>-1f</c> if <paramref name="rawPp"/> is
        /// invalid or cannot enter the retained top-play list.
        /// </returns>
        /// <remarks>
        /// This method assumes the caller already holds <c>_stateLock</c>. The
        /// calculated gain includes the new play's weighted contribution, reweighting
        /// of lower-ranked plays, and displacement of a play when the profile is
        /// already at its maximum stored score count.
        /// </remarks>
        private float GetProfilePpRawInternal(float rawPp)
        {
            if (float.IsNaN(rawPp) || float.IsInfinity(rawPp) || rawPp <= 0f)
            {
                return -1f;
            }

            float normalizedPp = Round(rawPp);

            if (_scores.Count >= GetPlusOneCount(Leaderboard) && _scores[^1].Pp >= normalizedPp)
            {
                return -1f;
            }

            int insertionIndex = FindInsertionIndex(normalizedPp);
            int maximumScores = GetPlusOneCount(Leaderboard);

            float weightedTotalBefore = _weightedScores.Sum();
            float weightedTotalAfter = 0f;

            for (int index = 0; index < maximumScores; index++)
            {
                float pp;

                if (index == insertionIndex)
                {
                    pp = normalizedPp;
                }
                else
                {
                    int sourceIndex = index < insertionIndex ? index : index - 1;

                    if (sourceIndex < 0 || sourceIndex >= _scores.Count)
                    {
                        break;
                    }

                    pp = _scores[sourceIndex].Pp;
                }

                weightedTotalAfter += GetWeight(index + 1) * pp;
            }

            return Round(weightedTotalAfter - weightedTotalBefore);
        }

        /// <summary>
        /// Replaces the profile's stored plays and total PP/AP with newly loaded data.
        /// </summary>
        /// <param name="scores">
        /// The plays from which to rebuild the profile. They are sorted by descending
        /// raw PP/AP and trimmed to the leaderboard's supported top-play count.
        /// </param>
        /// <param name="totalPp">
        /// The total profile PP/AP associated with the replacement data.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="scores"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Replacing the data recalculates weighted scores and <see cref="PlusOne"/>,
        /// resets the selected page to the first page, and creates a new
        /// <see cref="CurrentSession"/> using the supplied total. The
        /// <see cref="Changed"/> event is raised after the replacement completes.
        /// </remarks>
        internal void ReplaceData(IEnumerable<Play> scores, float totalPp)
        {
            if (scores is null)
            {
                throw new ArgumentNullException(nameof(scores));
            }

            Session newSession;

            lock (_stateLock)
            {
                _scores = [.. scores.OrderByDescending(play => play.Pp).Take(GetPlusOneCount(Leaderboard))];

                _totalPp = totalPp;

                _weightedScores = CreateWeightedScores(_scores);

                _plusOne = CalculatePlusOneInternal();

                _pageNumber = 1;

                newSession = _sessionFactory.Create(Leaderboard, UserId, totalPp);

                CurrentSession = newSession;
            }

            NotifyChanged();
        }

        /// <summary>
        /// Creates a serializable representation of the profile's current state.
        /// </summary>
        /// <returns>
        /// Persistence data containing the profile identity, stored plays, total PP/AP,
        /// PlusOne value, ranks, difficulties, non-default modes, and applicable
        /// AccSaber categories.
        /// </returns>
        /// <remarks>
        /// Difficulty and, when applicable, AccSaber category values are compressed for
        /// storage. Only modes that differ from <see cref="DEFAULT_MODE"/> are stored
        /// explicitly; standard modes are restored implicitly when the data is loaded.
        /// </remarks>
        internal ProfilePersistenceData CreatePersistenceData()
        {
            lock (_stateLock)
            {
                BeatmapDifficulty[] difficulties = [.. _scores.Select(play => play.Difficulty)];

                APCategory[] categories = [.. _scores.Select(play => play.AccSaberCategory)];

                List<ProfileUnusualModeData> unusualModes = [];

                for (int index = 0; index < _scores.Count; index++)
                {
                    string mode = _scores[index].Mode;

                    if (string.Equals(mode, DEFAULT_MODE, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    unusualModes.Add(new ProfileUnusualModeData(index, mode));
                }

                return new ProfilePersistenceData
                {
                    Leaderboard = Leaderboard,
                    AccSaberType = AccSaberCategory,
                    Scores = [.. _scores.Select(play => play.Pp)],
                    ScoreNames = [.. _scores.Select(play => play.MapName)],
                    ScoreDiffs = HelpfulMisc.CompressEnums(difficulties),
                    ScoreIDs = [.. _scores.Select(play => play.MapKey)],
                    Ranks = [.. _scores.Select(play => play.Rank)],
                    APCategories = IsAccSaberOverall ? HelpfulMisc.CompressEnums(categories) : null,
                    UnusualModes = unusualModes,
                    TotalPP = _totalPp,
                    UserID = UserId,
                    PlusOne = _plusOne,
                };
            }
        }

        /// <summary>
        /// Reconstructs profile plays from previously persisted profile data.
        /// </summary>
        /// <param name="data">The validated persistence data from which to create plays.</param>
        /// <returns>
        /// An array of reconstructed plays in the same order as the persisted score
        /// arrays.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="data"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// This method validates the supplied persistence data before reading it,
        /// decompresses stored difficulties and optional AccSaber categories, and uses
        /// <see cref="DEFAULT_MODE"/> for plays with no explicitly persisted mode.
        /// </remarks>
        internal static Play[] CreatePlaysFromPersistenceData(ProfilePersistenceData data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.Validate();

            BeatmapDifficulty[] difficulties = HelpfulMisc.UncompressEnums<BeatmapDifficulty>(data.ScoreDiffs);

            APCategory[]? categories = null;

            if (data.APCategories is not null && data.APCategories.Length > 0)
            {
                categories = HelpfulMisc.UncompressEnums<APCategory>(data.APCategories);
            }

            Dictionary<int, string> unusualModes = data.UnusualModes.ToDictionary(
                value => value.Index,
                value => value.Mode
            );

            Play[] plays = new Play[data.Scores.Length];

            for (int index = 0; index < plays.Length; index++)
            {
                plays[index] = new Play(
                    data.ScoreNames[index],
                    data.ScoreIDs[index],
                    difficulties[index],
                    unusualModes.TryGetValue(index, out string? mode) ? mode : DEFAULT_MODE,
                    data.Scores[index],
                    data.Ranks[index]
                )
                {
                    AccSaberCategory =
                        categories is not null && index < categories.Length ? categories[index] : APCategory.None,
                };
            }

            return plays;
        }

        /// <summary>
        /// Gets the maximum number of plays retained and weighted for a leaderboard.
        /// </summary>
        /// <param name="leaderboard">The leaderboard whose retained-play limit is requested.</param>
        /// <returns>
        /// <c>100</c> for BeatLeader, ScoreSaber, and AccSaber.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="leaderboard"/> is not a supported leaderboard.
        /// </exception>
        internal static int GetPlusOneCount(Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber or Leaderboards.Accsaber => 100,

                _ => throw new ArgumentOutOfRangeException(
                    nameof(leaderboard),
                    leaderboard,
                    "A supported leaderboard is required."
                ),
            };
        }

        /// <summary>
        /// Finds the stored play matching a map key, difficulty, and characteristic mode.
        /// </summary>
        /// <param name="mapKey">The map key to match, compared without case sensitivity.</param>
        /// <param name="difficulty">The difficulty to match.</param>
        /// <param name="mode">
        /// The beatmap characteristic or mode to match, compared using ordinal,
        /// case-sensitive comparison.
        /// </param>
        /// <returns>
        /// The zero-based index of the matching play; otherwise, <c>-1</c> when no
        /// stored play matches all supplied identifiers.
        /// </returns>
        /// <remarks>
        /// A play is considered a duplicate only when its map key, difficulty, and mode
        /// all match the supplied values.
        /// </remarks>
        private int FindPlayIndex(string mapKey, BeatmapDifficulty difficulty, string mode)
        {
            for (int index = 0; index < _scores.Count; index++)
            {
                Play play = _scores[index];

                if (!string.Equals(play.MapKey, mapKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (play.Difficulty != difficulty)
                    continue;

                if (string.Equals(play.Mode, mode, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the index at which a raw PP/AP value should be inserted into the
        /// descending stored-score collection.
        /// </summary>
        /// <param name="rawPp">The raw PP/AP value to insert.</param>
        /// <returns>
        /// The zero-based insertion index that preserves descending PP/AP order.
        /// </returns>
        /// <remarks>
        /// Scores equal to <paramref name="rawPp"/> are inserted after existing scores
        /// with the same PP/AP value.
        /// </remarks>
        private int FindInsertionIndex(float rawPp)
        {
            int low = 0;
            int high = _scores.Count;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (_scores[middle].Pp >= rawPp)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>
        /// Finds the index at which a weighted PP/AP value should be inserted into the
        /// descending weighted-score collection.
        /// </summary>
        /// <param name="weightedPp">The weighted PP/AP value to insert.</param>
        /// <returns>
        /// The zero-based insertion index that preserves descending weighted PP/AP order.
        /// </returns>
        /// <remarks>
        /// Weighted values equal to <paramref name="weightedPp"/> are inserted after
        /// existing equal values.
        /// </remarks>
        private int FindWeightedInsertionIndex(float weightedPp)
        {
            int low = 0;
            int high = _weightedScores.Length;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (_weightedScores[middle] >= weightedPp)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Creates the weighted PP/AP contribution for each supplied play.
        /// </summary>
        /// <param name="scores">
        /// The plays to weight, ordered from highest to lowest raw PP/AP.
        /// </param>
        /// <returns>
        /// An array whose values correspond by index to <paramref name="scores"/> and
        /// contain each play's raw PP/AP multiplied by its rank weight.
        /// </returns>
        /// <remarks>
        /// The first play uses the weight for score position one. Each subsequent play
        /// uses the next leaderboard-specific weight.
        /// </remarks>
        private float[] CreateWeightedScores(IReadOnlyList<Play> scores)
        {
            float[] weightedScores = new float[scores.Count];

            float weight = GetWeight(1);

            for (int index = 0; index < scores.Count; index++)
            {
                weightedScores[index] = weight * scores[index].Pp;

                weight = GetNextWeight(weight);
            }

            return weightedScores;
        }

        /// <summary>
        /// Calculates the raw PP/AP required for a new eligible play to increase the
        /// profile's total PP/AP by one point.
        /// </summary>
        /// <returns>
        /// The rounded raw PP/AP threshold required to gain one profile PP/AP; <c>1f</c>
        /// when the profile contains no plays; or <c>0f</c> when no insertion position
        /// can produce a one-point gain within the stored weighted scores.
        /// </returns>
        /// <remarks>
        /// BeatLeader and ScoreSaber use the normal geometric weighting calculation.
        /// AccSaber uses <see cref="CalculateAccSaberPlusOne"/> because its weighting
        /// curve differs from the other supported leaderboards.
        /// </remarks>
        private float CalculatePlusOneInternal()
        {
            if (_scores.Count == 0)
                return 1f;

            if (Leaderboard == Leaderboards.Accsaber)
            {
                return CalculateAccSaberPlusOne();
            }

            int index = 0;
            float shiftWeight = 1f - GetWeight(2);

            float weightedSum = _totalPp;

            while (index < _weightedScores.Length && _weightedScores[index] - (shiftWeight * weightedSum) >= 1f)
            {
                weightedSum -= _weightedScores[index];

                index++;
            }

            if (index >= _weightedScores.Length)
            {
                return 0f;
            }

            return Round((1f + (shiftWeight * weightedSum)) / GetWeight(index + 1));
        }

        /// <summary>
        /// Calculates the raw AP required for an AccSaber play to increase the profile
        /// total by one AP.
        /// </summary>
        /// <returns>
        /// The rounded raw AP threshold required for a one-point profile increase, or
        /// <c>1f</c> when the profile contains no plays.
        /// </returns>
        /// <remarks>
        /// This calculation accounts for AccSaber's logistic weighting curve and for
        /// any difference between the stored weighted-score sum and the profile's
        /// current total AP.
        /// </remarks>
        private float CalculateAccSaberPlusOne()
        {
            if (_scores.Count == 0)
                return 1f;

            float savedTotal = _weightedScores.Sum();

            float totalOffset =
                (_totalPp - savedTotal) * (GetWeight(_scores.Count) * _scores[0].Pp * 100f > 1f ? GetWeight(2) : 0f);

            float currentWeight = GetWeight(_scores.Count + 1);

            int index = _scores.Count - 1;

            for (; index > 0; index--)
            {
                totalOffset += _weightedScores[index] - (_scores[index].Pp * currentWeight);

                currentWeight = GetPreviousWeight(currentWeight);

                if ((_scores[index - 1].Pp * currentWeight) - totalOffset >= 1f)
                {
                    break;
                }
            }

            return Round((1f + totalOffset) / currentWeight);
        }

        /// <summary>
        /// Gets the leaderboard-specific weighting multiplier for a one-based score rank.
        /// </summary>
        /// <param name="scoreNumber">
        /// The one-based rank of the score in the descending profile play list.
        /// </param>
        /// <returns>
        /// The multiplier applied to a score at <paramref name="scoreNumber"/>.
        /// </returns>
        /// <remarks>
        /// BeatLeader and ScoreSaber use a geometric decay of <c>0.965</c> per rank.
        /// AccSaber uses its logistic weighting curve. Unsupported leaderboard values
        /// receive an unmodified weight of <c>1f</c>.
        /// </remarks>
        private float GetWeight(int scoreNumber)
        {
            int index = scoreNumber - 1;

            return Leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber => (float)Math.Pow(0.965d, index),

                Leaderboards.Accsaber => AccSaberConstants[1]
                    / (1f + (float)Math.Exp(0.4d * (index - AccSaberConstants[0]))),

                _ => 1f,
            };
        }

        /// <summary>
        /// Gets the weighting multiplier immediately following a known multiplier.
        /// </summary>
        /// <param name="lastWeight">
        /// The weighting multiplier for the preceding score position.
        /// </param>
        /// <returns>
        /// The weighting multiplier for the next lower-ranked score position.
        /// </returns>
        /// <remarks>
        /// This avoids recalculating the full weight from a rank number while building
        /// the weighted-score collection.
        /// </remarks>
        private float GetNextWeight(float lastWeight)
        {
            return Leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber => lastWeight * 0.965f,

                Leaderboards.Accsaber => 1f / ((1.49182469764f / lastWeight) - AccSaberConstants[2]),

                _ => lastWeight,
            };
        }

        /// <summary>
        /// Gets the weighting multiplier immediately preceding a known multiplier.
        /// </summary>
        /// <param name="lastWeight">
        /// The weighting multiplier for the current score position.
        /// </param>
        /// <returns>
        /// The weighting multiplier for the next higher-ranked score position.
        /// </returns>
        /// <remarks>
        /// This is used by the AccSaber PlusOne calculation while iterating from lower
        /// ranked plays toward higher ranked plays.
        /// </remarks>
        private float GetPreviousWeight(float lastWeight)
        {
            return Leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber => lastWeight / 0.965f,

                Leaderboards.Accsaber => 1f / ((1f / (1.49182469764f * lastWeight)) - AccSaberConstants[3]),

                _ => lastWeight,
            };
        }

        /// <summary>
        /// Restricts the currently selected page number to the valid range for the
        /// stored score count.
        /// </summary>
        /// <remarks>
        /// The first page is always page one, including when the profile has no scores.
        /// The final page is calculated from <see cref="PLAYS_TO_SHOW"/>.
        /// </remarks>
        private void ClampPageNumber()
        {
            int maximumPage = Math.Max(1, (_scores.Count + PLAYS_TO_SHOW - 1) / PLAYS_TO_SHOW);

            if (_pageNumber < 1)
                _pageNumber = 1;
            else if (_pageNumber > maximumPage)
                _pageNumber = maximumPage;
        }

        /// <summary>
        /// Rounds a PP/AP value using the decimal precision configured for the plugin.
        /// </summary>
        /// <param name="value">The value to round.</param>
        /// <returns>
        /// <paramref name="value"/> rounded to <c>_config.DecimalPrecision</c> decimal places.
        /// </returns>
        private float Round(float value)
        {
            return (float)Math.Round(value, _config.DecimalPrecision);
        }

        /// <summary>
        /// Notifies all subscribers that the profile state has changed.
        /// </summary>
        /// <remarks>
        /// Each subscriber is invoked independently. Exceptions thrown by one
        /// subscriber are logged and do not prevent notification of subsequent
        /// subscribers.
        /// </remarks>
        private void NotifyChanged()
        {
            Action? handler = Changed;

            if (handler is null)
                return;

            foreach (Action subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception exception)
                {
                    Plugin.Log.Error("A profile change subscriber failed:\n" + exception);
                }
            }
        }

        /// <summary>
        /// Determines whether two floating-point values are equal within a small
        /// absolute tolerance.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>
        /// <see langword="true"/> when the absolute difference between
        /// <paramref name="left"/> and <paramref name="right"/> is at most
        /// <see cref="UnityEngine.Mathf.Epsilon"/>; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool ApproximatelyEqual(float left, float right)
        {
            return UnityEngine.Mathf.Approximately(left, right);
        }

        /// <summary>
        /// Creates constants used to calculate and step through AccSaber's logistic
        /// score-weighting curve.
        /// </summary>
        /// <returns>
        /// An array containing the logistic curve offset, normalization factor, and
        /// forward and reverse weight-step constants, in that order.
        /// </returns>
        /// <remarks>
        /// The returned array is used by <see cref="GetWeight(int)"/>,
        /// <see cref="GetNextWeight(float)"/>, and
        /// <see cref="GetPreviousWeight(float)"/> for AccSaber profiles.
        /// </remarks>
        private static float[] CreateAccSaberConstants()
        {
            const double y1 = 0.1d;
            const double x1 = 15d;
            const double k = 0.4d;

            float[] constants = new float[4];

            constants[0] = (float)(-Math.Log((1d - y1) / ((y1 * Math.Exp(k * x1)) - 1d)) / k);

            constants[1] = 1f + (float)Math.Exp(-k * constants[0]);

            constants[2] = 1f / constants[1] * ((float)Math.Exp(k) - 1f);

            constants[3] = 1f / constants[1] * ((float)Math.Exp(-k) - 1f);

            return constants;
        }
    }
}
