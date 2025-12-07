using BLPPCounter.CalculatorStuffs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BLPPCounter.Utils.Containers
{
    public class PPHandler : IEnumerable<float>
    {
        public delegate void FCUpdateDelegate(RatingContainer ratings, float acc, in PPContainer main, ref PPContainer toChange);
        public delegate void ChooseActionDelegate(int index, float acc, in PPContainer main, ref PPContainer toChange);

        private RatingContainer ratings;
        private readonly Calculator calc;
        /// <summary>
        /// Array of functions to calculate other PP values. The parameters are: ratings, accuracy, main PPContainer, target PPContainer (this one should be modified).
        /// </summary>
        private readonly FCUpdateDelegate[] calcOtherPPs;
        private readonly int precision;
        private int mistakes;
        private bool isFcing;
        private PPContainer[] ppVals;

        public bool UpdateFCEnabled;
        public bool UpdatePPEnabled;
        public bool DisplayFC => !isFcing;

        public event Action<int> UpdateMistakes;
        public event Action<float, PPContainer[], ChooseActionDelegate> UpdateFC;

        public PPHandler(RatingContainer ratings, Calculator calc, int precision = -1, int extraPPVals = 0, params FCUpdateDelegate[] calcOtherPPs)
        {
            if (calcOtherPPs is null) throw new ArgumentNullException(nameof(calcOtherPPs));

            this.ratings = ratings;
            this.calc = calc;
            this.precision = precision;
            this.calcOtherPPs = calcOtherPPs;

            mistakes = 0;
            isFcing = true;
            UpdateFCEnabled = true;
            UpdatePPEnabled = true;

            ppVals = new PPContainer[calcOtherPPs.Length + 1 + extraPPVals];
            for (int i = 0; i < ppVals.Length; i++)
                ppVals[i] = new PPContainer(calc.DisplayRatingCount, 0f, precision: precision);
        }

        private void UseAction(int index, float acc, in PPContainer main, ref PPContainer toChange)
        {
            calcOtherPPs[index](ratings, acc, in main, ref toChange);
        }
        public void Update(float acc, int mistakes, float fcAcc = 0f)
        {
            if (mistakes != this.mistakes)
            {
                this.mistakes = mistakes;
                if (isFcing && UpdateFCEnabled) isFcing = false;
                UpdateMistakes?.Invoke(mistakes);
            }

            if (!isFcing) 
                UpdateFC?.Invoke(fcAcc, ppVals, UseAction);

            if (UpdatePPEnabled)
            {
                ppVals[0].SetValues(calc.GetPpWithSummedPp(acc, precision, ratings));

                for (int i = 0; i < calcOtherPPs.Length; i++)
                    calcOtherPPs[i](ratings, acc, in ppVals[0], ref ppVals[i + 1]);
            }
        }
        public void Reset()
        {
            mistakes = 0;
            isFcing = true;
        }
        public void SetRatings(RatingContainer ratings) => this.ratings = ratings;
        public ref PPContainer GetPPGroup(int group) => ref ppVals[group];

        public IEnumerator<float> GetEnumerator() => ppVals.SelectMany(vals => vals.ToEnumerable()).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public float this[int group, int index] => ppVals[group][index];
        public float this[int group] => ppVals[group].TotalPP;
    }
}
