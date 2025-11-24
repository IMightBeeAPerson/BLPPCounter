using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Utils;
using BLPPCounter.Utils.Misc_Classes;
using System.Threading;
using TMPro;

namespace BLPPCounter.Counters
{
    public abstract class MyCounters
    {
        protected TMP_Text display;
        protected Calculator calc;
        protected RatingContainer ratings;
        protected PPHandler ppHandler;

        public MyCounters(TMP_Text display, MapSelection map, CancellationToken ct) //this is the constructor that needs to be overritten
        {
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler = null;
            SetupData(map, ct);
            if (ppHandler is null)
                Plugin.Log.Critical("PPHandler is null in counter constructor!");
        }
        public virtual void ReinitCounter(TMP_Text display)
        {//same difficulty, modifier, and map
            this.display = display;
            ppHandler.Reset();
        }
        public virtual void ReinitCounter(TMP_Text display, RatingContainer ratingVals)
        {//same map, different modifiers
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = ratingVals;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler?.SetRatings(ratings);
            ppHandler.Reset();
        } 
        public virtual void ReinitCounter(TMP_Text display, MapSelection map)
        {//same map, different difficulty/mode
            this.display = display;
            calc = Calculator.GetSelectedCalc();
            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;
            ppHandler?.SetRatings(ratings);
            // Provide a default CancellationToken for callers that don't have one
            SetupData(map, CancellationToken.None);
        }
        // Backwards-compatible overload used by existing callers that don't provide a CancellationToken
        public virtual void SetupData(MapSelection map)
        {
            SetupData(map, CancellationToken.None);
        }
        public abstract void SetupData(MapSelection map, CancellationToken ct);
        public abstract void UpdateFormat();
        public abstract void UpdateCounter(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote);
        public abstract void SoftUpdate(float acc, int notes, int mistakes, float fcPercent, NoteData currentNote);
        public abstract string Name { get; }

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
