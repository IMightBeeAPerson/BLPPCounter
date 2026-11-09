using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using BLPPCounter.CalculatorStuffs;
using BLPPCounter.Helpfuls.FormatHelpers;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Containers;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using TMPro;
using Zenject;

namespace BLPPCounter.Counters
{
    public abstract class MyCounters
    {
        private ICalculatorProvider _calculatorProvider = null!;

        private ICounterLabelProvider _labelProvider = null!;

        private TMP_Text _display = null!;
        private bool _initialized;

        protected Calculator calc = null!;
        private protected PluginConfig pc = null!;
        protected RatingContainer ratings;
        protected PPHandler ppHandler = null!;
        protected StringBuilder outpText = new();
        private protected IMainCounterFormattingService MainFormatting = null!;

        public abstract string Name { get; }

        protected TMP_Text Display => _display;

        protected Leaderboards CurrentLeaderboard { get; private set; }

        protected IReadOnlyList<string> CurrentLabels => _labelProvider.GetLabels(calc, CurrentLeaderboard);

        [Inject]
        private void Construct(
            PluginConfig config,
            ICalculatorProvider calculatorProvider,
            ICounterLabelProvider labelProvider,
            IMainCounterFormattingService mainFormatting
        )
        {
            pc = config ?? throw new ArgumentNullException(nameof(config));

            _calculatorProvider = calculatorProvider ?? throw new ArgumentNullException(nameof(calculatorProvider));

            _labelProvider = labelProvider ?? throw new ArgumentNullException(nameof(labelProvider));

            MainFormatting = mainFormatting ?? throw new ArgumentNullException(nameof(mainFormatting));
        }

        internal void Initialize(
            TMP_Text display,
            MapSelection map,
            Leaderboards leaderboard,
            CancellationToken cancellationToken
        )
        {
            if (_initialized)
            {
                throw new InvalidOperationException($"{GetType().Name} has already been initialized.");
            }

            _display = display ?? throw new ArgumentNullException(nameof(display));

            CurrentLeaderboard = leaderboard;

            calc = _calculatorProvider.Get(leaderboard);

            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;

            outpText = new StringBuilder();

            cancellationToken.ThrowIfCancellationRequested();

            SetupData(map, cancellationToken);

            if (ppHandler is null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.{nameof(SetupData)} " + $"did not initialize {nameof(ppHandler)}."
                );
            }

            _initialized = true;
        }

        public virtual void ReinitCounter() { }

        public void ReinitCounter(TMP_Text display)
        {
            EnsureInitialized();

            _display = display ?? throw new ArgumentNullException(nameof(display));

            ppHandler.Reset();
            ReinitCounter();
        }

        public virtual void ReinitCounter(RatingContainer ratingValues) { }

        public void ReinitCounter(TMP_Text display, RatingContainer ratingValues)
        {
            EnsureInitialized();

            _display = display ?? throw new ArgumentNullException(nameof(display));

            calc = _calculatorProvider.Get(CurrentLeaderboard);

            ratings = ratingValues;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;

            ppHandler.SetRatings(ratings);
            ppHandler.Reset();

            ReinitCounter(ratingValues);
        }

        public void ReinitCounter(TMP_Text display, MapSelection map)
        {
            EnsureInitialized();

            _display = display ?? throw new ArgumentNullException(nameof(display));

            calc = _calculatorProvider.Get(CurrentLeaderboard);

            ratings = map.Ratings;
            ratings.SetSelectedRatings();
            calc.Ratings = ratings;

            ppHandler.SetRatings(ratings);

            SetupData(map, CancellationToken.None);

            ReinitCounter(map);
        }

        public virtual void ReinitCounter(MapSelection map) { }

        public void SetupData(MapSelection map)
        {
            SetupData(map, CancellationToken.None);
        }

        public abstract void SetupData(MapSelection map, CancellationToken cancellationToken);

        public abstract void UpdateFormat();

        public void UpdateCounter(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        )
        {
            EnsureInitialized();

            outpText.Clear();

            UpdateCounterInternal(accuracy, notes, mistakes, fullComboAccuracy, currentNote);

            _display.text = outpText.ToString();
        }

        public abstract void UpdateCounterInternal(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        );

        public abstract void SoftUpdate(
            float accuracy,
            int notes,
            int mistakes,
            float fullComboAccuracy,
            NoteData? currentNote
        );

        public virtual void CounterComplete(
            float finalAccuracy,
            float finalFullComboAccuracy,
            int totalNotes,
            int totalMistakes
        ) { }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} has not been " + "initialized by the counter factory."
                );
            }
        }
    }
}
