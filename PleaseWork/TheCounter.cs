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
using System.Linq;
using System.Reflection;
namespace PleaseWork
{

    public class TheCounter : BasicCustomCounter
    {
        /*[Inject] private BeatmapLevel beatmap;
        [Inject] private BeatmapKey beatmapDiff;// 1.37.0 and above */
        [Inject] private IDifficultyBeatmap beatmap; // 1.34.2 and below
        [Inject] private GameplayModifiers mods;
        [Inject] private ScoreController sc;
        [Inject] private BeatmapObjectManager bomb;
        private static readonly HttpClient client = new HttpClient();
        public static Dictionary<string, Map> Data { get; private set; }
        private static bool dataLoaded = false;
        private static MapSelection lastMap;
        public static IMyCounters theCounter { get; private set; }
        public static Dictionary<string, Type> StaticFunctions { get; private set; }
        public static Dictionary<string, Type> StaticProperties { get; private set; }
        public static Type[] ValidCounters { get; private set; }
        public static Dictionary<string, string> DisplayNameToCounter { get; private set; }
        public static string[] ValidDisplayNames { get; private set; }
        private static Func<bool, bool, float, float, int, string, string> displayFormatter;
        public static Func<string, string, string> TargetFormatter;
        public static Func<Func<string>, float, float, float, float, float, string> PercentNeededFormatter;
        private static Func<Func<Dictionary<char, object>, string>> displayIniter, targetIniter, percentNeededIniter;

        private static bool updateFormat;
        private TMP_Text display;        
        private bool enabled;
        private float passRating, accRating, techRating, stars;
        private int notes, comboNotes, mistakes;
        private int fcTotalHitscore, fcMaxHitscore;
        private double totalHitscore, maxHitscore;
        private string mode, lastTarget;
        private (int, float) currentMult;

        public static bool FormatUsable { get => displayFormatter != null && displayIniter != null; }
        public static bool TargetUsable { get => TargetFormatter != null && targetIniter != null; }
        public static bool PercentNeededUsable { get => PercentNeededFormatter != null && percentNeededIniter != null; }

        #region Overrides & Event Calls

