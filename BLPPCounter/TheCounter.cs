using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Counters;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Utils;
using BLPPCounter.Utils.API_Handlers;
using BLPPCounter.Utils.List_Settings;
using BS_Utils.Utilities;
using CountersPlus.Counters.Custom;
using ModestTree;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
#if NEW_VERSION
using System.Runtime.InteropServices.WindowsRuntime; // Used in 1.37.0 and above
#endif
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Zenject;
using static GameplayModifiers;
namespace BLPPCounter
{

    public class TheCounter : BasicCustomCounter
    {
        #region Injects
#pragma warning disable CS0649, IDE0044
#if NEW_VERSION
        [Inject] private BeatmapLevel beatmap;
        [Inject] private BeatmapKey beatmapDiff; // 1.37.0 and above */
#else

        [Inject] private IDifficultyBeatmap beatmap; // 1.34.2 and below
#endif
        [Inject] private GameplayModifiers mods;
        [Inject] private ScoreController sc;
        [Inject] private BeatmapObjectManager bomb;
        [Inject] private PlayerHeadAndObstacleInteraction wall;
#pragma warning restore CS0649
        #endregion
        #region Static Variables
        public static Dictionary<string, Map> Data { get; private set; }
        public static string DisplayName => "Main";
        private static PluginConfig pc => PluginConfig.Instance;
        private static bool dataLoaded = false, fullDisable = false, usingDefaultLeaderboard = false;
        internal static Leaderboards Leaderboard => usingDefaultLeaderboard ? pc.DefaultLeaderboard : pc.Leaderboard;
        internal static MapSelection LastMap;
        internal static GameplayModifiers LastMods;
        public static IMyCounters theCounter { get; internal set; }
        public static ReadOnlyDictionary<string, Type> StaticFunctions { get; private set; }
        public static ReadOnlyDictionary<string, Type> StaticProperties { get; private set; }
        public static Type[] ValidCounters { get; private set; }
        public static ReadOnlyDictionary<Leaderboards, string[]> ValidDisplayNames;
        public static string[] DisplayNames => fullDisable ? new string[1] { "There are none" } : ValidDisplayNames[Leaderboard];
        public static Dictionary<string, string> DisplayNameToCounter { get; private set; }
        private static Func<bool, bool, float, float, int, string, string> displayFormatter;
        internal static Func<string, string, string> TargetFormatter;
        internal static Func<Func<string>, float, float, float, float, float, string> PercentNeededFormatter;
        private static Func<Func<Dictionary<char, object>, string>> displayIniter, targetIniter, percentNeededIniter;
        private static readonly ReadOnlyCollection<string> Labels = new ReadOnlyCollection<string>(new string[] { " Acc PP", " Pass PP", " Tech PP", " PP" }); //Ain't Nobody appending a billion "BL"s to this now :)
        public static string[] CurrentLabels { get; private set; } = null;

        private static bool updateFormat;
        public static bool SettingChanged = false;
        public static bool FormatUsable => displayFormatter != null && displayIniter != null;
        public static bool TargetUsable => TargetFormatter != null && targetIniter != null;
        public static bool PercentNeededUsable => PercentNeededFormatter != null && percentNeededIniter != null;
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
        private static TimeLooper TimeLooper = new TimeLooper();
        private static string lastTarget = Targeter.NO_TARGET;
        #endregion
        #region Variables
        private TMP_Text display;
        private bool enabled, loading, hasNotifiers;
        private float passRating, accRating, techRating, starRating;
        private int notes, comboNotes, mistakes;
        private int fcTotalHitscore, fcMaxHitscore;
        private double totalHitscore, maxHitscore;
        private string mode, hash;
        #endregion
        #region Inits & Overrides

