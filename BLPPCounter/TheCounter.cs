using System;
using System.IO;
using CountersPlus.Counters.Custom;
using TMPro;
using Zenject;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils;
using BLPPCounter.Helpfuls;
using BLPPCounter.Counters;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;
using ModestTree;
using static GameplayModifiers;
using BLPPCounter.Utils.List_Settings;
namespace BLPPCounter
{

    public class TheCounter : BasicCustomCounter
    {
        #region Injects
#pragma warning disable CS0649, IDE0044
        [Inject] private BeatmapLevel beatmap;
        [Inject] private BeatmapKey beatmapDiff;// 1.37.0 and above */
        //[Inject] private IDifficultyBeatmap beatmap; // 1.34.2 and below
        [Inject] private GameplayModifiers mods;
        [Inject] private ScoreController sc;
        [Inject] private BeatmapObjectManager bomb;
        [Inject] private PlayerHeadAndObstacleInteraction wall;
#pragma warning restore CS0649
        #endregion
        #region Static Variables
        private static readonly HttpClient client = new HttpClient();
        public static Dictionary<string, Map> Data { get; private set; }
        public static string DisplayName => "Main";
        private static PluginConfig pc => PluginConfig.Instance;
        private static bool dataLoaded = false, fullDisable = false;
        private static MapSelection lastMap;
        public static IMyCounters theCounter { get; private set; }
        public static Dictionary<string, Type> StaticFunctions { get; private set; }
        public static Dictionary<string, Type> StaticProperties { get; private set; }
        public static Type[] ValidCounters { get; private set; }
        public static Dictionary<string, string> DisplayNameToCounter { get; private set; }
        public static string[] ValidDisplayNames { get; private set; }
        private static Func<bool, bool, float, float, int, string, string> displayFormatter;
        internal static Func<string, string, string> TargetFormatter;
        internal static Func<Func<string>, float, float, float, float, float, string> PercentNeededFormatter;
        private static Func<Func<Dictionary<char, object>, string>> displayIniter, targetIniter, percentNeededIniter;
        public static string[] Labels = new string[] { " Pass PP", " Acc PP", " Tech PP", " PP" };

