using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Misc_Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using static AlphabetScrollInfo;

namespace BLPPCounter.Utils
{
    internal class Profile
    {
        #region Static Variables
        public static readonly string DEFAULT_MODE = "Standard";
        private static readonly float[] AccsaberConsts;
        private static readonly Dictionary<string, Profile> LoadedProfiles = [];
        internal static TextMeshProUGUI TextContainer 
        {
            private get => textContainer;
            set
            {
                bool doUpdate = textContainer is null;
                textContainer = value;
                if (doUpdate)
                    foreach (Profile profile in LoadedProfiles.Values)
                        profile.InitTable();
            }
        }
        private static TextMeshProUGUI textContainer = null;
        private static bool Deserializing = false;
        #endregion
        #region Variables
        [JsonIgnore] private const int PlaysToShow = 10;
        [JsonIgnore] private string ID => GetID(Leaderboard, UserID, AccSaberType);
        [JsonIgnore] private bool IsAP => Leaderboard == Leaderboards.Accsaber && AccSaberType == APCategory.All;

        [JsonIgnore] private BeatmapDifficulty[] ActualScoreDiffs;
        [JsonIgnore] private APCategory[] ActualAPCategories;
        [JsonIgnore] private float[] WeightedScores;
        [JsonIgnore] private HashSet<int> UnusualModeIndexes;

        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)] private readonly Leaderboards Leaderboard;
        [JsonProperty(nameof(AccSaberType), Required = Required.DisallowNull)] private readonly APCategory AccSaberType;
        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)] private float[] Scores;
        [JsonProperty(nameof(ScoreNames), Required = Required.DisallowNull)] private string[] ScoreNames;
        [JsonProperty(nameof(ScoreDiffs), Required = Required.DisallowNull)] private ulong[] ScoreDiffs;
        [JsonProperty(nameof(ScoreIDs), Required = Required.DisallowNull)] private string[] ScoreIDs;
        [JsonProperty(nameof(APCategories), Required = Required.AllowNull)] private ulong[] APCategories;
        [JsonProperty(nameof(UnusualModes), Required = Required.DisallowNull)] private List<(int Index, string Mode)> UnusualModes; //Defaults to "Standard" if not in this list.
        [JsonProperty(nameof(TotalPP), Required = Required.DisallowNull)] private float TotalPP = -1.0f;
        [JsonProperty(nameof(UserID), Required = Required.DisallowNull)] private readonly string UserID;
        [JsonProperty(nameof(PlusOne), Required = Required.DisallowNull)] public float PlusOne { get; private set; } = -1.0f;

        [JsonIgnore] public Table PlayTable { get; private set; }
        [JsonIgnore] private int PageNumber;
        [JsonIgnore] public Session CurrentSession { get; private set; }
        #endregion
        #region Init
        static Profile()
        {
            const float y1 = 0.1f, x1 = 15, k = 0.4f;
            AccsaberConsts = new float[4];
            AccsaberConsts[0] = -Mathf.Log((1 - y1) / (y1 * Mathf.Exp(k * x1) - 1)) / k; //x0, equal to about 9.44418879333
            AccsaberConsts[1] = 1 + Mathf.Exp(-k * AccsaberConsts[0]); //the base exponental decay function, equal to about 1.02287580408
            AccsaberConsts[2] = 1 / AccsaberConsts[1] * (Mathf.Exp(k) - 1); //A number to go to the next weight without big exponents, equal to about 0.480825429323
            AccsaberConsts[3] = 1 / AccsaberConsts[1] * (Mathf.Exp(-k) - 1); //Same function as the last number, just to go to the previous weight. Equal to about -0.322306923919
        }
        [JsonConstructor]
        public Profile(Leaderboards leaderboard, string userID, APCategory accSaberType)
        {
            Leaderboard = leaderboard;
            UserID = userID;
            AccSaberType = accSaberType;
            PageNumber = 1;
            InitScores();
        }
        public Profile(Leaderboards leaderboard, string userID) : this(leaderboard, userID, leaderboard == Leaderboards.Accsaber ? APCategory.All : APCategory.None)
        { }
        private void InitScores()
        {
            if (Deserializing) return;
            if (TotalPP < 0)
            {
                APIHandler api = APIHandler.GetAPI(Leaderboard);
                bool usingAP = Leaderboard == Leaderboards.Accsaber && AccSaberType != APCategory.All;
                Play[] scoreData = (usingAP ?
                    (api as APAPI).GetScores(UserID, GetPlusOneCount(), AccSaberType) : api.GetScores(UserID, GetPlusOneCount()))?
                    .GetAwaiter().GetResult();
                UnusualModeIndexes = [];
                UnusualModes = [];
                if (scoreData is null)
                {
                    float[] value = [0.0f];
                    Scores = value;
                    ScoreNames = ["No Scores"];
                    ActualScoreDiffs = [BeatmapDifficulty.Normal];
                    ScoreDiffs = [1UL];
                    ScoreIDs = ["12345"];
                    ActualAPCategories = null;
                }
                else
                {
                    Scores = new float[scoreData.Length];
                    ScoreNames = new string[scoreData.Length];
                    ActualScoreDiffs = new BeatmapDifficulty[scoreData.Length];
                    ScoreIDs = new string[scoreData.Length];
                    if (IsAP) ActualAPCategories = new APCategory[scoreData.Length];
                    else ActualAPCategories = null;
                    for (int i = 0; i < scoreData.Length; i++)
                    {
                        Scores[i] = scoreData[i].Pp;
                        ScoreNames[i] = scoreData[i].MapName;
                        ActualScoreDiffs[i] = scoreData[i].Difficulty;
                        ScoreIDs[i] = scoreData[i].MapKey;
                        if (IsAP) ActualAPCategories[i] = scoreData[i].AccSaberCategory;
                        if (!scoreData[i].Mode.Equals(DEFAULT_MODE))
                        {
                            UnusualModes.Add((i, scoreData[i].Mode));
                            UnusualModeIndexes.Add(i);
                        }
                    }
                    ScoreDiffs = HelpfulMisc.CompressEnums(ActualScoreDiffs);
                    if (IsAP) APCategories = HelpfulMisc.CompressEnums(ActualAPCategories);
                }
                TotalPP = (usingAP ? (api as APAPI).GetProfilePP(UserID, AccSaberType) : api.GetProfilePP(UserID)).GetAwaiter().GetResult();
                CurrentSession ??= new Session(Leaderboard, UserID, TotalPP);
                InitTable();
            }
            WeightScores();
            if (PlusOne < 0)
                PlusOne = CalculatePlusOne();
        }
        private void PostInit()
        {
            try
            {
                ActualScoreDiffs = HelpfulMisc.UncompressEnums<BeatmapDifficulty>(ScoreDiffs);
                if (ActualScoreDiffs.Length != Scores.Length) throw new Exception($"ScoreDiffs array length does not equal the Scores array length\nActualScoreDiffs length: {ActualScoreDiffs.Length} || Scores Length: {Scores.Length}");
                if (UnusualModes is null) throw new Exception($"Modes are not found! (UnusualModes is null)");
                UnusualModeIndexes = new HashSet<int>(UnusualModes.Count);
                foreach ((int i, _) in UnusualModes)
                    UnusualModeIndexes.Add(i);
                if (APCategories is not null && APCategories.Length != 0)
                    ActualAPCategories = HelpfulMisc.UncompressEnums<APCategory>(APCategories);
                WeightScores();
                CurrentSession ??= new Session(Leaderboard, UserID, TotalPP);
                //CurrentSession.AddPlay(ScoreNames[0], ScoreIDs[0], ActualScoreDiffs[0], "Standard", Scores[0], 20f); //For debugging
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e);
                Plugin.Log.Warn("Json file missing important data for loading, using APIs to load instead.");
                Deserializing = false;
                ReloadScores();
                Deserializing = true;
            }
        }
        private void InitTable()
        {
            //Null check
            if (TextContainer is null || PlayTable is not null) return;

            //Label setup
            string[] names = [ 
                "<color=#FA0>Score #</color>",
                "Beatmap Name",
                "<color=#0F0>D</color><color=#FF0>i</color><color=#F70>f</color><color=#C16>f</color>",
                $"<color=purple>{(IsAP ? "AP" : "PP")}</color>",
                "<color=#4AF>Key</color>" 
            ];
            if (IsAP) names = [.. names, "<color=#777>Catagory</color>"];

            //Value setup
            string[][] values = GetTableValues();

            //PlayTable setup
            PlayTable = new Table(TextContainer, values, names)
            {
                HasEndColumn = true
            };
        }

        #endregion
        #region Reload Functions
        public void ReloadScores()
        {
            TotalPP = -1;
            PlusOne = -1;
            InitScores();
            ReloadTableValues();
        }
        public void ReloadTableValues()
        {
            if (PlayTable is null) return;
            PlayTable.SetValues(GetTableValues());
        }
        private string[][] GetTableValues()
        {
            //Plugin.Log.Info($"Null Checks: {HelpfulMisc.Print(new object[] { Scores is null, ScoreNames is null, ActualScoreDiffs is null, ScoreIDs is null, IsAP && ActualAPCategories is null })}");
            //Page number clamping
            if (PageNumber * PlaysToShow > Scores.Length) PageNumber = Scores.Length / PlaysToShow;
            if (PageNumber <= 0) PageNumber = 1;

            //Value setup
            string[][] values = new string[Math.Min(PlaysToShow, Scores.Length - (PageNumber - 1) * PlaysToShow)][];
            for (int i = 0, j = (PageNumber - 1) * PlaysToShow; i < values.Length; i++, j++)
            {
                values[i] = [
                $"<color=#FA0>#{j + 1}</color>",
                ScoreNames[j].ClampString(40),
                ColorizeDiff(ActualScoreDiffs[j]),
                $"<color=purple>{Math.Round(Scores[j], PluginConfig.Instance.DecimalPrecision)}</color> {(IsAP ? "AP" : "PP")}",
                $"<color=#4AF>{ScoreIDs[j]}</color>"
                ];
                if (IsAP) values[i] = [.. values[i], $"<color=#CCC>{ActualAPCategories[j]}</color>"];
            }
            return values;
        }
        #endregion
        #region Static Functions
        private static string ColorizeDiff(BeatmapDifficulty diff) => diff switch
            {
                BeatmapDifficulty.Easy => "<color=#0F0>Easy</color>",
                BeatmapDifficulty.Normal => "<color=#FF0>Normal</color>",
                BeatmapDifficulty.Hard => "<color=#F70>Hard</color>",
                BeatmapDifficulty.Expert => "<color=#C16>Expert</color>",
                BeatmapDifficulty.ExpertPlus => "<color=#F0F>Expert+</color>",
                _ => null,
            };
        internal static int GetPlusOneCount(Leaderboards leaderboard) => leaderboard switch
            {
                Leaderboards.Beatleader or Leaderboards.Scoresaber or Leaderboards.Accsaber => 100,
                _ => 0,
            };

        /// <summary>
        /// Saves the given <paramref name="profile"/> to the <see cref="LoadedProfiles"/> dictionary.
        /// </summary>
        /// <param name="profile">The <see cref="Profile"/> to save.</param>
        public static void SaveProfile(Profile profile)
        {
            if (LoadedProfiles.ContainsKey(profile.ID))
                LoadedProfiles[profile.ID] = profile;
            else
                LoadedProfiles.Add(profile.ID, profile);
        }
        /// <summary>
        /// Saves all profiles loaded in the <see cref="LoadedProfiles"/> dictionary to the file at the path <see cref="HelpfulPaths.PROFILE_DATA"/>.
        /// </summary>
        internal static void SaveAllProfiles()
        {
            if (LoadedProfiles.Count == 0) return;
            IEnumerable<JToken> data = LoadedProfiles.Select(p => JToken.FromObject(p.Value));
            using StreamWriter sw = new(HelpfulPaths.PROFILE_DATA);
            JsonSerializer serializer = new();
            serializer.Serialize(sw, data, typeof(Profile));
        }
        /// <summary>
        /// Loads all profiles from the file at the path <see cref="HelpfulPaths.PROFILE_DATA"/> and saves it to the dictionary <see cref="LoadedProfiles"/>.
        /// </summary>
        internal static void LoadAllProfiles()
        {
            if (!File.Exists(HelpfulPaths.PROFILE_DATA)) return;
            Task.Run(() =>
            {
                try
                {
                    Deserializing = true;
                    JsonSerializer serializer = new();
                    IEnumerable<Profile> profiles;
                    using (StreamReader reader = File.OpenText(HelpfulPaths.PROFILE_DATA))
                        profiles = serializer.Deserialize(reader, typeof(IEnumerable<Profile>)) as IEnumerable<Profile>;
                    foreach (Profile profile in profiles)
                    {
                        profile.PostInit();
                        LoadedProfiles.Add(profile.ID, profile);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.Error("Profiles failed to load!");
                    if (e is JsonSerializationException)
                    {
                        Plugin.Log.Error($"The {HelpfulPaths.PROFILE_DATA} file has bad json data in it. Deleting it.");
                        File.Delete(HelpfulPaths.PROFILE_DATA);
                    }
                    Plugin.Log.Error(e);
                }
                finally
                {
                    Deserializing = false;
                }
            });
            //Plugin.Log.Info("Profiles have been loaded.");
        }
        /// <summary>
        /// Gets a profile using the given <paramref name="leaderboard"/> and <paramref name="userID"/> from the <see cref="LoadedProfiles"/> dictionary.
        /// If the <see cref="Profile"/> is not found in the dictionary, then this function will create a new one, add it to the dictionary, and return it.
        /// </summary>
        /// <param name="leaderboard">The leaderboard to use.</param>
        /// <param name="userID">The user ID (Steam ID) to use.</param>
        /// <returns>The <see cref="Profile"/> either from the <see cref="LoadedProfiles"/> dictionary or newly made. This will never return null.</returns>
        public static Profile GetProfile(Leaderboards leaderboard, string userID, APCategory accSaberType = APCategory.None)
        {
            if (LoadedProfiles.TryGetValue(GetID(leaderboard, userID, accSaberType), out Profile profile))
                return profile;
            else
            {
                profile = leaderboard == Leaderboards.Accsaber ? new Profile(leaderboard, userID, accSaberType) : new Profile(leaderboard, userID);
                SaveProfile(profile);
                return profile;
            }
        }
        /// <summary>
        /// Attempts to get a <see cref="Profile"/> using the given <paramref name="leaderboard"/> and <paramref name="userID"/>.
        /// </summary>
        /// <param name="leaderboard">The leaderboard to use.</param>
        /// <param name="userID">The user ID (Steam ID) to use.</param>
        /// <returns>Either the <see cref="Profile"/> from the dictionary or, if it is not found, null.</returns>
        public static Profile TryGetProfile(Leaderboards leaderboard, string userID, APCategory accSaberType = APCategory.None) => 
            LoadedProfiles.ContainsKey(GetID(leaderboard, userID, accSaberType)) ? LoadedProfiles[GetID(leaderboard, userID, accSaberType)] : null;
        /// <summary>
        /// Converts a given <paramref name="leaderboard"/> and <paramref name="userID"/> into an ID string used as the key in the <see cref="LoadedProfiles"/> dictionary.
        /// There may also be an <paramref name="accSaberType"/> given when the <paramref name="leaderboard"/> is <see cref="Leaderboards.Accsaber"/>.
        /// </summary>
        /// <param name="leaderboard">The leaderboard to use.</param>
        /// <param name="userID">The user ID (Steam ID) to use.</param>
        /// <param name="accSaberType">The type of accSaber plays to track.</param>
        /// <returns>A string containing the <paramref name="userID"/> and <paramref name="leaderboard"/> (leaderboard as an int).
        /// If <paramref name="accSaberType"/> is given, then there is also a third int representing the <paramref name="accSaberType"/>.</returns>
        public static string GetID(Leaderboards leaderboard, string userID, APCategory accSaberType = APCategory.None) => 
            $"{userID}_{(int)leaderboard}{(accSaberType != APCategory.None ? "_" + (int)accSaberType : "")}";
        /// <summary>
        /// Adds a given play to all leaderboards that the play is ranked on.
        /// </summary>
        /// <param name="userID">The player who set the score.</param>
        /// <param name="hash">The hash of the map the score was set on.</param>
        /// <param name="acc">The accuracy the player set for the score.</param>
        /// <param name="mapName">The name of the map the score was set on.</param>
        /// <param name="mapDiff">The difficulty of the map that the score was set on.</param>
        /// <param name="mode">The mode of the play (BeatmapCharacteristic). Defaults to <see cref="DEFAULT_MODE"/> if none is given.</param>
        /// <returns>Whether or not the score was good enough to enter any leaderboard's <see cref="Scores"/> array.</returns>
        public static async Task<bool> AddPlay(string userID, string hash, float acc, string mapName, BeatmapDifficulty mapDiff, string mode = "")
        {
            if (mode.Length == 0) mode = DEFAULT_MODE;
            int currentNum = 1;
            Leaderboards current;
            (Leaderboards allowed, string mapKey) = await APIHandler.GetRankedLeaderboardsAndMapKey(hash, mapDiff, mode);
            if (allowed == Leaderboards.None) return false;
            int leaderCount = (int)Math.Log((int)Leaderboards.All + 1, 2);
            string currentMode;
            GameplayModifiers mods = TheCounter.LastMods;
            bool goodScore = false;
            for (int i = 0; i < leaderCount; i++)
            {
                current = (Leaderboards)currentNum;
                //Plugin.Log.Info($"Allowed: {allowed} || Current: {current}");
                if ((allowed & current) != Leaderboards.None)
                {
                    try
                    {
                        currentMode = TheCounter.SelectMode(mode, current);
                        Calculator calc = Calculator.GetCalc(current);
                        float[] ratings = calc.SelectRatings(TheCounter.GetDifficulty(await TheCounter.GetMap(hash, currentMode, current), mapDiff, current, currentMode, mods, true));
                        float pp = calc.Inflate(calc.GetSummedPp(acc, ratings));
                        if (current != Leaderboards.Accsaber)
                            goodScore |= GetProfile(current, userID).AddPlay(pp, mapName, mapKey, mapDiff, mode);
                        else
                        {
                            (_, HttpContent data) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.SSAPI_HASH, hash, "info", Map.FromDiff(mapDiff)), SSAPI.Throttle);
                            (_, data) = await APIHandler.CallAPI_Static(string.Format(HelpfulPaths.APAPI_LEADERBOARDID, JToken.Parse(await data.ReadAsStringAsync())["id"].ToString()));
                            APCategory accSaberCategory = (APCategory)Enum.Parse(typeof(APCategory), JToken.Parse(await data.ReadAsStringAsync())["categoryDisplayName"].ToString().Split(' ')[0]);
                            goodScore |= GetProfile(current, userID, accSaberCategory).AddPlay(pp, mapName, mapKey, mapDiff, mode);
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.Error($"There was an issue adding play for leaderboard {current}!\n{e}");
                    }
                }
                currentNum <<= 1;
            }
            return goodScore;
        }
        #endregion
        #region Misc Functions
        public void PageUp()
        {
            PageNumber--;
            ReloadTableValues();
        }
        public void PageDown()
        {
            PageNumber++;
            ReloadTableValues();
        }
        public void PageTop()
        {
            PageNumber = 1;
            ReloadTableValues();
        }
        private string GetMode(int index)
        {
            if (index < 0 || index > Scores.Length) throw new ArgumentOutOfRangeException("index");
            if (!UnusualModeIndexes.Contains(index)) return DEFAULT_MODE;
            var (Index, Mode) = UnusualModes.FirstOrDefault(vals => vals.Index >= index);
            return Index == index ? Mode : null;
        }
        /// <summary>
        /// Given the <paramref name="scoreNum"/> (NOT zero indexed, starts at 1), returns what weight that score will have.
        /// </summary>
        /// <param name="scoreNum">The number score to weight (NOT zero indexed, starts at 1).</param>
        /// <returns>The weight for the given score index.</returns>
        private float GetWeight(int scoreNum)
        {
            scoreNum--; //Makes it zero indexed.
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return Mathf.Pow(0.965f, scoreNum);
                case Leaderboards.Accsaber:
                    const float k = 0.4f;
                    return AccsaberConsts[1] / (1 + Mathf.Exp(k * (scoreNum - AccsaberConsts[0])));
                default:
                    return 1;
            }
        }
        /// <summary>
        /// Given the weight of a score, this will find the weight of the next score (ex: Given the weight of score #5, it finds the weight for score #6).
        /// </summary>
        /// <param name="lastWeight">The weight of a score.</param>
        /// <returns>The weight of a score directly after <paramref name="lastWeight"/>.</returns>
        private float GetNextWeight(float lastWeight)
        {
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return lastWeight * 0.965f;
                case Leaderboards.Accsaber:
                    const float e = 1.49182469764f; //e^k
                    return 1 / (e / lastWeight - AccsaberConsts[2]);
                default:
                    return lastWeight;
            }
        }
        /// <summary>
        /// Given the weight of a score, this will find the weight of the previous score (ex: Given the weight of score #5, it will find the weight for score #4).
        /// </summary>
        /// <param name="lastWeight">The weight of the score.</param>
        /// <returns>The weight directly before <paramref name="lastWeight"/>.</returns>
        private float GetPreviousWeight(float lastWeight)
        {
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return lastWeight / 0.965f;
                case Leaderboards.Accsaber:
                    const float e = 1.49182469764f; //e^k
                    return 1 / (1 / (e * lastWeight) - AccsaberConsts[3]);
                default:
                    return lastWeight;
            }
        }
        /// <summary>
        /// Populates the <see cref="WeightedScores"/> array using the <see cref="Scores"/> array by weighting each value.
        /// </summary>
        private void WeightScores()
        {
            if (WeightedScores is null || WeightedScores.Length != Scores.Length) WeightedScores = new float[Scores.Length];
            float weight = GetWeight(1);
            for (int i = 0; i < WeightedScores.Length; i++)
            {
                WeightedScores[i] = weight * Scores[i];
                weight = GetNextWeight(weight);
            }
            //Plugin.Log.Info($"Scores: {HelpfulMisc.Print(Scores)}\nWeighted Scores: {HelpfulMisc.Print(WeightedScores)}");
        }
        /// <summary>
        /// Simple switch statement for how many scores to ask the API for so that we can accurately calculate <see cref="PlusOne"/>.
        /// </summary>
        /// <returns>The number of scores needed to calculate <see cref="PlusOne"/>.</returns>
        private int GetPlusOneCount() => GetPlusOneCount(Leaderboard);
        /// <summary>
        /// Given <paramref name="rawPP"/>, finds what the weighted value is.
        /// </summary>
        /// <param name="rawPP">The raw pp value.</param>
        /// <returns>Either the weighted value of <paramref name="rawPP"/>, or -1. This returns -1 when <paramref name="rawPP"/> is lower than the
        /// lowest score in the <see cref="Scores"/> array.</returns>
        public float GetWeightedPP(float rawPP)
        {
            if (Scores[Scores.Length - 1] > rawPP) return -1;
            return (float)Math.Round(GetWeight(HelpfulMisc.ReverseBinarySearch(Scores, rawPP) + 1) * rawPP, PluginConfig.Instance.DecimalPrecision);
        }
        /// <summary>
        /// Given <paramref name="weightedPP"/>, calculate how much pp this will gain to the profile of the player.
        /// </summary>
        /// <param name="weightedPP">The weighted pp value.</param>
        /// <returns>The profile PP gained.</returns>
        public float GetProfilePP(float weightedPP) => GetProfilePP(weightedPP, HelpfulMisc.ReverseBinarySearch(WeightedScores, weightedPP) + 1);
        /// <summary>
        /// Given <paramref name="rawPP"/>, calculate how much pp this will gain to the profile of the player.
        /// </summary>
        /// <param name="rawPP">The raw pp value.</param>
        /// <returns>The profile PP gained.</returns>
        public float GetProfilePPRaw(float rawPP) => GetProfilePP(GetWeightedPP(rawPP), HelpfulMisc.ReverseBinarySearch(Scores, rawPP) + 1);
        /// <summary>
        /// The main profile PP calculator function.
        /// </summary>
        /// <param name="pp">The weighted pp value.</param>
        /// <param name="scores">The loaded weighted pp values in an array.</param>
        /// <param name="index">The score number (NOT zero index, starts at 1).</param>
        /// <returns>The profile pp gained from such a score.</returns>
        private float GetProfilePP(float weightedPp, int index, int ignoredScore = -1)
        {
            float weightedSum = TotalPP;
            if (index-- >= WeightedScores.Length)
                return -1;
            float outp;
            if (Leaderboard == Leaderboards.Accsaber)
            {//Gotta do things a bit different with accsaber due to the way they weight stuff
                float offset = 0, currentWeight = GetWeight(index + 2);
                for (; index < WeightedScores.Length; index++)
                {
                    if (index != ignoredScore)
                        offset += WeightedScores[index] - Scores[index] * currentWeight;
                    currentWeight = GetNextWeight(currentWeight);
                }
                outp = (float)Math.Round(weightedPp - offset, PluginConfig.Instance.DecimalPrecision);
            }
            else
            {
                for (int j = 0; j < index; weightedSum -= WeightedScores[j], j++) ;
                outp = (float)Math.Round(weightedPp - (1.0f - GetWeight(2)) * weightedSum, PluginConfig.Instance.DecimalPrecision);
            }
            return outp < 0 ? 0 : outp;
        }
        /// <summary>
        /// Calculates the plus one value using internal variables.
        /// </summary>
        /// <returns>What raw pp score is needed to gain one profile pp.</returns>
        public float CalculatePlusOne()
        {
            if (Leaderboard == Leaderboards.Accsaber) return MyPlusOne();
            int i = 0;
            float shiftWeight = 1.0f - GetWeight(2);
            float weightedSum = TotalPP;
            for (; i < WeightedScores.Length && WeightedScores[i] - shiftWeight * weightedSum >= 1.0f; weightedSum -= WeightedScores[i], i++) ;
            return i < WeightedScores.Length ?
                (float)Math.Round((1 + shiftWeight * weightedSum) / GetWeight(i + 1), PluginConfig.Instance.DecimalPrecision) : 0;
        }
        public float MyPlusOne()
        {
            float savedTotal = WeightedScores.Aggregate(0.0f, (total, current) => total + current);
            //I do a check below for if there is any significance to weights past the scores stored. If there is, account for it, otherwise pretend there isn't anything past what is stored. 
            float totalOffset = (TotalPP - savedTotal) * (GetWeight(Scores.Length) * Scores[0] * 100 > 1 ? GetWeight(2) : 0);
            //Plugin.Log.Info($"Given: {TotalPP} || Calculated: {savedTotal} || Start offset {totalOffset}");
            float currentWeight = GetWeight(Scores.Length + 1);
            int i = Scores.Length - 1;
            for (; i > 0; i--)
            {
                totalOffset += WeightedScores[i] - Scores[i] * currentWeight;
                currentWeight = GetPreviousWeight(currentWeight);
                //Plugin.Log.Info($"#{i}: Testing: {Scores[i - 1] * currentWeight} || CurrentOffset: {totalOffset}");
                if (Scores[i - 1] * currentWeight - totalOffset >= 1.0f) break;
            }
            //Plugin.Log.Info("Index: " + i);
            return (float)Math.Round((1.0f + totalOffset) / currentWeight, PluginConfig.Instance.DecimalPrecision);
        }
        /// <summary>
        /// Adds a play to the <see cref="Scores"/> array.
        /// </summary>
        /// <param name="rawPP">The raw pp value to add to this <see cref="Profile"/>.</param>
        /// <param name="mapName">The name of the map this was set on.</param>
        /// <param name="mapKey">The beatsaver key for the map.</param>
        /// <param name="diff">The difficulty of the map that this score set on.</param>
        /// <param name="mode">The BeatmapCharacteristic of the map.</param>
        /// <returns>Returns whether or not the score was good enough to enter the <see cref="Scores"/> array.</returns>
        public bool AddPlay(float rawPP, string mapName, string mapKey, BeatmapDifficulty diff, string mode, bool ignoreSession = false)
        {
            Plugin.Log.Info($"{Leaderboard}{(Leaderboard == Leaderboards.Accsaber ? $" ({AccSaberType})" : "")}: {HelpfulMisc.Print(new object[] { rawPP, mapName, diff })}");
            if (float.IsNaN(rawPP) || Scores[Scores.Length - 1] > rawPP) return false;
            //Plugin.Log.Debug($"Recieved score: " + rawPP);
            //Plugin.Log.Debug("AddPlay Index: " +  index);
            (bool isDupe, bool isBetter) = CheckForDupePlay(rawPP, mapKey, diff, mode, out int dupeIndex);
            if (isDupe && !isBetter) return false;

            if (isDupe)
            {
                float current = WeightedScores.Aggregate(0.0f, (total, current) => total + current);
                HelpfulMisc.SiftUp(Scores, dupeIndex);
                HelpfulMisc.SiftUp(ScoreNames, dupeIndex);
                HelpfulMisc.SiftUp(ActualScoreDiffs, dupeIndex);
                HelpfulMisc.SiftUp(ScoreIDs, dupeIndex);
                HelpfulMisc.SiftUp(WeightedScores, dupeIndex);
                WeightScores();
                TotalPP -= current - WeightedScores.Aggregate(0.0f, (total, current) => total + current);
            }

            int index = HelpfulMisc.ReverseBinarySearch(Scores, rawPP);
            if (index >= Scores.Length) return false;
            float profilePP = GetProfilePP(GetWeightedPP(rawPP), index + 1);
            float oldScore = ScoreIDs.IndexOf(mapKey);
            oldScore = oldScore >= 0 ? Scores[(int)oldScore] : 0;
            if (!ignoreSession) CurrentSession.AddPlay(mapName, mapKey, diff, mode, rawPP, profilePP, oldScore);
            if (profilePP > 0) TotalPP += profilePP;

            HelpfulMisc.SiftDown(Scores, index, rawPP);
            HelpfulMisc.SiftDown(ScoreNames, index, mapName);
            HelpfulMisc.SiftDown(ActualScoreDiffs, index, diff);
            HelpfulMisc.SiftDown(ScoreIDs, index, mapKey);
            WeightScores();

            PlusOne = CalculatePlusOne();
            if (index != dupeIndex)
                ScoreDiffs = HelpfulMisc.CompressEnums(ActualScoreDiffs);
            if (Leaderboard == Leaderboards.Accsaber && AccSaberType != APCategory.All)
            {
                Profile p = GetProfile(Leaderboards.Accsaber, UserID, APCategory.All);
                p.AddPlay(rawPP, mapName, mapKey, diff, mode, true);
                p.CurrentSession.AddPlay(mapName, mapKey, diff, mode, rawPP, profilePP, oldScore);
            }
            ReloadTableValues();
            return true;
        }
        private (bool IsDupe, bool IsBetter) CheckForDupePlay(float rawPP, string mapKey, BeatmapDifficulty diff, string mode, out int dupeIndex)
        {
            int index = Array.IndexOf(ScoreIDs, mapKey);
            while (index >= 0 && index + 1 < ScoreIDs.Length)
            {
                //Plugin.Log.Info($"Checking index {index} ({mapKey})");
                dupeIndex = index;
                if (ActualScoreDiffs[index] == diff && mode.Equals(GetMode(index))) return (true, rawPP > Scores[index]);
                index = Array.IndexOf(ScoreIDs, mapKey, index + 1);
            }
            dupeIndex = -1;
            return (false, false);
        }
        #endregion
    }
}
