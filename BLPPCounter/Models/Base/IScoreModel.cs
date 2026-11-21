namespace BLPPCounter.Models.Base
{
    internal interface IScoreModel : IModel
    {
        public float LeaderboardPoints { get; }
        public float Accuracy { get; }
        public int ModifiedScore { get; }
        public int Rank { get; }
        public string PlayerName { get; }
    }
}
