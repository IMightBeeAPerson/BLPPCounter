using BeatLeader.Models;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils.API_Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        #endregion
        #region Variables
        [JsonIgnore]
        private string ID => UserID + "_" + (int)Leaderboard;
        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)]
        private readonly Leaderboards Leaderboard;
        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)]
        private float[] Scores;
        [JsonProperty(nameof(ScoreNames), Required = Required.DisallowNull)]
        private string[] ScoreNames;
        [JsonProperty(nameof(ScoreDiffs), Required = Required.DisallowNull)]
        private string[] ScoreDiffs;
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
            InitScores();
        }
        private void InitScores()
        {
            if (TotalPP < 0)
            {
                APIHandler api = APIHandler.GetAPI(Leaderboard);
                var scoreData = api.GetScores(UserID, GetPlusOneCount());
                Scores = scoreData.Select(token => token.rawPP).ToArray();
                ScoreNames = scoreData.Select(token => token.MapName).ToArray();
                ScoreDiffs = scoreData.Select(token => token.Difficulty.ToString()).ToArray();
                TotalPP = api.GetProfilePP(UserID);
                if (Scores is null) Scores = new float[1] { 0.0f };
                InitTable();
            }
            WeightScores();
            if (PlusOne < 0)
                PlusOne = CalculatePlusOne();
        }
        private void InitTable()
        {
            if (TextContainer is null) return;
            const int PlaysToShow = 10;
            string label = Leaderboard == Leaderboards.Accsaber ? "AP" : "PP";
            string[] names = new string[4] { "Score #", "Name", "Diff", label };
            string[][] values = new string[Math.Min(PlaysToShow, Scores.Length)][];
            for (int i = 0; i < values.Length; i++)
                values[i] = new string[] {
                $"<color=#0F0>#{i + 1}</color>",
                ScoreNames[i],
                ScoreDiffs[i],
                $"<color=purple>{Math.Round(Scores[i], PluginConfig.Instance.DecimalPrecision)}</color> {label}"
                };
            if (PlayTable is null)
                PlayTable = new Table(TextContainer, values, names)
                {
                    HasEndColumn = true
                };
            else
                PlayTable.SetValues(values);
        }
        public void ReloadScores()
        {
            TotalPP = -1;
            PlusOne = -1;
            InitScores();
        }
        #endregion
        #region Static Functions
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
            byte[] data = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(LoadedProfiles.Values.Select(p => JToken.FromObject(p)), Formatting.Indented));
            if (File.Exists(HelpfulPaths.PROFILE_DATA)) File.Delete(HelpfulPaths.PROFILE_DATA);
            using (FileStream fs = File.OpenWrite(HelpfulPaths.PROFILE_DATA))
                fs.Write(data, 0, data.Length);
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
            foreach (JToken profileItem in profiles)
            {
                Profile profile = profileItem.ToObject<Profile>();
                LoadedProfiles.Add(profile.ID, profile);
            }
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
            Leaderboards allowed = APIHandler.GetRankedLeaderboards(hash), current;
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
            Plugin.Log.Debug($"Recieved score: " + rawPP);
            int index = HelpfulMisc.ReverseBinarySearch(Scores, rawPP);
            Plugin.Log.Debug("AddPlay Index: " +  index);
            if (index >= Scores.Length) return false;
            float profilePP = GetProfilePP(GetWeightedPP(rawPP), WeightedScores, index + 1);
            if (profilePP > 0) TotalPP += profilePP;
            HelpfulMisc.SiftDown(Scores, index, rawPP);
            HelpfulMisc.SiftDown(ScoreNames, index, mapName);
            HelpfulMisc.SiftDown(ScoreDiffs, index, diff.ToString());
            HelpfulMisc.SiftDown(WeightedScores, index, GetWeight(index + 1) * rawPP, GetWeight(2));
            PlusOne = CalculatePlusOne();
            InitTable();
            return true;
        }
        #endregion
    }
}
