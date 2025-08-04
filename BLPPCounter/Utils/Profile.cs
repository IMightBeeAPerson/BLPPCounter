using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.API_Handlers;
using IPA.Config.Stores.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        #endregion
        #region Variables
        [JsonProperty(nameof(Leaderboard), Required = Required.DisallowNull)]
        private readonly Leaderboards Leaderboard;
        [JsonProperty(nameof(Scores), Required = Required.DisallowNull)]
        [JsonConverter(typeof(float[]))]
        private float[] Scores;
        [JsonIgnore]
        private float[] WeightedScores;
        [JsonProperty(nameof(UserID), Required = Required.DisallowNull)]
        private readonly string UserID;
        [JsonIgnore]
        public float PlusOne { get; private set; }
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
            Scores = APIHandler.GetAPI(Leaderboard).GetScores(UserID, GetPlusOneCount());
            if (Scores is null) Scores = new float[1] { 0.0f };
            WeightScores();
            CalculatePlusOne();
        }
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
            Plugin.Log.Info($"Scores: {HelpfulMisc.Print(Scores)}\nWeighted Scores: {HelpfulMisc.Print(WeightedScores)}");
        }
        private int GetPlusOneCount()
        {
            switch (Leaderboard)
            {
                case Leaderboards.Beatleader:
                case Leaderboards.Scoresaber:
                    return 400;
                case Leaderboards.Accsaber:
                    return 50;
                default:
                    return 0;
            }
        }
        private void CalculatePlusOne()
        {
            float sum = 0;
            int i = Scores.Length - 1;
            float currentWeight = GetWeight(i + 2), previousWeight = 1;
            for (; i > 0; i--)
            {
                previousWeight = GetPreviousWeight(currentWeight);
                sum += (Scores[i] * previousWeight) - (Scores[i] * currentWeight);
                currentWeight = previousWeight;
                if (Scores[i - 1] * previousWeight - sum >= 1.0f)
                    break;
            }
            PlusOne = (1 + sum) / previousWeight;
            PlusOne = (float)Math.Round(PlusOne, PluginConfig.Instance.DecimalPrecision);
        }
        #endregion
    }
}