        private static bool updateFormat;
        public static bool FormatUsable { get => displayFormatter != null && displayIniter != null; }
        public static bool TargetUsable { get => TargetFormatter != null && targetIniter != null; }
        public static bool PercentNeededUsable { get => PercentNeededFormatter != null && percentNeededIniter != null; }
        public static readonly Dictionary<string, char> FormatAlias = new Dictionary<string, char>()
        {
            { "PP", 'x' },
            { "FCPP", 'y' },
            { "Mistakes", 'e' },
            { "Label", 'l' }
        };
        public static readonly Dictionary<string, char> TargetAlias = new Dictionary<string, char>()
        {
            {"Target", 't' },
            {"Mods", 'm' }
        };
        public static readonly Dictionary<string, char> PercentNeededAlias = new Dictionary<string, char>()
        {
            {"Color", 'c' },
            {"Accuracy", 'a' },
            {"Tech PP", 'x' },
            {"Acc PP", 'y' },
            {"Pass PP", 'z' },
            {"PP", 'p' }
        };
        internal static readonly FormatRelation DefaultFormatRelation = new FormatRelation("Main Format", DisplayName, 
            pc.FormatSettings.DefaultTextFormat, str => pc.FormatSettings.DefaultTextFormat = str, FormatAlias, 
            new Dictionary<char, string>()
            {
                { 'x', "The unmodified PP number" },
                { 'y', "The unmodified PP number if the map was FC'ed" },
                { 'e', "The amount of mistakes made in the map. This includes bomb and wall hits" },
                { 'l', "The label (ex: PP, Tech PP, etc)" }
            }, str => { var hold = GetTheFormat(str, out string errorStr); return (hold, errorStr); },
            new Dictionary<char, object>()
            {
                {(char)1, true },
                {(char)2, true },
                {'x', 543.21f },
                {'y', 654.32f },
                {'e', 2 },
                {'l', " PP" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, null, null, new Dictionary<char, IEnumerable<(string, object)>>(2)
            {
                { 'x', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'y', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } }
            }, new (char, string)[2]
            {
                ((char)1, "Has a miss"),
                ((char)2, "Is bottom of text")
            }
            );
        internal static readonly FormatRelation TargetFormatRelation = new FormatRelation("Target Format", DisplayName,
            pc.MessageSettings.TargetingMessage, str => pc.MessageSettings.TargetingMessage = str, TargetAlias,
            new Dictionary<char, string>()
            {
                { 't', "The name of the person being targeted" },
                { 'm', "The mods used by the person you are targeting" }
            }, str => { var hold = GetFormatTarget(str, out string errorStr); return (hold, errorStr); },
            new Dictionary<char, object>()
            {
                {'t', "Person" },
                {'m', "SF" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, null, null, null);
        internal static readonly FormatRelation PercentNeededFormatRelation = new FormatRelation("Percent Needed Format", DisplayName,
            pc.MessageSettings.PercentNeededMessage, str => pc.MessageSettings.PercentNeededMessage = str, PercentNeededAlias,
            new Dictionary<char, string>()
            {
                { 'c', "Must use as a group value, and will color everything inside group" },
                { 'a', "The accuracy needed to capture the map" },
                { 'x', "The tech PP needed" },
                { 'y', "The accuracy PP needed" },
                { 'z', "The pass PP needed" },
                { 'p', "The total PP number needed to capture the map" }
            }, str => { var hold = GetFormatPercentNeeded(str, out string errorStr); return (hold, errorStr); },
            new Dictionary<char, object>()
            {
                {'c', new Func<object>(() => "green") },
                {'a', "95.85" },
                {'x', 114.14f },
                {'y', 321.23f },
                {'z', 69.42f },
                {'p', 543.21f },
                {'t', "Person" }
            }, HelpfulFormatter.GLOBAL_PARAM_AMOUNT, new Dictionary<char, int>(3)
            {
                {'c', 0 },
                {'a', 1 },
                {'t', 2 }
            }, new Func<object, bool, object>[3]
            {
                FormatRelation.CreateFuncWithWrapper("<color={0}>{0}", "<color={0}>"),
                FormatRelation.CreateFunc("{0}%", "{0}"),
                FormatRelation.CreateFunc("Targeting <color=red>{0}</color>")
            }, new Dictionary<char, IEnumerable<(string, object)>>(5)
            {
                { 'a', new (string, object)[3] { ("MinVal", 0), ("MaxVal", 100), ("IncrementVal", 1.5f), } },
                { 'x', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'y', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'z', new (string, object)[3] { ("MinVal", 10), ("MaxVal", 1000), ("IncrementVal", 10), } },
                { 'p', new (string, object)[3] { ("MinVal", 100), ("MaxVal", 1000), ("IncrementVal", 10), } }
            }, null, new (char, ValueListInfo.ValueType)[1]
            {
                ('c', ValueListInfo.ValueType.Color)
            });
        #endregion
        #region Variables
        private TMP_Text display;
        private bool enabled;
        private float passRating, accRating, techRating, stars;
        private int notes, comboNotes, mistakes, totalNotes;
        private int fcTotalHitscore, fcMaxHitscore;
        private double totalHitscore, maxHitscore;
        private string mode, lastTarget;
        #endregion
        #region Inits & Overrides

        internal static void InitCounterStatic() 
        {
            updateFormat = false;
            SettingsHandler.NewInstance += (handler) => handler.PropertyChanged += (a,b) => updateFormat = true;
            SettingsHandler.Instance.PropertyChanged += (a,b) => updateFormat = true;

            StaticFunctions = new Dictionary<string, Type>() 
            { { "InitFormat", typeof(bool) } };
            StaticProperties = new Dictionary<string, Type>()
            { {"DisplayName", typeof(string) }, {"OrderNumber", typeof(int) }, {"DisplayHandler", typeof(string) } };

            GetMethodFromTypes("InitFormat", typeof(TheCounter)); //Call this by itself because it is not a chooseable counter.

            try
            {
                var validTypes = GetValidCounters();
                Dictionary<string, object> methodOutp = GetMethodFromTypes("InitFormat", validTypes);
                for (int i = validTypes.Length - 1; i >= 0; i--)
                {
                    if (methodOutp[validTypes[i].FullName] is bool v)
                        if (!v) validTypes[i] = null;
                }
                ValidCounters = validTypes.Where(a => a != null).ToArray();
                Dictionary<string, object> propertyOutp = GetPropertyFromTypes("DisplayName", ValidCounters);
                foreach (var toCheck in GetPropertyFromTypes("DisplayHandler", ValidCounters).Where(a => (a.Value as string).Equals(DisplayName)))
                    if (!FormatTheFormat(pc.FormatSettings.DefaultTextFormat, propertyOutp[toCheck.Key] as string))
                    {
                        ValidCounters[ValidCounters.IndexOf(ValidCounters.First(a => a.Name.Equals(propertyOutp[toCheck.Key] as string)))] = null;
                        propertyOutp.Remove(toCheck.Key);
                    }
                ValidCounters = ValidCounters.Where(a => a != null).ToArray();
                DisplayNameToCounter = new Dictionary<string, string>();
                foreach (var val in propertyOutp)
                    if (val.Value is string name)
                        DisplayNameToCounter.Add(name, val.Key);
                Dictionary<int, string> propertyOrder = GetPropertyFromTypes("OrderNumber", ValidCounters).ToDictionary(x => (int)x.Value, x => x.Key);
                List<string> displayNames = new List<string>();
                var sortedOrderNumbers = propertyOrder.Keys.OrderBy(x => x);
                foreach (int i in sortedOrderNumbers)
                        displayNames.Add(propertyOutp[propertyOrder[i]] as string);
                ValidDisplayNames = displayNames.ToArray();
            } catch (Exception e)
            {
                Plugin.Log.Error("Oh no! The static check for counters broke somehow :(");
                Plugin.Log.Error(e.ToString());
                return;
            }
            if (ValidDisplayNames.Length > 0)
            {
                if (!ValidDisplayNames.Contains(pc.PPType))
                    pc.PPType = ValidDisplayNames[0];
            } else
            {
                Plugin.Log.Critical("No counter is in working order!!! Shutting down this counter as it will only cause issues.");
                fullDisable = true;
                ValidDisplayNames = new string[] { "There are none" };
            }

        }
        public static bool InitFormat()
        {
            bool success = FormatTheFormat(pc.FormatSettings.DefaultTextFormat), hold;
            if (success) InitDisplayFormat();
            hold = FormatTarget(pc.MessageSettings.TargetingMessage);
            success &= hold;
            if (hold) InitTarget();
            hold = FormatPercentNeeded(pc.MessageSettings.PercentNeededMessage);
            if (hold) InitPercentNeeded();
            return success && hold;
        }
        public override void CounterDestroy() {
            if (enabled) ChangeNotifiers(false);
        }
        public override void CounterInit()
        {
            enabled = false;
            if (fullDisable) return;
            notes = fcMaxHitscore = comboNotes = fcTotalHitscore = mistakes = 0;
            totalHitscore = maxHitscore = 0.0;
            if (!dataLoaded)
            {
                Data = new Dictionary<string, Map>();
                client.Timeout = new TimeSpan(0, 0, 3);
                lastTarget = Targeter.NO_TARGET;
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
                    display.fontSize = (float)pc.FontSize;
                    display.text = "";
                    ChangeNotifiers(true);
                    loadedEvents = true;
                    //string hash = beatmap.level.levelID.Split('_')[2]; // 1.34.2 and below
                    string hash = beatmap.levelID.Split('_')[2]; // 1.37.0 and above
                    bool counterChange = theCounter != null && !theCounter.Name.Equals(pc.PPType.Split(' ')[0]);
                    if (counterChange)
                        if ((GetPropertyFromTypes("DisplayHandler", theCounter.GetType()).Values.First() as string).Equals(DisplayName))
                            //Need to recall this one so that it implements the current counter's wants properly
                            if (FormatTheFormat(pc.FormatSettings.DefaultTextFormat)) InitDisplayFormat();
                    if (counterChange || lastMap.Equals(default) || hash != lastMap.Hash || pc.PPType.Equals("Progressive") || lastTarget != pc.Target)
                    {
                        Data.TryGetValue(hash, out Map m);
                        if (m == null) 
                        {
                            Plugin.Log.Warn("Map not in cache, attempting API call to get map data...");
                            RequestHashData();
                            m = Data[hash];
                        }
                        //lastMap = new MapSelection(m, beatmap.difficulty, mode, passRating, accRating, techRating); // 1.34.2 and below
                        lastMap = new MapSelection(m, beatmapDiff.difficulty, mode, passRating, accRating, techRating); // 1.37.0 and above
                        totalNotes = HelpfulMath.NotesForMaxScore((int)lastMap.MapData.Item2["maxScore"]);
                        if (!InitCounter()) throw new Exception("Counter somehow failed to init. Weedoo weedoo weedoo weedoo.");
                    }
                    else
                        APIAvoidanceMode();
                    lastTarget = pc.Target;
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
                if (loadedEvents) ChangeNotifiers(false);
            }
        }
        #endregion
        #region Event Calls
        private void OnNoteScored(ScoringElement scoringElement)
        {
            if (scoringElement.noteData.gameplayType == NoteData.GameplayType.Bomb)
                return;
            NoteData.ScoringType st = scoringElement.noteData.scoringType;
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
            else OnMiss();
            Finish:
            theCounter.UpdateCounter((float)(totalHitscore / maxHitscore), notes, mistakes, fcTotalHitscore / (float)fcMaxHitscore);
            if (notes == totalNotes) ClearCounter();
        }

        private void OnBombHit(NoteController nc, in NoteCutInfo nci)
        {
            if(nc.noteData.gameplayType == NoteData.GameplayType.Bomb)
                OnMiss();
        }
        private void OnWallHit(ObstacleController oc)
        {
            OnMiss();
        }
        private void OnMiss()
        {
            if (notes == 0) return;
            comboNotes = HelpfulMath.DecreaseMultiplier(comboNotes);
            mistakes++;
        }
        #endregion
        #region API Calls
        private string RequestHashData()
        {
            string path = HelpfulPaths.BLAPI_HASH + beatmap.levelID.Split('_')[2].ToUpper(); // 1.37.0 and above
            //string path = HelpfulPaths.BLAPI_HASH + beatmap.level.levelID.Split('_')[2].ToUpper(); // 1.34.2 and below
            try
            {
                string data = client.GetStringAsync(new Uri(path)).Result;
                AddMap(data);
                return data;
            }
            catch (HttpRequestException e)
            {
                Plugin.Log.Warn($"Beat Leader API request for map info failed!\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                return "";
            }
        }
        public static bool CallAPI(string path, out string output)
        {
            path = HelpfulPaths.BLAPI + path;
            try
            {
                output = client.GetStringAsync(new Uri(path)).Result;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Beat Leader API request failed\nPath: {path}\nError: {e.Message}");
                Plugin.Log.Debug(e);
                output = "";
                return false;
            }
        }
        #endregion
        #region Helper Methods
        private void ChangeNotifiers(bool a)
        {
            if (a)
            {
                sc.scoringForNoteFinishedEvent += OnNoteScored;
                wall.headDidEnterObstacleEvent += OnWallHit;
                bomb.noteWasCutEvent += OnBombHit;
            } else
            {
                sc.scoringForNoteFinishedEvent -= OnNoteScored;
                wall.headDidEnterObstacleEvent -= OnWallHit;
                bomb.noteWasCutEvent -= OnBombHit;
            }
        }
        public static void ClearCounter() => lastMap = default;
        public static void ForceLoadMaps()
        {
            if (dataLoaded) return;
            client.Timeout = new TimeSpan(0, 0, 3);
            Data = new Dictionary<string, Map>();
            InitData();
        }
        private static Func<Func<Dictionary<char, object>, string>> GetTheFormat(string format, out string errorStr, string counter = "") =>
            HelpfulFormatter.GetBasicTokenParser(format, FormatAlias, counter, a => { },
                (tokens, tokensCopy, priority, vals) => 
                { 
                    if (!(bool)vals[(char)1]) HelpfulFormatter.SetText(tokensCopy, '1'); 
                    if (!(bool)vals[(char)2]) HelpfulFormatter.SetText(tokensCopy, '2'); 
                }, out errorStr);
       
        private static bool FormatTheFormat(string format, string counter = "") 
        { 
            displayIniter = GetTheFormat(format, out string _, counter);
            return displayIniter != null; 
        }
        private static Func<Func<Dictionary<char, object>, string>> GetFormatTarget(string format, out string errorStr) =>
            HelpfulFormatter.GetBasicTokenParser(format, TargetAlias, DisplayName, a => { }, (a, b, c, d) => { }, out errorStr);
        private static bool FormatTarget(string format)
        {
            targetIniter = GetFormatTarget(format, out string _);
            return targetIniter != null;
        }
        private static Func<Func<Dictionary<char, object>, string>> GetFormatPercentNeeded(string format, out string errorStr) =>
            HelpfulFormatter.GetBasicTokenParser(format, PercentNeededAlias, DisplayName, a => { },
                (tokens, tokensCopy, priority, vals) =>
                {
                    if (vals.ContainsKey('c')) HelpfulFormatter.SurroundText(tokensCopy, 'c', $"{((Func<object>)vals['c']).Invoke()}", "</color>");
                }, out errorStr);
        private static bool FormatPercentNeeded(string format)
        {
            percentNeededIniter = GetFormatPercentNeeded(format, out string _);
            return percentNeededIniter != null;
        }
        private static void InitDisplayFormat()
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
            //The line below adapted from: https://stackoverflow.com/questions/26733/getting-all-types-that-implement-an-interface
            var types = Assembly.GetExecutingAssembly().GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IMyCounters)));
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
                else
                {
                    var fields = t.GetFields(BindingFlags.Public | BindingFlags.Static);
                    foreach (FieldInfo f in fields)
                        if (Vars.ContainsKey(f.Name))
                            if (f.FieldType == Vars[f.Name])
                            {
                                if (f.IsInitOnly) Vars.Remove(f.Name);
                                else Plugin.Log.Warn($"For counter {t.Name}, they have the valid variable {f.Name} but need to set it to readonly.");
                                if (Vars.Count == 0) break;
                            }
                            else break;
                    if (Vars.Count == 0)
                        counters.Add(t);
                }
            }
            return counters.ToArray();
        }
        private static Dictionary<string, object> GetMethodFromTypes(string methodName, params Type[] types) =>
            GetMethodFromTypes(methodName, BindingFlags.Public | BindingFlags.Static, types);
        private static Dictionary<string, object> GetMethodFromTypes(string methodName, BindingFlags flags, params Type[] types)
        {
            Dictionary<string, object> outp = new Dictionary<string, object>();
            foreach (Type t in types)
            {
                var method = t.GetMethods(flags).First(a => a.Name.Equals(methodName));
                outp.Add(t.FullName, method.Invoke(null, null));
            }
            return outp;
        }
        private static Dictionary<string, object> GetPropertyFromTypes(string propertyName, params Type[] types) =>
            GetPropertyFromTypes(propertyName, BindingFlags.Public | BindingFlags.Static, types);
        private static Dictionary<string, object> GetPropertyFromTypes(string propertyName, BindingFlags flags, params Type[] types)
        {
            bool hasFlags = flags != 0;
            Dictionary<string, object> outp = new Dictionary<string, object>();
            foreach (Type t in types)
            {
                var method = (hasFlags ? t.GetProperties(flags) : t.GetProperties()).First(a => a.Name.Equals(propertyName));
                outp.Add(t.FullName, method.GetValue(null));
            }
            return outp;
        }
        #endregion
        #region Init
        private bool InitCounter()
        {
            var outpCounter = InitCounter(pc.PPType, display);
            if (outpCounter == null) return false;
            theCounter = outpCounter;
            return true;
        }
        public static IMyCounters InitCounter(string name, TMP_Text display)
        {
            if (!DisplayNameToCounter.TryGetValue(name, out string displayName)) return null;
            Type counterType = ValidCounters.FirstOrDefault(a => a.FullName.Equals(displayName));
            if (counterType == default) 
                throw new ArgumentException($"Name '{displayName}' is not a counter! Valid counter names are:\n{string.Join("\n", ValidCounters as IEnumerable<Type>)}");
            IMyCounters outp = (IMyCounters)Activator.CreateInstance(counterType, display, lastMap);
            outp.UpdateFormat();
            return outp;
        }
        private void APIAvoidanceMode()
        {
            Plugin.Log.Debug("API Avoidance mode is functioning (probably)!");
            //MapSelection thisMap = new MapSelection(Data[lastMap.Hash], beatmap.difficulty, mode, passRating, accRating, techRating); // 1.34.2 and below
            MapSelection thisMap = new MapSelection(Data[lastMap.Hash], beatmapDiff.difficulty, mode, passRating, accRating, techRating); // 1.37.0 and above
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
                    Map map = new Map(hash, songId + mapToken["value"] + mapToken["mode"], mapToken);
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
            string hash = beatmap.levelID.Split('_')[2].ToUpper(); // 1.37.0 and above
            //string hash = beatmap.level.levelID.Split('_')[2].ToUpper(); // 1.34.2 and below
            try
            {
                if (!Data.TryGetValue(hash, out Map theMap)) throw new KeyNotFoundException("The map is not in the loaded cache.");
                /*Dictionary<string, (string, JToken)> hold = theMap.Get(beatmap.difficulty);
                mode = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;// 1.34.2 and below */
                Dictionary<string, (string, JToken)> hold = theMap.Get(beatmapDiff.difficulty); 
                mode = beatmapDiff.beatmapCharacteristic.serializedName;// 1.37.0 and above */
                if (mode == default) mode = "Standard";
                if (!hold.TryGetValue(mode, out var holdInfo)) throw new KeyNotFoundException($"The mode '{mode}' doesn't exist.\nKeys: [{string.Join(", ", hold.Keys)}]");
                data = holdInfo.Item2;
                songId = holdInfo.Item1;
            }
            catch (Exception e)
            {
                Plugin.Log.Debug($"Data length: {Data.Count}");
                Plugin.Log.Warn("Hash: " + hash);
                Plugin.Log.Debug(e);
                return false;
            }
            Plugin.Log.Info("Map Hash: " + hash);
            return SetupMapData(data);
        }
        private bool SetupMapData(JToken data)
        {
            if (data == null || data.ToString().Length <= 0) return false;
            float multiplier = GetStarMultiplier(data);
            passRating = HelpfulPaths.GetRating(data, PPType.Pass, mods.songSpeed) * multiplier;
            accRating = HelpfulPaths.GetRating(data, PPType.Acc, mods.songSpeed) * multiplier;
            techRating = HelpfulPaths.GetRating(data, PPType.Tech, mods.songSpeed) * multiplier;
            stars = HelpfulPaths.GetRating(data, PPType.Star, mods.songSpeed);
            string mod = HelpfulMisc.GetModifierShortname(mods.songSpeed).ToUpper();
            Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {stars}\n{mod} Pass Rating: {passRating}\n{mod} Acc Rating: {accRating}\n{mod} Tech Rating: {techRating}" : $"Stars: {stars}\nPass Rating: {passRating}\nAcc Rating: {accRating}\nTech Rating: {techRating}");
            return stars > 0;
        }
        private float GetStarMultiplier(JToken data)
        {
            float outp = 1.0f;
            if (mods.ghostNotes) outp += HelpfulPaths.GetMultiAmount(data, "gn");
            if (mods.noArrows) outp += HelpfulPaths.GetMultiAmount(data, "na");
            if (mods.noFailOn0Energy) outp += HelpfulPaths.GetMultiAmount(data, "nf");
            if (mods.enabledObstacleType == EnabledObstacleType.NoObstacles) outp += HelpfulPaths.GetMultiAmount(data, "no");
            if (mods.noBombs) outp += HelpfulPaths.GetMultiAmount(data, "nb");
            return outp;
        }
        #endregion
        #region Updates
        
        public static void UpdateText(bool displayFc, TMP_Text display, float[] ppVals, int mistakes)
        {
            if (pc.SplitPPVals) {
                string outp = "";
                for (int i=0;i<4;i++)
                    outp += displayFormatter.Invoke(displayFc, pc.ExtraInfo && i == 3, ppVals[i], ppVals[i + 4], mistakes, Labels[i]) + "\n";
                display.text = outp;
            } else
                display.text = displayFormatter.Invoke(displayFc, pc.ExtraInfo, ppVals[3], ppVals[7], mistakes, Labels[3]);
        }
        #endregion
    }
}