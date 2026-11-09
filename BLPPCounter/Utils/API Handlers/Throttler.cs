using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.API_Handlers
{
    internal sealed class Throttler : IDisposable
    {
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly TimeSpan _cycleDuration;

        private DateTime _cycleStart;
        private int _callsThisCycle;
        private int _disposed;

        public Throttler(int callsPerCycle, int cycleLength)
        {
            if (callsPerCycle <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(callsPerCycle));
            }

            if (cycleLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cycleLength));
            }

            CallsPerCycle = callsPerCycle;
            CycleLength = cycleLength;

            _cycleDuration = TimeSpan.FromSeconds(cycleLength);
            _cycleStart = DateTime.UtcNow;
        }

        public int CallsPerCycle { get; }

        public int CycleLength { get; }

        public async Task Call(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                ThrowIfDisposed();

                TimeSpan delay;

                await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    DateTime now = DateTime.UtcNow;
                    TimeSpan elapsed = now - _cycleStart;

                    if (elapsed >= _cycleDuration)
                    {
                        _cycleStart = now;
                        _callsThisCycle = 0;
                        elapsed = TimeSpan.Zero;
                    }

                    if (_callsThisCycle < CallsPerCycle)
                    {
                        _callsThisCycle++;
                        return;
                    }

                    delay = _cycleDuration - elapsed;
                }
                finally
                {
                    _stateLock.Release();
                }

                if (delay <= TimeSpan.Zero)
                    continue;

                Plugin.Log.Info($"Throttling API calls for " + $"{(int)delay.TotalMilliseconds}ms.");

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _stateLock.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(Throttler));
            }
        }
    }
}
