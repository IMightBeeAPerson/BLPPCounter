using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using System;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public class ClanRankCounter(TMP_Text display, MapSelection map, CancellationToken ct) : MyCounters(display, map, ct)
    {
        #region Static Variables
        public static int OrderNumber => 5;
        public static string DisplayName => "Clan Rank";
        public static Leaderboards ValidLeaderboards => Leaderboards.Beatleader;
        public static string DisplayHandler => DisplayName;
        private static PluginConfig PC => PluginConfig.Instance;
        #endregion
        #region Variables
        public override string Name => DisplayName;
        #endregion

        public override void SetupData(MapSelection map, CancellationToken ct)
        {
        }
        public override void UpdateFormat()
        {
        }

        #region Updates
        public override void UpdateCounterInternal(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
        }
        public override void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
        }
        #endregion
    }
}
