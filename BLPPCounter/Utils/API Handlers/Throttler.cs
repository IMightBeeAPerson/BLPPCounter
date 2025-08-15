using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public async Task Call()
        {
            int restTime = 0;
            lock (locker)
            {
                TimeSpan diff = DateTime.UtcNow - CycleStartTime;

                if (diff.TotalSeconds >= CycleLength)
                {
                    CallsThisCycle = 0;
                    CycleStartTime = DateTime.UtcNow;
                }

                CallsThisCycle++;

                if (CallsThisCycle > CallsPerCycle)
                {
                    restTime = (int)(CycleLength * 1000 - diff.TotalMilliseconds);
                    Thread.Sleep(restTime);
                    CallsThisCycle = 1;
                    CycleStartTime = DateTime.UtcNow.AddMilliseconds(restTime);
                }
            }
            if (restTime > 0)
            {
                Plugin.Log.Info("Throttling calls for " + restTime + "ms.");
                await Task.Delay(restTime);
            }
        }
    }
}
