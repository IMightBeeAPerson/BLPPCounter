using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public class NormalCounter: MyCounters
    {
        public static string DisplayName => "Normal";
        public static string DisplayHandler => TheCounter.DisplayName;
        public static int OrderNumber => 0;
        public static Leaderboards ValidLeaderboards => Leaderboards.All;
        public override string Name => DisplayName;
        private float[] ppVals;

        #region Init
        public NormalCounter(TMP_Text display, MapSelection map, CancellationToken ct) : base(display, map, ct)
        {
            ppVals = new float[calc.DisplayRatingCount * 2];
        }
        public override void SetupData(MapSelection map, CancellationToken ct) { }
        public override void UpdateFormat() { }
        public static bool InitFormat() => TheCounter.FormatUsable;
        public static void ResetFormat() { }
        #endregion
        #region Updates
        public override void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            bool displayFC = PluginConfig.Instance.PPFC && mistakes > 0;
            calc.SetPp(acc, ppVals, 0, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
            if (displayFC) calc.SetPp(fcPercent, ppVals, ppVals.Length / 2, PluginConfig.Instance.DecimalPrecision, ratings.SelectedRatings);
            TheCounter.UpdateText(displayFC, display, ppVals, mistakes);
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote) { }
        #endregion
    }
}
