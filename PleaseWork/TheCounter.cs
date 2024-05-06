using System;
using System.IO;
using CountersPlus.Counters.Custom;
using TMPro;
using Zenject;
using PleaseWork.Settings;
using PleaseWork.Utils;
using PleaseWork.Helpfuls;
using PleaseWork.Counters;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter
    {
        [Inject] private IDifficultyBeatmap beatmap;
        [Inject] private GameplayModifiers mods;
        [Inject] private ScoreController sc;
        private static readonly HttpClient client = new HttpClient();
        public static Dictionary<string, Map> Data { get; private set; }
        private static bool dataLoaded = false;
        private static MapSelection lastMap;
        private static IMyCounters theCounter;
        private static Func<bool, float, float, string, string> displayFormatter;

        private TMP_Text display;        
        private bool enabled;
        private float passRating, accRating, techRating, stars;
        private int notes, badNotes, comboNotes;
        private int fcTotalHitscore, fcMaxHitscore;
        private double totalHitscore, maxHitscore;
        private string mode;

        #region Overrides & Event Calls

        static TheCounter() { FormatTheFormat(PluginConfig.Instance.DefaultTextFormat); }
        public override void CounterDestroy() {
            if (enabled) sc.scoringForNoteFinishedEvent -= OnNoteScored;
        }
        public override void CounterInit()
        {
            notes = badNotes = fcMaxHitscore = comboNotes = fcTotalHitscore = 0;
            totalHitscore = maxHitscore = 0.0;
            enabled = false;
            if (!dataLoaded)
            {
                Data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0, 0, 3);
                InitData();
            }
            bool loadedEvents = false;
            try
            {
                enabled = dataLoaded ? SetupMapData() : SetupMapData(JToken.Parse(RequestHashData()));
                if (enabled)
                {
                    display = CanvasUtility.CreateTextFromSettings(Settings);
                    display.fontSize = (float)PluginConfig.Instance.FontSize;
                    display.text = "";
                    sc.scoringForNoteFinishedEvent += OnNoteScored;
                    loadedEvents = true;
                    string hash = beatmap.level.levelID.Split('_')[2];
                    bool counterChange = theCounter != null && !theCounter.Name.Equals(PluginConfig.Instance.PPType.Split(' ')[0]);
                    if (counterChange || lastMap.Equals(new MapSelection()) || hash != lastMap.Hash || PluginConfig.Instance.PPType.Equals("Progressive"))
                    {
                        lastMap = new MapSelection(Data[hash], beatmap.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating);
                        if (!InitCounter()) throw new Exception("Counter somehow failed to init. Weedoo weedoo weedoo weedoo.");
                    }
                    else
                        APIAvoidanceMode();
                    theCounter.UpdateCounter(1, 0, 0, 0);
                } else
                    Plugin.Log.Warn("Maps failed to load, most likely unranked.");
            } catch (Exception e)
            {
                Plugin.Log.Warn($"Map data failed to be parsed: {e.Message}");
                Plugin.Log.Debug(e);
                enabled = false;
                if (display != null)
                    display.text = "";
                if (!loadedEvents)
                {
                    sc.scoringForNoteFinishedEvent -= OnNoteScored;
                }
            }
        }

        private void OnNoteScored(ScoringElement scoringElement)
        {
            NoteData.ScoringType st = scoringElement.noteData.scoringType;
            /*bool isSliderTail = st == NoteData.ScoringType.SliderTail || st == NoteData.ScoringType.BurstSliderElement;
            if (isSliderTail)
            {
                if (scoringElement.cutScore == 0) comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
                goto Finish;
            }//*/
            if (st <= 0) goto Finish;
            notes++; comboNotes++;
            maxHitscore += notes < 14 ? scoringElement.maxPossibleCutScore * (HelpfulMath.MultiplierForNote(notes) / 8.0) : scoringElement.maxPossibleCutScore;
            if (scoringElement.cutScore > 0)
            {
                totalHitscore += scoringElement.cutScore * (HelpfulMath.MultiplierForNote(comboNotes) / 8.0);
                fcTotalHitscore += scoringElement.cutScore;
                fcMaxHitscore += scoringElement.maxPossibleCutScore;
            }
            else
            {
                comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
                badNotes++;
            }
            Finish:
            theCounter.UpdateCounter((float)(totalHitscore / maxHitscore), notes, badNotes, fcTotalHitscore / (float)fcMaxHitscore);
        }
        #endregion
        #region API Calls
        
        
        private string RequestHashData()
        {
            string path = HelpfulPaths.BLAPI_HASH + beatmap.level.levelID.Split('_')[2].ToUpper();
            Plugin.Log.Info(path);
            try
            {
                string data = client.GetStringAsync(new Uri(path)).Result;
                AddMap(data);
                return data;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request failed!\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                return "";
            }
        }

        #endregion
        #region Helper Methods
        public static void ClearCounter() => lastMap = default;
        public static void ForceLoadMaps()
        {
            if (dataLoaded) return;
            client.Timeout = new TimeSpan(0, 0, 3);
            Data = new Dictionary<string, Map>();
            InitData();
        }
        public static void FormatTheFormat(string format) {
            var simple = HelpfulFormatter.GetBasicTokenParser(format, tokens => {}, (tokens, tokensCopy, vals) => { if (!(bool)vals['q'] && tokens.ContainsKey('1')) tokensCopy['1'] = ("", tokens['1'].Item2); });
            displayFormatter = (fc, pp, fcpp, label) => simple.Invoke(new Dictionary<char, object>() { { 'q', fc }, {'x', pp }, {'l', label }, { 'y', fcpp } });
        }
        #endregion
        #region Init
        private bool InitCounter()
        {
            switch (PluginConfig.Instance.PPType)
            {
                case "Relative":
                case "Relative w/ normal":
                    theCounter = new RelativeCounter(display, lastMap); break;
                case "Progressive":
                    theCounter = new ProgressCounter(display, lastMap); break;
                case "Normal":
                    theCounter = new NormalCounter(display, lastMap); break;
                case "Clan PP":
                case "Clan w/ normal":
                    theCounter = new ClanCounter(display, lastMap); break;
                default: return false;
            }
            return true;
        }
        private void APIAvoidanceMode()
        {
            Plugin.Log.Info("API Avoidance mode is functioning (probably)!");
            MapSelection thisMap = new MapSelection(Data[lastMap.Hash], beatmap.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating);
            bool ratingDiff, diffDiff;
            (ratingDiff, diffDiff) = thisMap.GetDifference(lastMap);
            Plugin.Log.Info($"Rating: {ratingDiff}\tDifficulty: {diffDiff}");
            if (diffDiff) theCounter.ReinitCounter(display, thisMap);
            else if (ratingDiff) theCounter.ReinitCounter(display, passRating, accRating, techRating);
            if (!ratingDiff && !diffDiff) theCounter.ReinitCounter(display);
        }
        private static void InitData()
        {
            dataLoaded = false;
            if (File.Exists(HelpfulPaths.BL_CACHE_FILE))
            {
                try
                {
                    JEnumerable<JToken> results = JObject.Parse(File.ReadAllText(HelpfulPaths.BL_CACHE_FILE))["Entries"].Children();
                    foreach (JToken result in results)
                    {
                        Map map = new Map(result["SongInfo"]["hash"].ToString().ToUpper(), (string)result["LeaderboardId"], result["DifficultyInfo"]);
                        if (Data.ContainsKey(map.Hash))
                            Data[map.Hash].Combine(map);
                        else Data[map.Hash] = map;
                    }
                    dataLoaded = true;
                    
                }
                catch (Exception e)
                {
                    Plugin.Log.Warn("Error loading bl Cache file: " + e.Message);
                    Plugin.Log.Debug(e);
                }
            }

        }
        private static void AddMap(string data)
        {
            try
            {
                JToken dataToken = JObject.Parse(data);
                JEnumerable<JToken> mapTokens = dataToken["song"]["difficulties"].Children();
                string hash = (string)dataToken["song"]["hash"];
                string songId = (string)dataToken["leaderboardId"];
                foreach (JToken mapToken in mapTokens)
                {
                    Map map = new Map(hash, songId, mapToken);
                    if (Data.ContainsKey(hash))
                        Data[map.Hash].Combine(map);
                    else Data[map.Hash] = map;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error adding map to cache: " + e.Message);
                Plugin.Log.Debug(e);
            }
        }
        private bool SetupMapData()
        {
            JToken data;
            string songId;
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper();
            try
            {
                Dictionary<string, (string, JToken)> hold = Data[hash].Get(beatmap.difficulty.Name().Replace("+", "Plus"));
                mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                if (mode == default) mode = "Standard";
                data = hold[mode].Item2;
                songId = hold[mode].Item1;
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Data length: {Data.Count}");
                Plugin.Log.Warn("Level doesn't exist for some reason :(\nHash: " + hash);
                Plugin.Log.Debug(e);
                return false;
            }
            Plugin.Log.Info("Map Hash: " + hash);
            return SetupMapData(data);
        }
        private bool SetupMapData(JToken data)
        {
            if (data == null || data.ToString().Length <= 0) return false;
            passRating = HelpfulPaths.GetRating(data, PPType.Pass, mods.songSpeedMul);
            accRating = HelpfulPaths.GetRating(data, PPType.Acc, mods.songSpeedMul);
            techRating =HelpfulPaths.GetRating(data, PPType.Tech, mods.songSpeedMul);
            stars = HelpfulPaths.GetRating(data, PPType.Star, mods.songSpeedMul);
            string mod = HelpfulMisc.GetModifierShortname(HelpfulMisc.SpeedToModifier(mods.songSpeedMul)).ToUpper();
            Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {stars}\n{mod} Pass Rating: {passRating}\n{mod} Acc Rating: {accRating}\n{mod} Tech Rating: {techRating}" : $"Stars: {stars}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            return stars > 0;
        }
        #endregion
        #region Updates
        
        public static void UpdateText(bool displayFc, TMP_Text display, float[] ppVals)
        {
            //if (ppVals.Length != 8) ppVals = new float[8];
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };
            if (PluginConfig.Instance.SplitPPVals) {
                string outp = "";
                for (int i=0;i<4;i++)
                    outp += displayFormatter.Invoke(displayFc, ppVals[i], ppVals[i+4], labels[i]);
                display.text = outp;
            } else
                display.text = displayFormatter.Invoke(displayFc, ppVals[3], ppVals[7], labels[3]);
            string target = PluginConfig.Instance.Target;
            if (!target.Equals("None"))
                display.text += $"\nTargeting <color=\"red\">{target}</color>";
        }
        #endregion
    }
}