using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Models;
using BLPPCounter.Models.Base;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using Newtonsoft.Json.Linq;
using Zenject;
using static BLPPCounter.Helpfuls.HelpfulPaths;
using static GameplayModifiers;

namespace BLPPCounter.Utils
{
    internal class PathUtils
    {
        #region Injects

        [Inject]
        private readonly ILeaderboardApiProvider provider = null!;

        [Inject]
        private readonly RatingContainer.Factory ratingFactory = null!;

        #endregion
        #region Directory Checks
        public bool EnsureTaohableDirectoryExists()
        {
            string path = THE_FOLDER;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            path = Path.Combine(path, "TaohableData.json");
            HttpContent? content =
                provider.BeatLeader.CallAPI_Static(TAOHABLE_META).GetAwaiter().GetResult()
                ?? throw new Exception(
                    "Taoh's meta file is not available. This means either internet is down or I need to go yell at someone."
                );
            JToken TaohHeaders = JToken.Parse(content.ReadAsStringAsync().GetAwaiter().GetResult());
            bool headersGood = true;

            if (!File.Exists(HEADERS))
                using (FileStream fs = File.Create(HEADERS))
                {
                    byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                    fs.Write(headerInfo, 0, headerInfo.Length);
                    return false;
                }
            else
            {
                string headerString = File.ReadAllText(HEADERS);
                if (headerString is null || headerString.Length == 0)
                    using (FileStream fs = File.OpenWrite(HEADERS))
                    {
                        byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                        fs.Write(headerInfo, 0, headerInfo.Length);
                        return false;
                    }
                JToken? CurrentTaohHeaders = null;
                try
                {
                    CurrentTaohHeaders = JToken.Parse(headerString);
                    static int SimpleComparer<T>(T i1, T i2)
                        where T : IComparable
                    {
                        return i1.CompareTo(i2);
                    }

                    headersGood =
                        HelpfulMisc.CompareStructValues<float>(
                            TaohHeaders,
                            CurrentTaohHeaders,
                            "top10kVersion",
                            SimpleComparer
                        ) <= 0
                        && HelpfulMisc.CompareStructValues<float>(
                            TaohHeaders,
                            CurrentTaohHeaders,
                            "songLibraryVersion",
                            SimpleComparer
                        ) <= 0
                        && HelpfulMisc.CompareValues(
                            TaohHeaders,
                            CurrentTaohHeaders,
                            "top10kUpdated",
                            item => DateTime.Parse(item),
                            SimpleComparer
                        ) <= 0;
                }
                catch (Exception e)
                {
                    Plugin.Log.Warn(
                        "Oh No! Current Taoh Headers are not parseable! Automatically overriding the problem file.\n"
                            + e
                    );
                    File.Delete(HEADERS);
                    headersGood = false;
                }
                if (!headersGood)
                    using (FileStream fs = File.OpenWrite(HEADERS))
                    {
                        byte[] headerInfo = Encoding.UTF8.GetBytes(TaohHeaders.ToString());
                        fs.Write(headerInfo, 0, headerInfo.Length);
                    }
            }
            return headersGood && File.Exists(path);
        }
        #endregion

        #region Json Paths
        public static string DiffNumToReloadedDiff(int diffNum)
        {
            return diffNum switch
            {
                1 => "EASY",
                3 => "NORMAL",
                5 => "HARD",
                7 => "EXPERT",
                9 => "EXPERT_PLUS",
                _ => throw new ArgumentException(
                    "Invalid difficulty number. Must be one of the following: 1, 3, 5, 7, 9."
                ),
            };
        }

        public static int ReloadedDiffToDiffNum(string? diff)
        {
            return diff switch
            {
                "EASY" => 1,
                "NORMAL" => 3,
                "HARD" => 5,
                "EXPERT" => 7,
                "EXPERT_PLUS" => 9,
                _ => throw new ArgumentException(
                    "Invalid difficulty string. Must be one of the following: EASY, NORMAL, HARD, EXPERT, EXPERT_PLUS."
                ),
            };
        }

        public static string? ReloadedCategoryToCategoryId(string? category)
        {
            return category switch
            {
                "b0000000-0000-0000-0000-000000000001" => "True",
                "b0000000-0000-0000-0000-000000000002" => "Standard",
                "b0000000-0000-0000-0000-000000000003" => "Tech",
                _ => null,
            };
        }

