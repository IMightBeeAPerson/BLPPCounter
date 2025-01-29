using BLPPCounter.Settings.Configs;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static BLPPCounter.Helpfuls.HelpfulPaths;

namespace BLPPCounter.Utils
{
    internal static class APIHandler
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = new TimeSpan(0, 0, 3)
        };
        private static readonly Throttler BL_Throttle = new Throttler(100, 15);
        private static readonly Throttler SS_Throttle = new Throttler(50, 10);
        public static bool CallAPI(string path, out HttpContent content, bool quiet = false, bool forceNoHeader = false, bool forceBLCall = false)
        {
            const string LinkHeader = "https://";
            if (!forceNoHeader && !path.Substring(0, LinkHeader.Length).Equals(LinkHeader))
                path = (!forceBLCall && PluginConfig.Instance.UsingSS ? SSAPI : BLAPI) + path;
            if (path.Substring(LinkHeader.Length, 14).Equals("api.beatleader")) BL_Throttle.Call();
            if (path.Substring(LinkHeader.Length, 10).Equals("scoresaber")) SS_Throttle.Call();
            try
            {
                Plugin.Log.Debug("API Call: " + path);
                HttpResponseMessage hrm = client.GetAsync(new Uri(path)).Result;
                hrm.EnsureSuccessStatusCode();
                content = hrm.Content;
                return true;
            }
            catch (Exception e)
            {
                if (!quiet)
                {
                    Plugin.Log.Error($"Beat Leader API request failed\nPath: {path}\nError: {e.Message}");
                    Plugin.Log.Debug(e);
                }
                content = null;
                
                return false;
            }
        }
        public static HttpContent CallAPI(string path, bool quiet = false, bool forceNoHeader = false, bool forceBLCall = false)
        {
            CallAPI(path, out HttpContent content, quiet, forceNoHeader, forceBLCall);
            return content;
        }
        public static string CallAPI_String(string path, bool quiet = false, bool forceNoHeader = false, bool forceBLCall = false) =>
            CallAPI(path, quiet, forceNoHeader, forceBLCall)?.ReadAsStringAsync().Result;
        public static byte[] CallAPI_Bytes(string path, bool quiet = false, bool forceNoHeader = false, bool forceBLCall = false) =>
            CallAPI(path, quiet, forceNoHeader, forceBLCall)?.ReadAsByteArrayAsync().Result;

        private class Throttler //based off this: https://github.com/TaohRihze/SongSuggestCore/blob/master/SongSuggestCore/DataHandlers/WebDownloader.cs#L483
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
}
