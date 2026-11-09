using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using Newtonsoft.Json.Linq;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class Session
    {
        private readonly object _stateLock = new();
        private readonly List<Play> _scores = [];
        private readonly APIHandler _api;
        private readonly int _decimalPrecision;

        internal Session(
            Leaderboards leaderboard,
            string userId,
            float initialProfilePp,
            int decimalPrecision,
            APIHandler api
        )
        {
            if (BetterString.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("A session requires a user ID.", nameof(userId));
            }

            if (decimalPrecision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimalPrecision));
            }

            Leaderboard = leaderboard;
            UserId = userId;
            InitialProfilePp = initialProfilePp;
            _decimalPrecision = decimalPrecision;

            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        /// <summary>
        /// Raised whenever the session's score state changes.
        /// Subscribers must dispatch to Unity's main thread before touching UI.
        /// </summary>
        public event Action? Changed;

        public Leaderboards Leaderboard { get; }

        public string UserId { get; }

        public float InitialProfilePp { get; }

        public float GainedProfilePp
        {
            get
            {
                lock (_stateLock)
                {
                    return _scores.Sum(play => play.ProfilePpGained);
                }
            }
        }

        public int PlaysSet
        {
            get
            {
                lock (_stateLock)
                    return _scores.Count;
            }
        }

        public float CurrentProfilePp => InitialProfilePp + GainedProfilePp;

        public async Task<uint> AddPlayAsync(
            string mapName,
            string hash,
            string mapKey,
            BeatmapDifficulty difficulty,
            string mode,
            float rawPp,
            uint rank,
            float profilePpGained,
            float oldPp = -1,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            Play play = new(
                mapName,
                mapKey,
                difficulty,
                mode,
                Round(rawPp),
                rank,
                Round(profilePpGained),
                Round(oldPp)
            );

            int playIndex;

            lock (_stateLock)
            {
                playIndex = _scores.Count;
                _scores.Add(play);
            }

            NotifyChanged();

            if (rank != 0)
                return rank;

            uint fetchedRank;

            try
            {
                fetchedRank = await FetchRankAsync(hash, difficulty, mode, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error(
                    $"Failed to obtain a rank for session play " + $"\"{mapName}\" ({mapKey}):\n{exception}"
                );

                return 0;
            }

            if (fetchedRank == 0)
                return 0;

            lock (_stateLock)
            {
                if (playIndex < 0 || playIndex >= _scores.Count)
                {
                    Plugin.Log.Warn(
                        $"Session play index {playIndex} no longer " + "exists while applying a fetched rank."
                    );

                    return 0;
                }

                Play updatedPlay = _scores[playIndex];
                updatedPlay.Rank = fetchedRank;
                _scores[playIndex] = updatedPlay;
            }

            NotifyChanged();
            return fetchedRank;
        }

        public Play[] GetScoresSnapshot()
        {
            lock (_stateLock)
                return [.. _scores];
        }

        public SessionSnapshot GetSnapshot()
        {
            lock (_stateLock)
            {
                Play[] scores = [.. _scores];

                return new SessionSnapshot(
                    Leaderboard,
                    UserId,
                    InitialProfilePp,
                    scores.Sum(play => play.ProfilePpGained),
                    scores
                );
            }
        }

        private async Task<uint> FetchRankAsync(
            string hash,
            BeatmapDifficulty difficulty,
            string mode,
            CancellationToken cancellationToken
        )
        {
            JToken? scoreData = await _api.GetScoreData(
                    UserId,
                    hash,
                    difficulty.ToString(),
                    mode,
                    quiet: true,
                    ct: cancellationToken
                )
                .ConfigureAwait(false);

            if (scoreData is null)
            {
                Plugin.Log.Debug(
                    $"No session rank was returned for user "
                        + $"{UserId}, map {hash}, difficulty "
                        + $"{difficulty}, and mode {mode}."
                );

                return 0;
            }

            int rank = _api.GetRank(scoreData);

            return rank > 0 ? checked((uint)rank) : 0;
        }

        private float Round(float value)
        {
            if (value < 0)
                return value;

            return (float)Math.Round(value, _decimalPrecision);
        }

        private void NotifyChanged()
        {
            Action? handler = Changed;

            if (handler is null)
                return;

            try
            {
                handler();
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"A session change subscriber failed:\n" + exception);
            }
        }
    }
}
