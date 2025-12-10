using System;

namespace BLPPCounter.Utils.Enums
{
    [Flags]
    public enum Leaderboards //bitmask
    {
        None = 0, Beatleader = 1, Scoresaber = 2, Accsaber = 4, All = Beatleader | Scoresaber | Accsaber
    }
}