        internal static void InitCounterStatic() 
        {
            updateFormat = false;
            void PropChanged(object o, PropertyChangedEventArgs args)
            {
                updateFormat = true;
            }
            SettingsHandler.NewInstance += (handler) => handler.PropertyChanged += PropChanged;
            SettingsHandler.Instance.PropertyChanged += PropChanged;

            StaticFunctions = new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>() 
            { { "InitFormat", typeof(bool) }, { "ResetFormat", typeof(void) } });
            StaticProperties = new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>()
            { {"DisplayName", typeof(string) }, {"OrderNumber", typeof(int) }, {"DisplayHandler", typeof(string) }, {"ValidLeaderboards", typeof(Leaderboards) } });

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
                Dictionary<string, string> counterToDisplayName = new Dictionary<string, string>();
                foreach (var val in propertyOutp)
                    if (val.Value is string name) 
                    {
                        DisplayNameToCounter.Add(name, val.Key);
                        counterToDisplayName.Add(val.Key, name);
                    }
                Dictionary<int, string> propertyOrder = GetPropertyFromTypes("OrderNumber", ValidCounters).ToDictionary(x => (int)x.Value, x => x.Key);
                List<string> displayNames = new List<string>();
                var sortedOrderNumbers = propertyOrder.Keys.OrderBy(x => x);
                foreach (int i in sortedOrderNumbers)
                        displayNames.Add(propertyOutp[propertyOrder[i]] as string);
                Dictionary<string, Leaderboards> hold = new Dictionary<string, Leaderboards>(GetPropertyFromTypes("ValidLeaderboards", ValidCounters).Select(kvp => new KeyValuePair<string, Leaderboards>(kvp.Key, (Leaderboards)kvp.Value)));
                Dictionary<Leaderboards, string[]> DNames = new Dictionary<Leaderboards, string[]>();
                foreach (Leaderboards l in Enum.GetValues(typeof(Leaderboards)))
                    DNames[l] = hold.Where(kvp => (kvp.Value & l) > 0).Select(kvp => kvp.Key).ToArray();
                IEnumerable<Leaderboards> keys = (Leaderboards[])Enum.GetValues(typeof(Leaderboards));
                foreach (Leaderboards l in keys)
                {
                    HashSet<string> ls = new HashSet<string>(DNames[l].Select(n => counterToDisplayName[n]));
                    DNames[l] = displayNames.Where(str => ls.Contains(str)).ToArray();
                }
                ValidDisplayNames = new ReadOnlyDictionary<Leaderboards, string[]>(DNames);
            } catch (Exception e)
            {
                Plugin.Log.Error("Oh no! The static check for counters broke somehow :(");
                Plugin.Log.Error(e.ToString());
                return;
            }
            if (DisplayNames.Count() > 0)
            {
                if (!DisplayNames.Contains(pc.PPType))
                    pc.PPType = DisplayNames[0];
            } else
            {
                Plugin.Log.Critical("No counter is in working order!!! Shutting down this counter as it will only cause issues.");
                fullDisable = true;
            }
            LoadSomeTaohableData();
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
        public static void ResetFormat()
        {
            displayIniter = null;
            displayFormatter = null;
            targetIniter = null;
            TargetFormatter = null;
            percentNeededIniter = null;
            PercentNeededFormatter = null;
        }
        public override void CounterDestroy() {
            //Plugin.Log.Info($"hash: {hash} || songName: {songName}");
            usingDefaultLeaderboard = false;
            if (hasNotifiers) 
                ChangeNotifiers(false);
            if (enabled)
                TimeLooper.End();
        }
        public override void CounterInit()
        {
            enabled = hasNotifiers = false;
            if (fullDisable) return;
            notes = fcMaxHitscore = comboNotes = fcTotalHitscore = mistakes = 0;
            totalHitscore = maxHitscore = 0.0;
            ChangeNotifiers(true);
            display = CanvasUtility.CreateTextFromSettings(Settings);
            display.fontSize = (float)pc.FontSize;
            display.text = "Loading...";
            Task.Run(async () =>
            {
                loading = true;
                await AsyncCounterInit().ConfigureAwait(false);
                loading = false;
            });
        }
        private async Task AsyncCounterInit() 
        {
            if (!dataLoaded)
            {
                Data = new Dictionary<string, Map>();
                InitData();
            }
            try
            {
                if (!dataLoaded) RequestHashData();
                enabled = SetupMapData();
                if (enabled)
                {
#if NEW_VERSION
                    hash = beatmap.levelID.Split('_')[2]; // 1.37.0 and above
#else
                    hash = beatmap.level.levelID.Split('_')[2]; // 1.34.2 and below
#endif
                    bool counterChange = theCounter != null && !theCounter.Name.Equals(pc.PPType);
                    if (counterChange)
                        if ((GetPropertyFromTypes("DisplayHandler", theCounter.GetType()).Values.First() as string).Equals(DisplayName))
                            //Need to recall this one so that it implements the current counter's wants properly
                            if (FormatTheFormat(pc.FormatSettings.DefaultTextFormat)) InitDisplayFormat();
                    //Plugin.Log.Info($"CounterChange = {counterChange}\nNULL CHECKS\nLast map: {lastMap.Equals(default)}, hash: {hash is null}, pc: {pc is null}, PPType: {pc?.PPType is null}, lastTarget: {lastTarget is null}, Target: {pc.Target is null}");
                    if (theCounter is null || SettingChanged || counterChange || LastMap.Equals(default) || !hash.Equals(LastMap.Hash) || pc.PPType.Equals(ProgressCounter.DisplayName) || !lastTarget.Equals(pc.Target))
                    {
                        SettingChanged = false;
                        Map m = await GetMap(hash, mode, Leaderboard).ConfigureAwait(false);
#if NEW_VERSION
                        MapSelection ms = new MapSelection(m, beatmapDiff.difficulty, mode, starRating, accRating, passRating, techRating); // 1.34.2 and below
#else
                        MapSelection ms = new MapSelection(m, beatmap.difficulty, mode, starRating, accRating, passRating, techRating); // 1.37.0 and below
#endif
                        if (!ms.IsUsable)
                        {
                            Plugin.Log.Warn("The status of this map marks it as unusable.");
                            goto Failed;
                        } 
                        LastMap = ms;
                        LastMods = mods;
                        APIHandler.UsingDefault = usingDefaultLeaderboard;
                        Calculator.UsingDefault = usingDefaultLeaderboard;
                        if (!InitCounter())
                        {
                            Plugin.Log.Warn("Counter somehow failed to init. Weedoo weedoo weedoo weedoo.");
                            goto Failed;
                        }
                    }
                    else if (!APIAvoidanceMode())
                        goto Failed;
                    lastTarget = pc.Target;
                    if (updateFormat) { theCounter.UpdateFormat(); updateFormat = false; }
                    if (pc.UpdateAfterTime) SetTimeLooper();
                    SetLabels();
                    if (notes < 1) theCounter.UpdateCounter(1, 0, 0, 1);
                    return;
                } else
                    Plugin.Log.Warn("Maps failed to load, most likely unranked.");
            } catch (Exception e)
            {
                Plugin.Log.Error($"The counter failed to be initialized: {e.Message}\nSource: {e.Source}");
                if (e is KeyNotFoundException) Plugin.Log.Error($"Data dictionary length: {Data.Count}");
                Plugin.Log.Debug(e);
                if (usingDefaultLeaderboard)
                {
                    enabled = false;
                    display.text = "";
                    if (hasNotifiers) ChangeNotifiers(false);
                }
            }
            Failed:
            if (!usingDefaultLeaderboard)
            {
                usingDefaultLeaderboard = true;
                usingDefaultLeaderboard = DisplayNames.Contains(pc.PPType);
                if (!usingDefaultLeaderboard) return;
                await AsyncCounterInit();
            } else
            {
                enabled = false;
                display.text = "";
                if (hasNotifiers) ChangeNotifiers(false);
            }
        }
