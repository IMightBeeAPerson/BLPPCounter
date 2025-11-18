
using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal static class LocalReplayHandler
    {
        private static JObject replayHeaders;
        
        public static void LoadReplays()
        {
            if (!(replayHeaders is null)) return;
            try
            {
                replayHeaders = JObject.Parse(File.ReadAllText(HelpfulPaths.BL_REPLAY_HEADERS));
            } catch (Exception e)
            {
                Plugin.Log.Warn($"Failed to load local replays: {e.Message}");
                Plugin.Log.Debug(e);
            }
        }
        public static string GetReplayName(string userId, string hash, string mode, string diff)
        {
            if (replayHeaders is null)
                LoadReplays();
            string filename = null;
            foreach (var token in replayHeaders)
                if (token.Value["PlayerID"].ToString().Equals(userId) &&
                token.Value["SongHash"].ToString().Equals(hash) &&
                token.Value["SongMode"].ToString().Equals(mode) &&
                token.Value["SongDifficulty"].ToString().Equals(diff)
                ) filename = token.Key;
            return filename;
        }
    }
}
