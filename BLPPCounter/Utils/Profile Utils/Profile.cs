using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;

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

        public Play[] GetScoresSnapshot()
        {
            lock (_stateLock)
                return [.. _scores];
        }

        public ProfileSnapshot GetSnapshot()
        {
            lock (_stateLock)
            {
                return new ProfileSnapshot(Key, _totalPp, _plusOne, _pageNumber, _scores.ToArray());
            }
        }

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

        public void PageUp()
        {
            lock (_stateLock)
            {
                _pageNumber--;
                ClampPageNumber();
            }

            NotifyChanged();
        }

        public void PageDown()
        {
            lock (_stateLock)
            {
                _pageNumber++;
                ClampPageNumber();
            }

            NotifyChanged();
        }

        public void PageTop()
        {
            lock (_stateLock)
                _pageNumber = 1;

            NotifyChanged();
        }

        public float GetWeightedPp(float rawPp)
        {
            lock (_stateLock)
            {
                if (float.IsNaN(rawPp))
                    return -1f;

                if (_scores.Count > 0 && _scores[^1].Pp > rawPp)
                {
                    return -1f;
                }

                int index = FindInsertionIndex(rawPp);

                return Round(GetWeight(index + 1) * rawPp);
            }
        }

        public float GetProfilePp(float weightedPp)
        {
            lock (_stateLock)
            {
                int index = FindWeightedInsertionIndex(weightedPp);

                return GetProfilePpInternal(weightedPp, index + 1);
            }
        }

        public float GetProfilePpRaw(float rawPp)
        {
            lock (_stateLock)
            {
                if (float.IsNaN(rawPp))
                    return -1f;

                if (_scores.Count > 0 && _scores[^1].Pp > rawPp)
                {
                    return -1f;
                }

                int index = FindInsertionIndex(rawPp);
                float weightedPp = GetWeight(index + 1) * rawPp;

                return GetProfilePpInternal(weightedPp, index + 1);
            }
        }

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

            if (float.IsNaN(rawPp))
                return false;

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

            float profilePpGained;
            float oldPp;
            Play insertedPlay;

            lock (_stateLock)
            {
                int duplicateIndex = FindPlayIndex(mapKey, difficulty, mode);

                if (duplicateIndex >= 0 && _scores[duplicateIndex].Pp >= rawPp)
                {
                    return false;
                }

                if (duplicateIndex < 0 && _scores.Count >= GetPlusOneCount(Leaderboard) && _scores[^1].Pp > rawPp)
                {
                    return false;
                }

                oldPp = duplicateIndex >= 0 ? _scores[duplicateIndex].Pp : 0f;

                if (duplicateIndex >= 0)
                {
                    float weightedBefore = _weightedScores.Sum();

                    _scores.RemoveAt(duplicateIndex);

                    _weightedScores = CreateWeightedScores(_scores);

                    float weightedAfter = _weightedScores.Sum();

                    _totalPp -= weightedBefore - weightedAfter;
                }

                int insertIndex = FindInsertionIndex(rawPp);

                float weightedPp = GetWeight(insertIndex + 1) * rawPp;

                profilePpGained = GetProfilePpInternal(weightedPp, insertIndex + 1);

                if (profilePpGained > 0f)
                    _totalPp += profilePpGained;

                insertedPlay = new Play(
                    mapName,
                    mapKey,
                    difficulty,
                    mode,
                    Round(rawPp),
                    0,
                    Round(profilePpGained),
                    Round(oldPp)
                )
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

                _plusOne = CalculatePlusOneInternal();

                ClampPageNumber();
            }

            NotifyChanged();

            if (!addToSession)
                return true;

            uint rank = await CurrentSession
                .AddPlayAsync(
                    mapName,
                    hash,
                    mapKey,
                    difficulty,
                    mode,
                    rawPp,
                    0,
                    profilePpGained,
                    oldPp,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (rank == 0)
                return true;

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
                NotifyChanged();

            return true;
        }

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

        private int FindInsertionIndex(float rawPp)
        {
            int low = 0;
            int high = _scores.Count;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (_scores[middle].Pp >= rawPp)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private int FindWeightedInsertionIndex(float weightedPp)
        {
            int low = 0;
            int high = _weightedScores.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

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

        private float GetProfilePpInternal(float weightedPp, int scoreNumber, int ignoredScore = -1)
        {
            int index = scoreNumber - 1;

            if (index < 0 || index >= _weightedScores.Length)
            {
                return -1f;
            }

            float result;

            if (Leaderboard == Leaderboards.Accsaber)
            {
                float offset = 0f;
                float currentWeight = GetWeight(index + 2);

                for (int currentIndex = index; currentIndex < _weightedScores.Length; currentIndex++)
                {
                    if (currentIndex != ignoredScore)
                    {
                        offset += _weightedScores[currentIndex] - _scores[currentIndex].Pp * currentWeight;
                    }

                    currentWeight = GetNextWeight(currentWeight);
                }

                result = Round(weightedPp - offset);
            }
            else
            {
                float weightedSum = _totalPp;

                for (int currentIndex = 0; currentIndex < index; currentIndex++)
                {
                    weightedSum -= _weightedScores[currentIndex];
                }

                result = Round(weightedPp - (1f - GetWeight(2)) * weightedSum);
            }

            return result < 0f ? 0f : result;
        }

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

            while (index < _weightedScores.Length && _weightedScores[index] - shiftWeight * weightedSum >= 1f)
            {
                weightedSum -= _weightedScores[index];

                index++;
            }

            if (index >= _weightedScores.Length)
            {
                return 0f;
            }

            return Round((1f + shiftWeight * weightedSum) / GetWeight(index + 1));
        }

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
                totalOffset += _weightedScores[index] - _scores[index].Pp * currentWeight;

                currentWeight = GetPreviousWeight(currentWeight);

                if (_scores[index - 1].Pp * currentWeight - totalOffset >= 1f)
                {
                    break;
                }
            }

            return Round((1f + totalOffset) / currentWeight);
        }

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

        private float GetNextWeight(float lastWeight)
        {
            return Leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber => lastWeight * 0.965f,

                Leaderboards.Accsaber => 1f / (1.49182469764f / lastWeight - AccSaberConstants[2]),

                _ => lastWeight,
            };
        }

        private float GetPreviousWeight(float lastWeight)
        {
            return Leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber => lastWeight / 0.965f,

                Leaderboards.Accsaber => 1f / (1f / (1.49182469764f * lastWeight) - AccSaberConstants[3]),

                _ => lastWeight,
            };
        }

        private void ClampPageNumber()
        {
            int maximumPage = Math.Max(1, (_scores.Count + PLAYS_TO_SHOW - 1) / PLAYS_TO_SHOW);

            if (_pageNumber < 1)
                _pageNumber = 1;
            else if (_pageNumber > maximumPage)
                _pageNumber = maximumPage;
        }

        private float Round(float value)
        {
            return (float)Math.Round(value, _config.DecimalPrecision);
        }

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

        private static bool ApproximatelyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= 0.0001f;
        }

        private static float[] CreateAccSaberConstants()
        {
            const double y1 = 0.1d;
            const double x1 = 15d;
            const double k = 0.4d;

            float[] constants = new float[4];

            constants[0] = (float)(-Math.Log((1d - y1) / (y1 * Math.Exp(k * x1) - 1d)) / k);

            constants[1] = 1f + (float)Math.Exp(-k * constants[0]);

            constants[2] = 1f / constants[1] * ((float)Math.Exp(k) - 1f);

            constants[3] = 1f / constants[1] * ((float)Math.Exp(-k) - 1f);

            return constants;
        }
    }
}
