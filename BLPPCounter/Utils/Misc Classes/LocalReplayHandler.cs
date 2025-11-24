
using BeatLeader.Models.Replay;
using BLPPCounter.Helpfuls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal static class LocalReplayHandler
    {
        private static Dictionary<string, string> cachedHeaders = null; 

        public static void LoadReplays()
        {
            if (cachedHeaders is not null) return;
            Plugin.Log.Info("Loading local replays...");
            try
            {
                cachedHeaders = [];
                JObject replayHeaders = JObject.Parse(File.ReadAllText(HelpfulPaths.BL_REPLAY_HEADERS));
                Dictionary<string, int> cachedScores = [];
                foreach (KeyValuePair<string, JToken> token in replayHeaders)
                {
                    string keyString = GetKey(token.Value["PlayerID"].ToString(), token.Value["SongHash"].ToString(), token.Value["SongMode"].ToString(), token.Value["SongDifficulty"].ToString());
                    if (!cachedHeaders.TryAdd(keyString, token.Key))
                    {
                        string path = GetReplayPath(cachedHeaders[keyString]);
                        if (path is null) goto skip;
                        if (!cachedScores.ContainsKey(keyString))
                        {
                            ReplayDecoder.TryDecodeReplayInfo(File.ReadAllBytes(path), out ReplayInfo info);
                            cachedScores.Add(keyString, info.score);
                        }
                        path = GetReplayPath(token.Key);
                        if (path is null) goto skip;
                        ReplayDecoder.TryDecodeReplayInfo(File.ReadAllBytes(path), out ReplayInfo newInfo);
                        if (newInfo.score > cachedScores[keyString])
                        {
                            cachedHeaders[keyString] = token.Key;
                            cachedScores[keyString] = newInfo.score;
                        }
                    skip:;
                    }

                }
            } catch (Exception e)
            {
                Plugin.Log.Warn($"Failed to load local replays: {e.Message}");
                Plugin.Log.Debug(e);
            }
            Plugin.Log.Info($"Loaded {cachedHeaders.Count} local replays.");
        }
        public static string GetReplayName(string userId, string hash, string mode, string diff)//, string mapName = null)
        {
            //Plugin.Log.Info("Searching replay headers for matching replay...");
            if (cachedHeaders is null)
                LoadReplays();
            return cachedHeaders.TryGetValue(GetKey(userId, hash, mode, diff), out string replayName) ? replayName : null;
        }
        private static string GetKey(string userId, string hash, string mode, string diff) =>
            $"{userId}-{hash}-{mode}-{diff}";
        public static string GetReplayPath(string replayName)
        {
            string path = Path.Combine(HelpfulPaths.BL_REPLAY_FOLDER, replayName);
            if (File.Exists(path)) return path;
            path = Path.Combine(HelpfulPaths.BL_REPLAY_CACHE_FOLDER, replayName);
            return File.Exists(path) ? path : null;
        }
    }
}