        public static void InitCounterStatic() 
        {
            updateFormat = false;
            PleaseWork.Settings.SettingsHandler.SettingsUpdated += () => updateFormat = true;

            StaticFunctions = new Dictionary<string, Type>() 
            { { "InitFormat", typeof(bool) } };
            StaticProperties = new Dictionary<string, Type>()
            { {"DisplayNames", typeof(string[]) }, {"OrderNumber", typeof(int) } };

            if (FormatTheFormat(PluginConfig.Instance.FormatSettings.DefaultTextFormat)) InitFormat();
            if (FormatTarget(PluginConfig.Instance.MessageSettings.TargetingMessage)) InitTarget();
            if (FormatPercentNeeded(PluginConfig.Instance.MessageSettings.PercentNeededMessage)) InitPercentNeeded();
            try
            {
                var validTypes = GetValidCounters();
                Dictionary<string, object> methodOutp = GetMethodFromTypes("InitFormat", validTypes);
                for (int i = validTypes.Length - 1; i >= 0; i--)
                {
                    if (methodOutp[validTypes[i].Name] is bool v)
                        if (!v) validTypes[i] = null;
                }
                ValidCounters = validTypes.Where(a => a != null).ToArray();
                Dictionary<string, object> propertyOutp = GetPropertyFromTypes("DisplayNames", ValidCounters);
                DisplayNameToCounter = new Dictionary<string, string>();
                foreach (var val in propertyOutp)
                    if (val.Value is string[] arr)
                        foreach (var otherVal in arr)
                            DisplayNameToCounter.Add(otherVal, val.Key);
                Dictionary<int, string> propertyOrder = GetPropertyFromTypes("OrderNumber", ValidCounters).ToDictionary(x => (int)x.Value, x => x.Key);
                List<string> displayNames = new List<string>();
                for (int i = 0; i < propertyOrder.Count; i++)
                    foreach (string name in propertyOutp[propertyOrder[i]] as string[])
                        displayNames.Add(name);
                ValidDisplayNames = displayNames.ToArray();
            } catch (Exception e)
            {
                Plugin.Log.Error("Oh no! The static check for counters broke somehow :(");
                Plugin.Log.Error(e.ToString());
            }
            
        }
        public override void CounterDestroy() {
            if (enabled)
            {
                sc.scoringForNoteFinishedEvent -= OnNoteScored;
                sc.multiplierDidChangeEvent -= MultiplierChanged;
                bomb.noteWasCutEvent -= OnBombHit;
            }
        }
        public override void CounterInit()
        {
            notes = fcMaxHitscore = comboNotes = fcTotalHitscore = mistakes = 0;
            totalHitscore = maxHitscore = 0.0;
            enabled = false;
            if (!dataLoaded)
            {
                Data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0, 0, 3);
                lastTarget = "None";
                InitData();
            }
            bool loadedEvents = false;
            try
            {
                if (!dataLoaded) RequestHashData();
                enabled = SetupMapData();
                if (!enabled && dataLoaded)
                {
                    Plugin.Log.Warn("Data load from cache failed. Attempting to load from API.");
                    RequestHashData();
                    enabled = SetupMapData();
                }
                if (enabled)
                {
                    display = CanvasUtility.CreateTextFromSettings(Settings);
                    display.fontSize = (float)PluginConfig.Instance.FontSize;
                    display.text = "";
                    sc.scoringForNoteFinishedEvent += OnNoteScored;
                    sc.multiplierDidChangeEvent += MultiplierChanged;
                    bomb.noteWasCutEvent += OnBombHit;
                    currentMult = (1, 0);
                    loadedEvents = true;
                    string hash = beatmap.level.levelID.Split('_')[2]; // 1.34.2 and below
                    //string hash = beatmap.levelID.Split('_')[2]; // 1.37.0 and above
                    bool counterChange = theCounter != null && !theCounter.Name.Equals(PluginConfig.Instance.PPType.Split(' ')[0]);
                    if (counterChange || lastMap.Equals(new MapSelection()) || hash != lastMap.Hash || PluginConfig.Instance.PPType.Equals("Progressive") || lastTarget != PluginConfig.Instance.Target)
                    {

                        Data.TryGetValue(hash, out Map m);
                        if (m == null) 
                        {
                            Plugin.Log.Warn("Map not in cache, attempting API call to get map data...");
                            RequestHashData();
                            m = Data[hash];
                        }
                        lastMap = new MapSelection(m, beatmap.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating); // 1.34.2 and below
                        //lastMap = new MapSelection(Data[hash], beatmapDiff.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating); // 1.37.0 and above
                        if (!InitCounter()) throw new Exception("Counter somehow failed to init. Weedoo weedoo weedoo weedoo.");
                    }
                    else
                        APIAvoidanceMode();
                    lastTarget = PluginConfig.Instance.Target;
                    if (updateFormat) { theCounter.UpdateFormat(); updateFormat = false; }
                    theCounter.UpdateCounter(1, 0, 0, 0);
                } else
                    Plugin.Log.Warn("Maps failed to load, most likely unranked.");
            } catch (Exception e)
            {
                Plugin.Log.Error($"The counter failed to be initialized: {e.Message}\nSource: {e.Source}");
                
                if (e is KeyNotFoundException) Plugin.Log.Error($"Data dictionary length: {Data.Count}");
                Plugin.Log.Debug(e);
                enabled = false;
                if (display != null)
                    display.text = "";
                if (loadedEvents)
                {
                    sc.scoringForNoteFinishedEvent -= OnNoteScored;
                    sc.multiplierDidChangeEvent -= MultiplierChanged;
                    bomb.noteWasCutEvent -= OnBombHit;
                }
            }
        }

