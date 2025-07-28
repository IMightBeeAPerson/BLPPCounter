using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLPPCounter.Utils
{
    public enum Leaderboards //bitmask
    {
        Beatleader = 1, Scoresaber = 2, Accsaber = 4, All = Beatleader | Scoresaber | Accsaber
    }
}
