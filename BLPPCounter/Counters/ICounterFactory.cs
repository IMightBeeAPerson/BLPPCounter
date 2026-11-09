using System.Threading;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using TMPro;

namespace BLPPCounter.Counters
{
    internal interface ICounterFactory
    {
        MyCounters? Create(
            string displayName,
            TMP_Text display,
            MapSelection map,
            Leaderboards leaderboard,
            CancellationToken cancellationToken = default
        );
    }
}
