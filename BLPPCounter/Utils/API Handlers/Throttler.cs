using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.API_Handlers
{
    internal class Throttler //based off this: https://github.com/TaohRihze/SongSuggestCore/blob/master/SongSuggestCore/DataHandlers/WebDownloader.cs#L483
    {
        public int CallsPerCycle { get; private set; }
        public int CycleLength { get; private set; } //In seconds

        private DateTime CycleStartTime;
        private int CallsThisCycle;
        private readonly object locker;

        public Throttler(int callsPerCycle, int cycleLength)
        {
            CallsPerCycle = callsPerCycle;
            CycleLength = cycleLength;

            CycleStartTime = DateTime.UtcNow;
            CallsThisCycle = 0;
            locker = new object();
        }

        public void Call()
        {
            lock (locker)
            {
                TimeSpan diff = DateTime.UtcNow - CycleStartTime;
                if (diff.TotalSeconds >= CycleLength)
                {
                    CallsThisCycle = 0;
                    CycleStartTime = DateTime.UtcNow;
                }
                else CallsThisCycle++;
                if (CallsThisCycle >= CallsPerCycle)
                {
                    int restTime = CycleLength * 1000 - diff.Milliseconds;
                    Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                    Thread.Sleep(restTime);
                    CallsThisCycle = 0;
                    CycleStartTime = DateTime.UtcNow;
                }
            }
        }
    }
}