        public static string? CategoryIdToReloadedCategory(string? category)
        {
            return category switch
            {
                "True" => "b0000000-0000-0000-0000-000000000001",
                "Standard" => "b0000000-0000-0000-0000-000000000002",
                "Tech" => "b0000000-0000-0000-0000-000000000003",
                _ => null,
            };
        }

        public float GetRating(
            ICacheModel? data,
            PPType type,
            Leaderboards leaderboard,
            SongSpeed mod = SongSpeed.Normal
        )
        {
            if (data is null)
                return 0f;

            if (data is TaohCache taohCache)
                return GetRating(taohCache, type, leaderboard, mod);

            if (data is BlCacheEntry blCache)
                return GetRating(blCache, type, leaderboard, mod);

            throw new ArgumentException(
                "The given ICacheModel is not a known type, cannot get the rating.\nType = " + data.GetType()
            );
        }

        private float GetRating(TaohCache cache, PPType type, Leaderboards leaderboard, SongSpeed mod)
        {
            if (mod != SongSpeed.Normal || (leaderboard != Leaderboards.Beatleader && type != PPType.Star))
                return 0f;

            if (type == PPType.Star)
            {
                switch (leaderboard)
                {
                    case Leaderboards.Beatleader:
                        return cache.StarBeatLeader.GetValueOrDefault();
                    case Leaderboards.Scoresaber:
                        return cache.StarScoreSaber.GetValueOrDefault();
                    case Leaderboards.Accsaber:
                        return cache.ComplexityAccSaber.GetValueOrDefault();
                }
            }

            return type switch
            {
                PPType.Star => throw new ArgumentException("How in the world did we end up here????"),
                PPType.Acc => cache.StarAccBeatLeader.GetValueOrDefault(),
                PPType.Pass => cache.StarPassBeatLeader.GetValueOrDefault(),
                PPType.Tech => cache.StarTechBeatLeader.GetValueOrDefault(),
                _ => 0f,
            };
        }

        private float GetRating(BlCacheEntry cache, PPType type, Leaderboards leaderboard, SongSpeed mod)
        {
            if (leaderboard != Leaderboards.Beatleader)
                return 0f;
            return cache.DifficultyInfo.GetRating(type, mod);
        }

        public RatingContainer GetAllRatingsOfSpeed(
            ICacheModel data,
            Leaderboards leaderboard = Leaderboards.Beatleader,
            SongSpeed mod = SongSpeed.Normal
        )
        { //star, acc, pass, tech
            float[] outp =
            [
                .. Enum.GetValues(typeof(PPType))
                    .Cast<PPType>()
                    .Select(type => GetRating(data, type, leaderboard, mod)),
            ];
            return ratingFactory.GetContainer(leaderboard, outp);
        }

        public RatingContainer[] GetAllRatings(ICacheModel data, Calculator? calc)
        {
            //Plugin.Log.Info("Getting all ratings of all speeds for data:\n" + data);
            Leaderboards leaderboard = calc?.Leaderboard ?? Leaderboards.Beatleader;

            if (!(calc?.UsesModifiers ?? true))
                return [GetAllRatingsOfSpeed(data, leaderboard)];

            List<RatingContainer> outp = [with(HelpfulMisc.OrderedSpeeds.Length)];
            foreach (SongSpeed s in HelpfulMisc.OrderedSpeeds)
                outp.Add(GetAllRatingsOfSpeed(data, leaderboard, s));

            return [.. outp];
        }

        public RatingContainer[] GetAllRatings(ICacheModel data)
        {
            return GetAllRatings(data, null);
        }

        public float GetStarMultiplier(ICacheModel data, GameplayModifiers? mods)
        {
            if (mods is null || data is not BlCacheEntry cache)
                return 1.0f;

            float outp = 1.0f;
            if (mods.ghostNotes)
                outp += cache.DifficultyInfo.Modifiers["gn"];
            if (mods.noArrows)
                outp += cache.DifficultyInfo.Modifiers["na"];
            if (mods.enabledObstacleType == EnabledObstacleType.NoObstacles)
                outp += cache.DifficultyInfo.Modifiers["no"];
            if (mods.noBombs)
                outp += cache.DifficultyInfo.Modifiers["nb"];
            return outp;
        }

        public float GetMultiAmounts(ICacheModel data, string[] names)
        {
            if (data is not BlCacheEntry cache)
                return 1f;

            float outp = 1f;

            foreach (string n in names)
                outp += cache.DifficultyInfo.Modifiers.TryGetValue(n, out float val) ? val : 0f;

            return outp;
        }
        #endregion
    }
}
