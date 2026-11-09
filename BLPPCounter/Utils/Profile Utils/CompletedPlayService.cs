using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Models.MapModels;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.LangAddons;
using BLPPCounter.Utils.Map_Utils;
using static GameplayModifiers;

namespace BLPPCounter.Utils.Profile_Utils
{
    internal sealed class CompletedPlayService : ICompletedPlayService
    {
        private static readonly Leaderboards[] SupportedLeaderboards =
        [
            Leaderboards.Beatleader,
            Leaderboards.Scoresaber,
            Leaderboards.Accsaber,
        ];

        private readonly IProfileRepository _profileRepository;

        private readonly IBeatSaverMetadataService _beatSaverMetadata;

        private readonly ILeaderboardApiProvider _apiProvider;

        private readonly ICalculatorProvider _calculatorProvider;

        private readonly IMapRepository _mapRepository;

        private readonly PathUtils _pathUtils;

        public CompletedPlayService(
            IProfileRepository profileRepository,
            IBeatSaverMetadataService beatSaverMetadata,
            ILeaderboardApiProvider apiProvider,
            ICalculatorProvider calculatorProvider,
            IMapRepository mapRepository,
            PathUtils pathUtils
        )
        {
            _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));

            _beatSaverMetadata = beatSaverMetadata ?? throw new ArgumentNullException(nameof(beatSaverMetadata));

            _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            _calculatorProvider = calculatorProvider ?? throw new ArgumentNullException(nameof(calculatorProvider));

            _mapRepository = mapRepository ?? throw new ArgumentNullException(nameof(mapRepository));

