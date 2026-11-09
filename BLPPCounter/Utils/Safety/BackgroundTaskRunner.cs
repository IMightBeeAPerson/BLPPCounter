using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Safety
{
    internal sealed class BackgroundTaskRunner : IBackgroundTaskRunner, IDisposable
    {
        private readonly CancellationTokenSource _shutdownSource = new();

        private int _disposed;

        public void Forget(Task task, string operationName)
        {
            if (task is null)
                throw new ArgumentNullException(nameof(task));

            if (BetterString.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("An operation name is required.", nameof(operationName));
            }

            ObserveAsync(task, operationName);
        }

        public void Run(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken = default
        )
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            if (BetterString.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("An operation name is required.", nameof(operationName));
            }

            ThrowIfDisposed();

            RunInternalAsync(operation, operationName, cancellationToken);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _shutdownSource.Cancel();
            _shutdownSource.Dispose();
        }

        private async void ObserveAsync(Task task, string operationName)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Plugin.Log.Debug($"{operationName} was cancelled.");
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"{operationName} failed:\n{exception}");
            }
        }

        private async void RunInternalAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken
        )
        {
            using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                _shutdownSource.Token,
                cancellationToken
            );

            try
            {
                await operation(linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
            {
                Plugin.Log.Debug($"{operationName} was cancelled.");
            }
            catch (Exception exception)
            {
                Plugin.Log.Error($"{operationName} failed:\n{exception}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(BackgroundTaskRunner));
            }
        }
    }
}
