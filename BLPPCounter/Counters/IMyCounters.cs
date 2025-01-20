using BLPPCounter.Utils;
using System;
using TMPro;

namespace BLPPCounter.Counters
{
    public interface IMyCounters
    {
        void SetupData(MapSelection map);
        void ReinitCounter(TMP_Text display); //same difficulty, modifier, and map
        void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating); //same map, different modifiers
        void ReinitCounter(TMP_Text display, MapSelection map); //same map, different difficulty/mode
        void UpdateFormat();
        void UpdateCounter(float acc, int notes, int mistakes, float fcPrecent);
        string Name { get; }

        /*Constructors that must be created:
         public IMyCounters(TMP_Text display, MapSelection map) { }
         */

        /*Static functions that must be created:
         public static bool InitFormat() { }
         */

        /*Static Fields that must be created:
         public static int OrderNumber;
         public static string DisplayName;
         public static string DisplayHandler;
         */
    }
}
