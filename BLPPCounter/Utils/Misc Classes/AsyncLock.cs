using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal sealed class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private int _disposed;

        public async Task<Releaser> LockAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (Volatile.Read(ref _disposed) != 0)
            {
                _semaphore.Release();

                throw new ObjectDisposedException(nameof(AsyncLock));
            }

            return new Releaser(new Lease(_semaphore));
        }

        public async Task<Releaser?> TryLockAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            bool acquired = await _semaphore.WaitAsync(0, cancellationToken).ConfigureAwait(false);

            if (!acquired)
                return null;

            if (Volatile.Read(ref _disposed) != 0)
            {
                _semaphore.Release();

                throw new ObjectDisposedException(nameof(AsyncLock));
            }

            return new Releaser(new Lease(_semaphore));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _semaphore.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncLock));
            }
        }

        public readonly struct Releaser : IDisposable
        {
            private readonly Lease? _lease;

            internal Releaser(Lease lease)
            {
                _lease = lease;
            }

            public void Dispose()
            {
                _lease?.Release();
            }
        }

        internal sealed class Lease
        {
            private readonly SemaphoreSlim _semaphore;

            private int _released;

            internal Lease(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
            }

            internal void Release()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                    _semaphore.Release();
            }
        }
    }
}
