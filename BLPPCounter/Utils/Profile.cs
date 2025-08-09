using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using IPA.Config.Stores.Attributes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;

namespace BLPPCounter.Utils
{
    internal class Profile
    {
#pragma warning disable IDE0051
        #region Static Variables
        private static readonly float[] AccsaberConsts;
        private static readonly Dictionary<string, Profile> LoadedProfiles = new Dictionary<string, Profile>();
        #endregion
        #region Variables
        [JsonIgnore]
        private string ID => UserID + "_" + (int)Leaderboard;
        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)]
        private readonly Leaderboards Leaderboard;
        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)]
        private float[] Scores;
        [JsonIgnore]
        private float[] WeightedScores;
        [JsonProperty(nameof(TotalPP), Required = Required.DisallowNull)]
        private float TotalPP = -1.0f;
        [JsonProperty(nameof(UserID), Required = Required.DisallowNull)]
        private readonly string UserID;
        [JsonProperty(nameof(PlusOne), Required = Required.DisallowNull)]
        public float PlusOne { get; private set; } = -1.0f;
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
                Scores = api.GetScores(UserID, GetPlusOneCount());
                TotalPP = api.GetProfilePP(UserID);
                if (Scores is null) Scores = new float[1] { 0.0f };
            }
            WeightScores();
            if (PlusOne < 0)
                PlusOne = CalculatePlusOne();
        }
        public void ReloadScores()
        {
            TotalPP = -1;
            PlusOne = -1;
            InitScores();
            Plugin.Log.Info($"Scores Length: {Scores.Length}\nNew Scores: {HelpfulMisc.Print(Scores)}");
            Plugin.Log.Info($"Weighted Scores Length: {WeightedScores.Length}\nNew Weighted Scores: {HelpfulMisc.Print(WeightedScores)}");
        }
        #endregion
        #region Static Functions
        public static void SaveProfile(Profile profile)
        {
            if (LoadedProfiles.ContainsKey(profile.ID))
                LoadedProfiles[profile.ID] = profile;
            else
                LoadedProfiles.Add(profile.ID, profile);
        }
        internal static void SaveAllProfiles()
        {
            if (LoadedProfiles.Count == 0) return;
            byte[] data = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(LoadedProfiles.Values.Select(p => JToken.FromObject(p)), Formatting.Indented));
            if (File.Exists(HelpfulPaths.PROFILE_DATA)) File.Delete(HelpfulPaths.PROFILE_DATA);
            using (FileStream fs = File.OpenWrite(HelpfulPaths.PROFILE_DATA))
                fs.Write(data, 0, data.Length);
            //Plugin.Log.Info("Profiles have been saved.");
        }
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
        public static Profile TryGetProfile(Leaderboards leaderboard, string userID) => 
            LoadedProfiles.ContainsKey(GetID(leaderboard, userID)) ? LoadedProfiles[GetID(leaderboard, userID)] : null;
        public static string GetID(Leaderboards leaderboard, string userID) => userID + "_" + (int)leaderboard;
        #endregion
        #region Misc Functions
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
        private int GetScoreIndex(float rawPP)
        {
            int i = 0;
            while (i < Scores.Length && Scores[i] > rawPP) i++;
            return i + 1;
        }
        public int GetScoreIndexWeighted(float weightedPP)
        {
            int i = 0;
            while (i < WeightedScores.Length && WeightedScores[i] > weightedPP) i++;
            return i + 1;
        }
        public float GetWeightedPP(float rawPP) => (float)Math.Round(GetWeight(GetScoreIndex(rawPP)) * rawPP, PluginConfig.Instance.DecimalPrecision);
        public float GetProfilePP(float weightedPP) => GetProfilePP(weightedPP, WeightedScores, GetScoreIndexWeighted(weightedPP));
        public float GetProfilePPRaw(float rawPP) => GetProfilePP(GetWeightedPP(rawPP), WeightedScores, GetScoreIndex(rawPP));
        private float GetProfilePP(float pp, float[] scores, int index)
        {
            float weightedSum = TotalPP;
            if (index-- >= scores.Length)
                return -1;
            else for (int j = 0; j < index; weightedSum -= scores[j], j++) ;
            float outp = (float)Math.Round(pp - (1.0f - GetWeight(2)) * weightedSum, PluginConfig.Instance.DecimalPrecision);
            return outp < 0 ? 0 : outp;
        }
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
        public void AddPlay(float rawPP)
        {
            if (float.IsNaN(rawPP) || Scores[Scores.Length - 1] > rawPP) return;
            int index = Scores.Length - HelpfulMisc.FindInsertValue(Scores.Reverse().ToArray(), rawPP);
            float profilePP = GetProfilePP(GetWeightedPP(rawPP), WeightedScores, index + 1);
            if (profilePP > 0) TotalPP += profilePP;
            HelpfulMisc.SiftDown(Scores, index, rawPP);
            HelpfulMisc.SiftDown(WeightedScores, index, GetWeight(index + 1) * rawPP, GetWeight(2));
            PlusOne = CalculatePlusOne();
        }
        #endregion
    }
}
