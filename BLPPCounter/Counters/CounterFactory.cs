using System;
using System.Threading;
using BLPPCounter.Utils.Enums;
using BLPPCounter.Utils.Map_Utils;
using TMPro;
using Zenject;

namespace BLPPCounter.Counters
{
    internal sealed class CounterFactory : ICounterFactory
    {
        private readonly ICounterCatalog _catalog;
        private readonly IInstantiator _instantiator;

        public CounterFactory(ICounterCatalog catalog, IInstantiator instantiator)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            _instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
        }

        public MyCounters? Create(
            string displayName,
            TMP_Text display,
            MapSelection map,
            Leaderboards leaderboard,
            CancellationToken cancellationToken = default
        )
        {
            if (display is null)
            {
                throw new ArgumentNullException(nameof(display));
            }

            CounterDescriptor descriptor = _catalog.Get(displayName);

            if (!descriptor.Supports(leaderboard))
            {
                Plugin.Log.Warn($"Counter \"{displayName}\" does not " + $"support {leaderboard}.");

                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                MyCounters counter = (MyCounters)_instantiator.Instantiate(descriptor.CounterType);

                counter.Initialize(display, map, leaderboard, cancellationToken);

                counter.UpdateFormat();

                return counter;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Plugin.Log.Warn($"Creation of counter \"{displayName}\" " + "was cancelled.");

                return null;
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"Could not create counter " + $"\"{displayName}\":\n{exception}");

                return null;
            }
        }
    }
}
