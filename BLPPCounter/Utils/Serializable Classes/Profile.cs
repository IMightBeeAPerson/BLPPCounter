using BeatLeader.Models;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.API_Handlers;
using IPA.Config.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BLPPCounter.Utils
{
    internal class Profile
    {
#pragma warning disable IDE0051
        #region Static Variables
        private static readonly float[] AccsaberConsts;
        private static readonly Dictionary<string, Profile> LoadedProfiles = new Dictionary<string, Profile>();
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
        [JsonIgnore]
        private const int PlaysToShow = 10;
        [JsonIgnore]
        private string ID => UserID + "_" + (int)Leaderboard;
        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)]
        private readonly Leaderboards Leaderboard;
        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)]
        private float[] Scores;
        [JsonProperty(nameof(ScoreNames), Required = Required.DisallowNull)]
        private string[] ScoreNames;
        [JsonProperty(nameof(ScoreDiffs), Required = Required.DisallowNull)]
        private ulong[] ScoreDiffs;
        [JsonIgnore]
        private BeatmapDifficulty[] ActualScoreDiffs;
        [JsonProperty(nameof(ScoreIDs), Required = Required.DisallowNull)]
        private string[] ScoreIDs;
        [JsonIgnore]
        private float[] WeightedScores;
        [JsonProperty(nameof(TotalPP), Required = Required.DisallowNull)]
        private float TotalPP = -1.0f;
        [JsonProperty(nameof(UserID), Required = Required.DisallowNull)]
        private readonly string UserID;
        [JsonProperty(nameof(PlusOne), Required = Required.DisallowNull)]
        public float PlusOne { get; private set; } = -1.0f;
        [JsonIgnore]
        public Table PlayTable { get; private set; }
        [JsonIgnore]
        private int PageNumber;
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
        public Profile(Leaderboards leaderboard, string userID)
        {
            Leaderboard = leaderboard;
            UserID = userID;
            PageNumber = 1;
            InitScores();
        }
        private void InitScores()
        {
            if (Deserializing) return;
            if (TotalPP < 0)
            {
                APIHandler api = APIHandler.GetAPI(Leaderboard);
                var scoreData = api.GetScores(UserID, GetPlusOneCount())?.Result;
                if (scoreData is null)
                {
                    Scores = new float[1] { 0.0f };
                    ScoreNames = new string[1] { "No Scores" };
                    ActualScoreDiffs = new BeatmapDifficulty[1] { BeatmapDifficulty.Normal };
                    ScoreDiffs = new ulong[1] { 1UL };
                    ScoreIDs = new string[1] { "12345" };
                }
                else
                {
                    Scores = scoreData.Select(token => token.RawPP).ToArray();
                    ScoreNames = scoreData.Select(token => token.MapName).ToArray();
                    ActualScoreDiffs = scoreData.Select(token => token.Difficulty).ToArray();
                    ScoreDiffs = HelpfulMisc.CompressEnums(ActualScoreDiffs);
                    ScoreIDs = scoreData.Select(token => token.MapId).ToArray();
                }
                TotalPP = api.GetProfilePP(UserID).Result;
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
                WeightScores();
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
            if (TextContainer is null || !(PlayTable is null)) return;

            //Label setup
            string[] names = new string[5] { 
                "<color=#FA0>Score #</color>",
                "Beatmap Name",
                "<color=#0F0>D</color><color=#FF0>i</color><color=#F70>f</color><color=#C16>f</color>",
                $"<color=purple>{(Leaderboard == Leaderboards.Accsaber ? "AP" : "PP")}</color>",
                "<color=#4AF>Key</color>" 
            };

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
            //Page number clamping
            if (PageNumber * PlaysToShow > Scores.Length) PageNumber = Scores.Length / PlaysToShow;
            if (PageNumber <= 0) PageNumber = 1;

            //Value setup
            string[][] values = new string[Math.Min(PlaysToShow, Scores.Length - (PageNumber - 1) * PlaysToShow)][];
            for (int i = 0, j = (PageNumber - 1) * PlaysToShow; i < values.Length; i++, j++)
            {
                values[i] = new string[] {
                $"<color=#FA0>#{j + 1}</color>",
                ScoreNames[j].ClampString(40),
                ColorizeDiff(ActualScoreDiffs[j]),
                $"<color=purple>{Math.Round(Scores[j], PluginConfig.Instance.DecimalPrecision)}</color> {(Leaderboard == Leaderboards.Accsaber ? "AP" : "PP")}",
                $"<color=#4AF>{ScoreIDs[j]}</color>"
                };
            }
            return values;
        }
        #endregion
        #region Static Functions
        private static string ColorizeDiff(BeatmapDifficulty diff)
        {
            switch (diff)
            {
                case BeatmapDifficulty.Easy:
                    return "<color=#0F0>Easy</color>";
                case BeatmapDifficulty.Normal:
                    return "<color=#FF0>Normal</color>";
                case BeatmapDifficulty.Hard:
                    return "<color=#F70>Hard</color>";
                case BeatmapDifficulty.Expert:
                    return "<color=#C16>Expert</color>";
                case BeatmapDifficulty.ExpertPlus:
                    return "<color=#F0F>Expert+</color>";
                default:
                    return null;
            }
        }
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
            IEnumerable<JToken> data = LoadedProfiles.Where(kvp => int.Parse(kvp.Key.Split('_')[1]) != (int)Leaderboards.Accsaber).Select(p => JToken.FromObject(p.Value));
            byte[] rawData = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(HelpfulPaths.PROFILE_DATA)) File.Delete(HelpfulPaths.PROFILE_DATA);
            using (FileStream fs = File.OpenWrite(HelpfulPaths.PROFILE_DATA))
                fs.Write(rawData, 0, rawData.Length);
            //Plugin.Log.Info("Profiles have been saved.");
        }
        /// <summary>
        /// Loads all profiles from the file at the path <see cref="HelpfulPaths.PROFILE_DATA"/> and saves it to the dictionary <see cref="LoadedProfiles"/>.
        /// </summary>
        internal static void LoadAllProfiles()
        {
            if (!File.Exists(HelpfulPaths.PROFILE_DATA)) return;
            JEnumerable<JToken> profiles;
            try
            {
                profiles = JToken.Parse(File.ReadAllText(HelpfulPaths.PROFILE_DATA)).Children();
            } catch (Exception e)
            {
                Plugin.Log.Error("Profiles failed to load!\n" + e.ToString());
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    Deserializing = true;
                    foreach (JToken profileItem in profiles)
                    {
                        Profile profile = profileItem.ToObject<Profile>();
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
                    else Plugin.Log.Error(e);
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
        public static Profile GetProfile(Leaderboards leaderboard, string userID)
        {
            if (LoadedProfiles.TryGetValue(GetID(leaderboard, userID), out Profile profile))
                return profile;
            else
            {
                profile = new Profile(leaderboard, userID);
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
        public static Profile TryGetProfile(Leaderboards leaderboard, string userID) => 
            LoadedProfiles.ContainsKey(GetID(leaderboard, userID)) ? LoadedProfiles[GetID(leaderboard, userID)] : null;
        /// <summary>
        /// Converts a given <paramref name="leaderboard"/> and <paramref name="userID"/> into an ID string used as the key in the <see cref="LoadedProfiles"/>
        /// dictionary.
        /// </summary>
        /// <param name="leaderboard">The leaderboard to use.</param>
        /// <param name="userID">The user ID (Steam ID) to use.</param>
        /// <returns>A string containing the <paramref name="userID"/> and <paramref name="leaderboard"/> (leaderboard as an int).</returns>
        public static string GetID(Leaderboards leaderboard, string userID) => userID + "_" + (int)leaderboard;
        /// <summary>
        /// Adds a given play to all leaderboards that the play is ranked on.
        /// </summary>
        /// <param name="userID">The player who set the score.</param>
        /// <param name="hash">The hash of the map the score was set on.</param>
        /// <param name="acc">The accuracy the player set for the score.</param>
        /// <param name="mapName">The name of the map the score was set on.</param>
        /// <param name="mapDiff">The difficulty of the map that the score was set on.</param>
        /// <returns>Whether or not the score was good enough to enter any leaderboard's <see cref="Scores"/> array.</returns>
        public static bool AddPlay(string userID, string hash, float acc, string mapName, BeatmapDifficulty mapDiff)
        {
            int currentNum = 1;
            Leaderboards allowed = APIHandler.GetRankedLeaderboards(hash).Result, current;
            if (allowed == Leaderboards.None) return false;
            int leaderCount = (int)Math.Log((int)allowed, 2) + 1;
            float[] ratings = new float[] {TheCounter.LastMap.StarRating, TheCounter.LastMap.AccRating, TheCounter.LastMap.PassRating, TheCounter.LastMap.TechRating};
            bool goodScore = false;
            for (int i = 0; i < leaderCount; i++)
            {
                current = (Leaderboards)currentNum;
                if ((allowed & current) != Leaderboards.None)
                {
                    Calculator calc = Calculator.GetCalc(current);
                    goodScore |= GetProfile(current, userID).AddPlay(calc.Inflate(calc.GetSummedPp(acc, calc.SelectRatings(ratings))), mapName, mapDiff);
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
        /// <summary>
        /// Given the <paramref name="scoreNum"/> (NOT zero indexed, starts at 1), returns what weight that score will have.
        /// </summary>
        /// <param name="scoreNum">The number score to weight (NOT zero indexed, starts at 1).</param>
        /// <returns>The weight for the given score index.</returns>
        private float GetWeight(int scoreNum)
        {
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return Mathf.Pow(0.965f, scoreNum - 1);
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
            WeightedScores = new float[Scores.Length];
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
        private int GetPlusOneCount()
        {
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return 100;
                case Leaderboards.Accsaber:
                    return 50;
                default:
                    return 0;
            }
        }
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
        public float GetProfilePP(float weightedPP) => GetProfilePP(weightedPP, WeightedScores, HelpfulMisc.ReverseBinarySearch(WeightedScores, weightedPP) + 1);
        /// <summary>
        /// Given <paramref name="rawPP"/>, calculate how much pp this will gain to the profile of the player.
        /// </summary>
        /// <param name="rawPP">The raw pp value.</param>
        /// <returns>The profile PP gained.</returns>
        public float GetProfilePPRaw(float rawPP) => GetProfilePP(GetWeightedPP(rawPP), WeightedScores, HelpfulMisc.ReverseBinarySearch(Scores, rawPP) + 1);
        /// <summary>
        /// The main profile PP calculator function.
        /// </summary>
        /// <param name="pp">The weighted pp value.</param>
        /// <param name="scores">The loaded weighted pp values in an array.</param>
        /// <param name="index">The score number (NOT zero index, starts at 1).</param>
        /// <returns>The profile pp gained from such a score.</returns>
        private float GetProfilePP(float pp, float[] scores, int index)
        {
            float weightedSum = TotalPP;
            if (index-- >= scores.Length)
                return -1;
            else for (int j = 0; j < index; weightedSum -= scores[j], j++) ;
            float outp = (float)Math.Round(pp - (1.0f - GetWeight(2)) * weightedSum, PluginConfig.Instance.DecimalPrecision);
            return outp < 0 ? 0 : outp;
        }
        /// <summary>
        /// Calculates the plus one value using internal variables.
        /// </summary>
        /// <returns>What raw pp score is needed to gain one profile pp.</returns>
        public float CalculatePlusOne()
        {
            int i = 0;
            float shiftWeight = 1.0f - GetWeight(2);
            float weightedSum = TotalPP;
            for (; i < WeightedScores.Length && WeightedScores[i] - shiftWeight * weightedSum >= 1.0f; weightedSum -= WeightedScores[i], i++) ;
            return i < WeightedScores.Length ?
                (float)Math.Round((1 + shiftWeight * weightedSum) / GetWeight(i + 1), PluginConfig.Instance.DecimalPrecision) : 0;
        }
        public float CalculatePlusOne(int scoreNum)
        {
            int i = 0;
            float shiftWeight = 1.0f - GetWeight(2);
            float weightedSum = TotalPP;
            float currentMult = GetWeight(scoreNum + 1);
            while (i < WeightedScores.Length && WeightedScores[i] - shiftWeight * weightedSum >= 1.0f)
            {
                if (i == scoreNum - 1) continue;
                if (i >= scoreNum)
                {
                    weightedSum -= Scores[i] * currentMult;
                    currentMult = GetNextWeight(scoreNum);
                } else
                    weightedSum -= WeightedScores[i];
                i++;
            }
            return i < WeightedScores.Length ?
                (float)Math.Round((1 + shiftWeight * weightedSum) / GetWeight(i + 1), PluginConfig.Instance.DecimalPrecision) : 0;
        }
        /// <summary>
        /// Adds a play to the <see cref="Scores"/> array.
        /// </summary>
        /// <param name="rawPP">The raw pp value to add to this <see cref="Profile"/>.</param>
        /// <param name="mapName">The name of the map this was set on.</param>
        /// <param name="diff">The difficulty of the map that this score set on.</param>
        /// <returns>Returns whether or not the score was good enough to enter the <see cref="Scores"/> array.</returns>
        public bool AddPlay(float rawPP, string mapName, BeatmapDifficulty diff)
        {
            if (float.IsNaN(rawPP) || Scores[Scores.Length - 1] > rawPP) return false;
            //Plugin.Log.Debug($"Recieved score: " + rawPP);
            int index = HelpfulMisc.ReverseBinarySearch(Scores, rawPP);
            //Plugin.Log.Debug("AddPlay Index: " +  index);
            if (index >= Scores.Length) return false;
            float profilePP = GetProfilePP(GetWeightedPP(rawPP), WeightedScores, index + 1);
            if (profilePP > 0) TotalPP += profilePP;
            HelpfulMisc.SiftDown(Scores, index, rawPP);
            HelpfulMisc.SiftDown(ScoreNames, index, mapName);
            HelpfulMisc.SiftDown(ActualScoreDiffs, index, diff);
            HelpfulMisc.SiftDown(WeightedScores, index, GetWeight(index + 1) * rawPP, GetWeight(2));
            PlusOne = CalculatePlusOne();
            ScoreDiffs = HelpfulMisc.CompressEnums(ActualScoreDiffs);
            ReloadTableValues();
            return true;
        }
        #endregion
    }
}
