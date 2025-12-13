using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Map_Utils;
using System.Text;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public abstract class MyCounters
    {
        private TMP_Text display;
        protected Calculator calc;
        protected RatingContainer ratings;
        protected PPHandler ppHandler;
        protected StringBuilder outpText;

        public abstract string Name { get; }
        protected TMP_Text Display { get => display; }

        public MyCounters(TMP_Text display, MapSelection map, CancellationToken ct) //this is the constructor that needs to be overritten
        {
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler = null;
            outpText = new();
            SetupData(map, ct);
            if (ppHandler is null)
                Plugin.Log.Critical("PPHandler is null in counter constructor!");
        }
        public virtual void ReinitCounter() { }
        public void ReinitCounter(TMP_Text display)
        {//same difficulty, modifier, and map
            this.display = display;
            ppHandler.Reset();
            ReinitCounter();
        }
        public virtual void ReinitCounter(RatingContainer ratingVals) { }
        public void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {//same map, different modifiers
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = ratingVals;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler?.SetRatings(ratings);
            ppHandler.Reset();
            ReinitCounter(ratingVals);
        } 
        public void ReinitCounter(TMP_Text display, MapSelection map)
        {//same map, different difficulty/mode
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler?.SetRatings(ratings);
            // Provide a default CancellationToken for callers that don't have one
            SetupData(map, CancellationToken.None);
            ReinitCounter(map);
        }
        public virtual void ReinitCounter(MapSelection map) { }
        // Backwards-compatible overload used by existing callers that don't provide a CancellationToken
        public virtual void SetupData(MapSelection map)
        {
            SetupData(map, CancellationToken.None);
        }
        public abstract void SetupData(MapSelection map, CancellationToken ct);
        public abstract void UpdateFormat();
        public void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote)
        {
            outpText.Clear();
            UpdateCounterInternal(acc, notes, mistakes, fcPercent, currentNote);
            display.text = outpText.ToString();
        }
        public abstract void UpdateCounterInternal(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote);
        public abstract void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote);

        /*Static functions that must be created:
         public static bool InitFormat() { }
         */

        /*Static Fields that must be created:
         public static int OrderNumber;
         public static string DisplayName;
         public static string DisplayHandler;
         public static Leaderboards ValidLeaderboards;
         */
    }
}
