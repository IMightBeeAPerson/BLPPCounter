using TMPro;

namespace PleaseWork.Counters
{
    internal interface IMyCounters
    {
        void SetupData(string id, string hash, string diff, string mode, string mapData);
        void ReinitCounter(TMP_Text display); //same difficulty, modifier, and map
        void ReinitCounter(TMP_Text display, float passRating, float accRating, float techRating); //same map, different modifiers
        void ReinitCounter(TMP_Text display, string hash, string diff, string mode, string mapData); //same map, different difficulty/mode
        void UpdateCounter(float acc, int notes, int badNotes, int fcScore);

        string Name { get; }
    }
}