#endregion
        #region Event Calls
        private void OnNoteScored(ScoringElement scoringElement)
        {
            if (scoringElement.noteData.gameplayType == NoteData.GameplayType.Bomb)
                return;
            bool enteredLock = !loading && pc.UpdateAfterTime && Monitor.TryEnter(TimeLooper.Locker); //This is to make sure timeLooper is paused, not to pause this thread.
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
            if (loading) return;
            theCounter.SoftUpdate((float)(totalHitscore / maxHitscore), notes, mistakes, fcTotalHitscore / (float)fcMaxHitscore);
            if (enteredLock) Monitor.Exit(TimeLooper.Locker);
            if (!pc.UpdateAfterTime) theCounter.UpdateCounter((float)(totalHitscore / maxHitscore), notes, mistakes, fcTotalHitscore / (float)fcMaxHitscore);
            else if (TimeLooper.IsPaused) TimeLooper.SetStatus(false);
        }

        private void OnBombHit(NoteController nc, in NoteCutInfo nci)
        {
            if (nc.noteData.gameplayType == NoteData.GameplayType.Bomb)
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
        private void SetTimeLooper()
        {
            TimeLooper.Delay = (int)(pc.UpdateTime * 1000);
            int currentNotes = 0;
            TimeLooper.GenerateTask(() =>
            {
                if (currentNotes == notes) return true;
                theCounter.UpdateCounter((float)(totalHitscore / maxHitscore), notes, mistakes, fcTotalHitscore / (float)fcMaxHitscore);
                currentNotes = notes;
                return false;
            });
        }
        #endregion
        #region API Calls
        private void RequestHashData() => APIHandler.GetAPI(usingDefaultLeaderboard).AddMap(Data, hash);
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
            hasNotifiers = a;
        }
        public static void ClearCounter() => LastMap = default;
        public static void ForceLoadMaps()
        {
            if (dataLoaded) return;
            Data = new Dictionary<string, Map>();
            InitData();
        }
        public static async Task<Map> GetMap(string hash, string mode, Leaderboards leaderboard)
        {
            if (!dataLoaded) ForceLoadMaps();
            if ((!Data.TryGetValue(hash, out Map m) || !m.GetModes().Contains(mode)))
            {
                //Plugin.Log.Warn("Map not in cache, attempting API call to get map data...");
                await APIHandler.GetAPI(leaderboard).AddMap(Data, hash).ConfigureAwait(false);
                m = Data[hash];
            }
            return m;
        }
        public static MapSelection GetDifficulty(Map m, BeatmapDifficulty diff, Leaderboards leaderboard, string mode = "Standard", GameplayModifiers mods = null, bool quiet = false)
        {
            (_, JToken diffData) = m.Get(mode, diff);
            if (!SetupMapData(diffData, leaderboard, out float[] ratings, mods, quiet)) return default;
            return new MapSelection(m, diff, mode, ratings);
        }
        public static string SelectMode(string mainMode, Leaderboards leaderboard)
        {
            switch (leaderboard)
            {
                case Leaderboards.Scoresaber:
                    return Map.SS_MODE_NAME;
                case Leaderboards.Accsaber:
                    return Map.AP_MODE_NAME;
                default: 
                    return mainMode ?? "Standard";
            }
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
        private static void SetLabels()
        {
            CurrentLabels = new string[Calculator.GetCalc(usingDefaultLeaderboard).DisplayRatingCount];
            string predicate = pc.LeaderInLabel ? Calculator.GetCalc(usingDefaultLeaderboard).Label : "";
            for (int i = 0; i < CurrentLabels.Length; i++)
            if (pc.LeaderInLabel && ((usingDefaultLeaderboard && pc.DefaultLeaderboard != Leaderboards.Accsaber) || (!usingDefaultLeaderboard && pc.Leaderboard != Leaderboards.Accsaber)))
                CurrentLabels[i] = predicate + Labels[CurrentLabels.Length == 1 ? 3 : i];
            else CurrentLabels[i] = predicate;
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
            IMyCounters outp = (IMyCounters)Activator.CreateInstance(counterType, display, LastMap);
            outp.UpdateFormat();
            return outp;
        }
        private bool APIAvoidanceMode()
        {
            Plugin.Log.Debug("API Avoidance mode is functioning (probably)!");
#if NEW_VERSION
            MapSelection thisMap = new MapSelection(Data[LastMap.Hash], beatmapDiff.difficulty, mode, starRating, accRating, passRating, techRating); // 1.37.0 and above
#else
            MapSelection thisMap = new MapSelection(Data[LastMap.Hash], beatmap.difficulty, mode, starRating, accRating, passRating, techRating); // 1.34.2 and below
#endif
            if (!thisMap.IsUsable) return false;
            Plugin.Log.Debug($"Last Map\n-------------------\n{LastMap}\n-------------------\nThis Map\n-------------------\n{thisMap}\n-------------------");
            bool ratingDiff, diffDiff;
            (ratingDiff, diffDiff) = thisMap.GetDifference(LastMap);
            Plugin.Log.Debug($"DID CHANGE || Rating: {ratingDiff}, Difficulty: {diffDiff}");
            if (diffDiff) theCounter.ReinitCounter(display, thisMap);
            else if (ratingDiff) theCounter.ReinitCounter(display, passRating, accRating, techRating, starRating);
            else theCounter.ReinitCounter(display);
            LastMap = thisMap;
            return true;
        }
        private static void InitData(bool loadOnlySS = false, bool doNotLoop = false)
        {
            dataLoaded = false;
            if (!loadOnlySS && File.Exists(HelpfulPaths.BL_CACHE_FILE))
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
                    
                    
                }
                catch (Exception e)
                {
                    Plugin.Log.Warn("Error loading bl Cache file: " + e.Message);
                    Plugin.Log.Debug(e);
                    return;
                }
            }
            try
            {
                JEnumerable<JToken> results = JToken.Parse(File.ReadAllText(HelpfulPaths.TAOHABLE_DATA)).Children();
                foreach (JToken result in results)
                {
                    bool ssRanked = false, apRanked = false;
                    if (result is JObject jo)
                    {
#if NEW_VERSION
                        ssRanked = jo.ContainsKey("starScoreSaber");  // 1.37.0 and above
                        apRanked = jo.ContainsKey("complexityAccSaber");
#else
                        ssRanked = jo.Property("starScoreSaber") != null; // 1.34.0 and below 
                        apRanked = jo.Property("complexityAccSaber") != null; 
#endif
                    }
                    if (!ssRanked && !apRanked) continue;
                    Map apMap = null, ssMap = null;
                    if (apRanked)
                        apMap = new Map(result["hash"].ToString().ToUpper(), Map.AP_MODE_NAME, Map.FromValue(int.Parse(result["difficulty"].ToString())), result["scoreSaberID"].ToString(), result);
                    if (ssRanked)
                        ssMap = new Map(result["hash"].ToString().ToUpper(), Map.SS_MODE_NAME, Map.FromValue(int.Parse(result["difficulty"].ToString())), result["scoreSaberID"].ToString(), result);
                    Map map = ssRanked && apRanked ? Map.Combine(apMap, ssMap) : ssRanked ? ssMap : apMap;
                    if (Data.ContainsKey(map.Hash))
                        Data[map.Hash].Combine(map);
                    else Data[map.Hash] = map;
                }
                dataLoaded = true;
            }
            catch (Exception e)
            {
                Plugin.Log.Warn("Error loading Taohable Cache file: " + e.Message);
                Plugin.Log.Debug(e);
                if (!doNotLoop && e is JsonReaderException)
                {
                    File.Delete(HelpfulPaths.TAOHABLE_DATA); //In the case that there is an issue loading the file, try again.
                    LoadSomeTaohableData();
                    InitData(true, true);
                } 
            }
        }
        private static void LoadSomeTaohableData()
        {
            if (HelpfulPaths.EnsureTaohableDirectoryExists()) return;
            Plugin.Log.Debug("Taoh data not up to date! Loading...");
            string filePath = HelpfulPaths.TAOHABLE_DATA;
            byte[] data = null;
            (bool succeeded, HttpContent content) = APIHandler.CallAPI_Static(HelpfulPaths.TAOHABLE_API).Result;
            if (succeeded)
                data = content.ReadAsByteArrayAsync().Result;
            if (File.Exists(filePath)) File.Delete(filePath); 
            using (FileStream fs = File.OpenWrite(filePath))
                fs.Write(data, 0, data.Length); //For some reason Stream.Write isn't implemented
        }
        private bool SetupMapData()
        {
            JToken data;
            string songId;
#if NEW_VERSION
            mode = SelectMode(beatmapDiff.beatmapCharacteristic.serializedName, Leaderboard); 
            hash = beatmap.levelID.Split('_')[2].ToUpper(); // 1.37.0 and above
#else
            mode = SelectMode(beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName, Leaderboard);
            hash = beatmap.level.levelID.Split('_')[2].ToUpper(); // 1.34.2 and below
#endif
            try
            {
                Map theMap = GetMap(hash, mode, Leaderboard).Result;
                if (theMap is null)
                {
                    Plugin.Log.Warn("The map is still not in the loaded cache.");
                    return false;
                }
#if NEW_VERSION
                Dictionary<string, (string, JToken)> hold = theMap.Get(beatmapDiff.difficulty); // 1.37.0 and above
#else
                Dictionary<string, (string, JToken)> hold = theMap.Get(beatmap.difficulty); // 1.34.2 and below
#endif
                if (!hold.TryGetValue(mode, out (string, JToken) holdInfo))
                {
                    Plugin.Log.Warn($"The mode '{mode}' doesn't exist.\nKeys: [{string.Join(", ", hold.Keys)}]");
                    return false;
                }
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
            if (!SetupMapData(data, Leaderboard, out float[] ratings, mods))
                return false;
            if (ratings.Length == 1)
                starRating = ratings[0];
            else
            {
                starRating = ratings[0];
                accRating = ratings[1];
                passRating = ratings[2];
                techRating = ratings[3];
            }
            return true;
        }
        /// <summary>
        /// Given the difficulty <paramref name="data"/>, <paramref name="mods"/>, and <paramref name="leaderboard"/>,
        /// find what the star ratings are with as few API calls as possible.
        /// </summary>
        /// <param name="data">The difficulty data of the map.</param>
        /// <param name="leaderboard">The leaderboard in which the map is being checked on.</param>
        /// <param name="ratings">the ratings of the map. Outputs in the order of star, acc, pass, tech.</param>
        /// <param name="mods">What mods are used in the map (defaults to null for no mods).</param>
        /// <param name="quiet">Whether or not for this function to print out the ratings to read.</param>
        /// <returns>Whether or not the ratings were loaded successfully.</returns>
        public static bool SetupMapData(JToken data, Leaderboards leaderboard, out float[] ratings, GameplayModifiers mods = null, bool quiet = false)
        {
            if (data is null || data.ToString().Length <= 0)
            {
                ratings = null;
                return false;
            }
            //Too lazy to use a switch here, sue me.
            if (leaderboard == Leaderboards.Scoresaber)
            {
                ratings = new float[1];
                ratings[0] = (float)data["starScoreSaber"];
                if (!quiet) Plugin.Log.Info("Stars: " + ratings[0]);
                return ratings[0] > 0;
            }
            if (leaderboard == Leaderboards.Accsaber)
            {
                ratings = new float[1];
                ratings[0] = (float)data["complexityAccSaber"];
                if (!quiet) Plugin.Log.Info("Complexity: " + ratings[0]);
                return ratings[0] > 0;
            }
            float multiplier = GetStarMultiplier(data, mods);
            ratings = new float[4];
            ratings[0] = HelpfulPaths.GetRating(data, PPType.Star, mods.songSpeed);
            ratings[1] = HelpfulPaths.GetRating(data, PPType.Acc, mods.songSpeed) * multiplier;
            ratings[2] = HelpfulPaths.GetRating(data, PPType.Pass, mods.songSpeed) * multiplier;
            ratings[3] = HelpfulPaths.GetRating(data, PPType.Tech, mods.songSpeed) * multiplier;
            string mod = HelpfulMisc.GetModifierShortname(mods.songSpeed).ToUpper();
            if (!quiet) Plugin.Log.Info(mod.Length > 0 ? $"{mod} Stars: {ratings[0]}\n{mod} Acc Rating: {ratings[1]}\n{mod} Pass Rating: {ratings[2]}\n{mod} Tech Rating: {ratings[3]}" : $"Stars: {ratings[0]}\nAcc Rating: {ratings[1]}\nPass Rating: {ratings[2]}\nTech Rating: {ratings[3]}");
            return ratings[3] > 0;
        }
        private float GetStarMultiplier(JToken data) => GetStarMultiplier(data, mods);
        public static float GetStarMultiplier(JToken data, GameplayModifiers mods)
        {
            if (!Calculator.GetCalc(usingDefaultLeaderboard).UsesModifiers) return 1.0f;
            float outp = 1.0f;
            if (mods.ghostNotes) outp += HelpfulPaths.GetMultiAmount(data, "gn");
            if (mods.noArrows) outp += HelpfulPaths.GetMultiAmount(data, "na");
            if (mods.enabledObstacleType == EnabledObstacleType.NoObstacles) outp += HelpfulPaths.GetMultiAmount(data, "no");
            if (mods.noBombs) outp += HelpfulPaths.GetMultiAmount(data, "nb");
            return outp;
        }
#endregion
        #region Updates
        public static void UpdateText(bool displayFc, TMP_Text display, float[] ppVals, int mistakes)
        {
            int num = Calculator.GetCalc(usingDefaultLeaderboard).DisplayRatingCount; //4 comes from the maximum amount of ratings of currently supported leaderboards
            if (pc.SplitPPVals && num > 1) {
                string outp = "";
                for (int i = 0; i < 4; i++) 
                    outp += displayFormatter.Invoke(displayFc, pc.ExtraInfo && i == 3, ppVals[i], ppVals[i + num], mistakes, CurrentLabels[i]) + "\n";
                display.text = outp;
            } else
                display.text = displayFormatter.Invoke(displayFc, pc.ExtraInfo, ppVals[num - 1], ppVals[num * 2 - 1], mistakes, CurrentLabels.Last());
        }
        public static string GetUpdateText(bool displayFc, float[] ppVals, int mistakes, string[] labels = null)
        {
            if (labels is null) labels = Labels.ToArray();
            int num = Calculator.GetCalc(usingDefaultLeaderboard).DisplayRatingCount; //4 comes from the maximum amount of ratings of currently supported leaderboards
            if (pc.SplitPPVals && num > 1)
            {
                string outp = "";
                for (int i = 0; i < 4; i++)
                    outp += displayFormatter.Invoke(displayFc, pc.ExtraInfo && i == 3, ppVals[i], ppVals[i + num], mistakes, CurrentLabels[i]) + "\n";
                return outp;
            }
            return displayFormatter.Invoke(displayFc, pc.ExtraInfo, ppVals[num - 1], ppVals[num * 2 - 1], mistakes, CurrentLabels.Last());
        }
        #endregion
    }
}