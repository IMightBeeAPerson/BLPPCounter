using TMPro;

namespace BLPPCounter.Counters
{
    internal interface ICounterController
    {
        public void ClearCounter();

        public void CancelCounter();

        public void ReplaceCurrentCounter(string newCounter, TMP_Text counterDisplay);
    }
}