            _pathUtils = pathUtils ?? throw new ArgumentNullException(nameof(pathUtils));
        }

        public async Task<bool> AddCompletedPlayAsync(
            string userId,
            string hash,
            float accuracy,
            string mapName,
            BeatmapDifficulty difficulty,
            string characteristic = Profile.DEFAULT_MODE,
            GameplayModifiers? modifiers = null,
            CancellationToken cancellationToken = default
        )
        {
            ValidateCompletedPlay(userId, hash, accuracy, mapName);

            characteristic = BetterString.IsNullOrWhiteSpace(characteristic) ? Profile.DEFAULT_MODE : characteristic;

            BeatSaverMapMetadata? metadata = await _beatSaverMetadata
                .GetMapMetadataAsync(hash, difficulty, characteristic, cancellationToken)
                .ConfigureAwait(false);

            if (metadata is null)
                return false;

            bool addedAny = false;

            foreach (Leaderboards leaderboard in SupportedLeaderboards)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (
                    leaderboard != Leaderboards.Accsaber
                    && (metadata.RankedLeaderboards & leaderboard) == Leaderboards.None
                )
                {
                    continue;
                }

                try
                {
                    addedAny |= await AddToLeaderboardAsync(
                            leaderboard,
                            userId,
                            hash,
                            metadata.MapKey,
                            accuracy,
                            mapName,
                            difficulty,
                            characteristic,
                            modifiers,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Plugin.Log.Error($"Failed to add a completed play " + $"to {leaderboard}:\n{exception}");
                }
            }

            return addedAny;
        }

        private async Task<bool> AddToLeaderboardAsync(
            Leaderboards leaderboard,
            string userId,
            string hash,
            string mapKey,
            float accuracy,
            string mapName,
            BeatmapDifficulty difficulty,
            string characteristic,
            GameplayModifiers? modifiers,
            CancellationToken cancellationToken
        )
        {
            string cacheMode = SelectCacheMode(characteristic, leaderboard);

            APIHandler api = _apiProvider.Get(leaderboard);

            Map? map = await _mapRepository
                .GetAsync(hash, cacheMode, leaderboard, forceRemoteLoad: true, cancellationToken)
                .ConfigureAwait(false);

            if (
                map is null
                || !map.TryGet(cacheMode, difficulty, out MapDiffModel? mapDifficulty)
                || mapDifficulty is null
            )
            {
                return false;
            }

            ICacheModel cacheModel = mapDifficulty.Data;

            if (!api.MapIsUsable(cacheModel))
                return false;

            Calculator calculator = _calculatorProvider.Get(leaderboard);

            float[] ratings = GetSelectedRatings(leaderboard, calculator, cacheModel, modifiers);

            if (ratings.Length != calculator.RatingCount || ratings.Length == 0 || ContainsInvalidRating(ratings))
            {
                return false;
            }

            float pp = calculator.Inflate(calculator.GetSummedPp(accuracy, ratings));

            if (float.IsNaN(pp) || float.IsInfinity(pp) || pp <= 0f)
            {
                return false;
            }

            if (leaderboard != Leaderboards.Accsaber)
            {
                Profile profile = await _profileRepository
                    .GetAsync(leaderboard, userId, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return await profile
                    .AddPlayAsync(
                        pp,
                        mapName,
                        hash,
                        mapKey,
                        difficulty,
                        characteristic,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            APCategory category = ResolveAccSaberCategory(cacheModel);

            return await AddAccSaberPlayAsync(
                    userId,
                    hash,
                    mapKey,
                    pp,
                    mapName,
                    difficulty,
                    characteristic,
                    category,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        private async Task<bool> AddAccSaberPlayAsync(
            string userId,
            string hash,
            string mapKey,
            float pp,
            string mapName,
            BeatmapDifficulty difficulty,
            string characteristic,
            APCategory category,
            CancellationToken cancellationToken
        )
        {
            Profile overallProfile = await _profileRepository
                .GetAsync(Leaderboards.Accsaber, userId, APCategory.All, cancellationToken)
                .ConfigureAwait(false);

            bool addedOverall = await overallProfile
                .AddPlayAsync(
                    pp,
                    mapName,
                    hash,
                    mapKey,
                    difficulty,
                    characteristic,
                    category,
                    addToSession: true,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (category is APCategory.None or APCategory.All)
            {
                return addedOverall;
            }

            Profile categoryProfile = await _profileRepository
                .GetAsync(Leaderboards.Accsaber, userId, category, cancellationToken)
                .ConfigureAwait(false);

            bool addedCategory = await categoryProfile
                .AddPlayAsync(
                    pp,
                    mapName,
                    hash,
                    mapKey,
                    difficulty,
                    characteristic,
                    category,
                    addToSession: true,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return addedOverall || addedCategory;
        }

        private float[] GetSelectedRatings(
            Leaderboards leaderboard,
            Calculator calculator,
            ICacheModel cacheModel,
            GameplayModifiers? modifiers
        )
        {
            SongSpeed speed =
                leaderboard == Leaderboards.Beatleader ? modifiers?.songSpeed ?? SongSpeed.Normal : SongSpeed.Normal;

            float[] allRatings = _pathUtils
                .GetAllRatingsOfSpeed(cacheModel, leaderboard, speed)
                .GetRatings(leaderboard);

            if (leaderboard == Leaderboards.Beatleader && allRatings.Length >= 4)
            {
                float multiplier = _pathUtils.GetStarMultiplier(cacheModel, modifiers);

                allRatings[1] *= multiplier;
                allRatings[2] *= multiplier;
                allRatings[3] *= multiplier;
            }

            return calculator.SelectRatings(allRatings);
        }

        private static APCategory ResolveAccSaberCategory(ICacheModel cacheModel)
        {
            if (cacheModel is not TaohCache taohCache)
            {
                return APCategory.None;
            }

            int categoryBits = taohCache.SongCategory & 0xE;

            if (categoryBits <= 0 || (categoryBits & (categoryBits - 1)) != 0)
            {
                return APCategory.None;
            }

            int categoryValue = 0;

            while (categoryBits > 1)
            {
                categoryBits >>= 1;
                categoryValue++;
            }

            APCategory category = (APCategory)categoryValue;

            return
                Enum.IsDefined(typeof(APCategory), category)
                && category != APCategory.None
                && category != APCategory.All
                ? category
                : APCategory.None;
        }

        private static string SelectCacheMode(string characteristic, Leaderboards leaderboard)
        {
            return leaderboard switch
            {
                Leaderboards.Scoresaber => Map.SS_MODE_NAME,

                Leaderboards.Accsaber => Map.AP_MODE_NAME,

                _ => characteristic,
            };
        }

        private static bool ContainsInvalidRating(float[] ratings)
        {
            return ratings.Any(rating => float.IsNaN(rating) || float.IsInfinity(rating) || rating <= 0f);
        }

        private static void ValidateCompletedPlay(string userId, string hash, float accuracy, string mapName)
        {
            if (BetterString.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("A user ID is required.", nameof(userId));
            }

            if (BetterString.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("A map hash is required.", nameof(hash));
            }

            if (BetterString.IsNullOrWhiteSpace(mapName))
            {
                throw new ArgumentException("A map name is required.", nameof(mapName));
            }

            if (float.IsNaN(accuracy) || float.IsInfinity(accuracy) || accuracy <= 0f || accuracy > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(accuracy),
                    accuracy,
                    "Accuracy must be between zero " + "and one."
                );
            }
        }
    }
}