        private void OnNoteScored(ScoringElement scoringElement)
        {
            if (scoringElement.noteData.gameplayType == NoteData.GameplayType.Bomb)
            {
                return;
            }
            NoteData.ScoringType st = scoringElement.noteData.scoringType;
            /*bool isSliderTail = st == NoteData.ScoringType.SliderTail || st == NoteData.ScoringType.BurstSliderElement;
            if (isSliderTail)
            {
                if (scoringElement.cutScore == 0) comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
                goto Finish;
            }//*/
            if (st == NoteData.ScoringType.Ignore) goto Finish; //if scoring type is Ignore, skip this function
            notes++;
            if (st != NoteData.ScoringType.NoScore) comboNotes++;
            maxHitscore += notes < 14 ? scoringElement.maxPossibleCutScore * (HelpfulMath.MultiplierForNote(notes) / 8.0) : scoringElement.maxPossibleCutScore;
            if (scoringElement.cutScore > 0)
            {
                totalHitscore += scoringElement.cutScore * (HelpfulMath.MultiplierForNote(comboNotes) / 8.0);
                fcTotalHitscore += scoringElement.cutScore;
                fcMaxHitscore += scoringElement.maxPossibleCutScore;
            }
            else if (currentMult.Item1 == 1 && currentMult.Item2 == 0) MultiplierChanged(1, 0);
            Finish:
            theCounter.UpdateCounter((float)(totalHitscore / maxHitscore), notes, mistakes, fcTotalHitscore / (float)fcMaxHitscore);
        }
        private void OnBombHit(NoteController nc, in NoteCutInfo nci)
        {
            if(nc.noteData.gameplayType == NoteData.GameplayType.Bomb && currentMult.Item1 == 1 && currentMult.Item2 == 0)
            {
                MultiplierChanged(1, 0);
            }
        }
        private void MultiplierChanged(int newMult, float percentFilled)
        {
            if (newMult < currentMult.Item1 || (newMult == currentMult.Item1 && percentFilled < currentMult.Item2))
            {
                comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
                mistakes++;
            } else if (newMult == 1 && percentFilled == 0.0f)
            {
                mistakes++;
            }
            currentMult = (newMult, percentFilled);
        }
        #endregion
        #region API Calls
        
        
        private string RequestHashData()
        {
            //string path = HelpfulPaths.BLAPI_HASH + beatmap.levelID.Split('_')[2].ToUpper(); // 1.37.0 and above
            string path = HelpfulPaths.BLAPI_HASH + beatmap.level.levelID.Split('_')[2].ToUpper(); // 1.34.2 and below
            Plugin.Log.Debug(path);
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
        private static bool FormatTheFormat(string format) {
            displayIniter = HelpfulFormatter.GetBasicTokenParser(format, a => { }, 
                (tokens, tokensCopy, priority, vals) => 
                { 
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1'); 
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2'); 
                });
            return displayIniter != null;
        }
        private static bool FormatTarget(string format)
        {
            targetIniter = HelpfulFormatter.GetBasicTokenParser(format, a => { }, (a, b, c, d) => { });
            return targetIniter != null;
        }
        private static bool FormatPercentNeeded(string format)
        {
            percentNeededIniter = HelpfulFormatter.GetBasicTokenParser(format, a => { },
                (tokens, tokensCopy, priority, vals) =>
                {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<string>)vals['c']).Invoke()}", "</color>");
                });
            return percentNeededIniter != null;
        }
        private static void InitFormat()
        {
            var simple = displayIniter.Invoke();
            displayFormatter = (fc, totPp, pp, fcpp, mistakes, label) => simple.Invoke(new Dictionary<char, object>()
            { { (char)1, fc }, {(char)2, totPp }, {'x', pp }, {'l', label }, { 'y', fcpp }, {'e', mistakes } });
        }
        private static void InitTarget()
        {
            var simple = targetIniter.Invoke();
            TargetFormatter = (name, mods) => simple.Invoke(new Dictionary<char, object>() { { 't', name }, { 'm', mods } });
        }
        private static void InitPercentNeeded()
        {
            var simple = percentNeededIniter.Invoke();
            PercentNeededFormatter = (color, acc, passpp, accpp, techpp, pp) => simple.Invoke(new Dictionary<char, object>()
            { { 'c', color }, { 'a', acc }, { 'x', techpp }, { 'y', accpp }, { 'z', passpp }, { 'p', pp } });
        }
        private static Type[] GetValidCounters()
        {
            List<Type> counters = new List<Type>();
            //The line below taken from: https://stackoverflow.com/questions/26733/getting-all-types-that-implement-an-interface
            var types = System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IMyCounters)));
            foreach (Type t in types)
            {
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
                var Funcs = new Dictionary<string, Type>(StaticFunctions);
                foreach (MethodInfo m in methods)
                    if (Funcs.ContainsKey(m.Name)) 
                        if (m.ReturnParameter.ParameterType.Equals(Funcs[m.Name]))
                        {
                            Funcs.Remove(m.Name);
                            if (Funcs.Count == 0) break;
                        } else break;
                if (Funcs.Count != 0) continue;
                var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Static);
                var Vars = new Dictionary<string, Type>(StaticProperties);
                foreach (PropertyInfo p in properties)
                    if (Vars.ContainsKey(p.Name))
                        if (p.PropertyType == Vars[p.Name])
                        {
                            if (!p.CanRead) continue;
                            Vars.Remove(p.Name);
                            if (Vars.Count == 0) break;
                        }
                        else break;
                if (Vars.Count == 0)
                    counters.Add(t);
            }
            return counters.ToArray();
        }
        private static Dictionary<string, object> GetMethodFromTypes(string methodName, params Type[] types)
        {
            Dictionary<string, object> outp = new Dictionary<string, object>();
            foreach (Type t in types)
            {
                var method = t.GetMethods(BindingFlags.Public | BindingFlags.Static).SkipWhile(a => !a.Name.Equals(methodName)).First();
                outp.Add(t.Name, method.Invoke(null, null));
            }
            return outp;
        }
        private static Dictionary<string, object> GetPropertyFromTypes(string propertyName, params Type[] types)
        {
            Dictionary<string, object> outp = new Dictionary<string, object>();
            foreach (Type t in types)
            {
                var method = t.GetProperties(BindingFlags.Public | BindingFlags.Static).SkipWhile(a => !a.Name.Equals(propertyName)).First();
                outp.Add(t.Name, method.GetValue(null));
            }
            return outp;
        }
        #endregion
        #region Init
        private bool InitCounter()
        {
            if (!DisplayNameToCounter.TryGetValue(PluginConfig.Instance.PPType, out string name)) return false;
            Type counterType = ValidCounters.First(a => a.Name.Equals(name));
            theCounter = (IMyCounters)Activator.CreateInstance(counterType, display, lastMap);
            return true;
        }
        private void APIAvoidanceMode()
        {
            Plugin.Log.Info("API Avoidance mode is functioning (probably)!");
            MapSelection thisMap = new MapSelection(Data[lastMap.Hash], beatmap.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating); // 1.34.2 and below
            //MapSelection thisMap = new MapSelection(Data[lastMap.Hash], beatmapDiff.difficulty.Name().Replace("+", "Plus"), mode, passRating, accRating, techRating); // 1.37.0 and above
            Plugin.Log.Debug($"Last Map\n-------------------\n{lastMap}\n-------------------\nThis Map\n-------------------\n{thisMap}\n-------------------");
            bool ratingDiff, diffDiff;
            (ratingDiff, diffDiff) = thisMap.GetDifference(lastMap);
            Plugin.Log.Info($"Rating: {ratingDiff}\tDifficulty: {diffDiff}");
            if (diffDiff) theCounter.ReinitCounter(display, thisMap);
            else if (ratingDiff) theCounter.ReinitCounter(display, passRating, accRating, techRating);
            else theCounter.ReinitCounter(display);
            lastMap = thisMap;
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
                string hash = ((string)dataToken["song"]["hash"]).ToUpper();
                string songId = (string)dataToken["song"]["id"];
                foreach (JToken mapToken in mapTokens)
                {
                    Map map = new Map(hash, songId, mapToken);
                    if (Data.ContainsKey(hash))
                        Data[hash].Combine(map);
                    else Data[hash] = map;
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
            //string hash = beatmap.levelID.Split('_')[2].ToUpper(); // 1.37.0 and above
            string hash = beatmap.level.levelID.Split('_')[2].ToUpper(); // 1.34.2 and below
            try
            {
                if (!Data.TryGetValue(hash, out Map theMap)) throw new KeyNotFoundException("The map is not in the loaded cache.");
                var hold = theMap.Get(beatmap.difficulty.Name().Replace("+", "Plus"));
                mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;// 1.34.2 and below */
                /*Dictionary<string, (string, JToken)> hold = Data[hash].Get(beatmapDiff.difficulty.Name().Replace("+", "Plus")); 
                mode = beatmapDiff.beatmapCharacteristic.serializedName;// 1.37.0 and above */
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
            techRating = HelpfulPaths.GetRating(data, PPType.Tech, mods.songSpeedMul);
            stars = HelpfulPaths.GetRating(data, PPType.Star, mods.songSpeedMul);
            string mod = HelpfulMisc.GetModifierShortname(HelpfulMisc.SpeedToModifier(mods.songSpeedMul)).ToUpper();
            Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {stars}\n{mod} Pass Rating: {passRating}\n{mod} Acc Rating: {accRating}\n{mod} Tech Rating: {techRating}" : $"Stars: {stars}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            return stars > 0;
        }
        #endregion
        #region Updates
        
        public static void UpdateText(bool displayFc, TMP_Text display, float[] ppVals, int mistakes)
        {
            string[] labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };
            if (PluginConfig.Instance.SplitPPVals) {
                string outp = "";
                for (int i=0;i<4;i++)
                    outp += displayFormatter.Invoke(displayFc, PluginConfig.Instance.ExtraInfo && i == 3, ppVals[i], ppVals[i + 4], mistakes, labels[i]) + "\n";
                display.text = outp;
            } else
                display.text = displayFormatter.Invoke(displayFc, PluginConfig.Instance.ExtraInfo, ppVals[3], ppVals[7], mistakes, labels[3]);
        }
        #endregion
    }
}