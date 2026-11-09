using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BLPPCounter.Utils.Safety
{
    /// <summary>
    /// Queues operations to run on Unity's main thread.
    /// </summary>
    internal sealed class MainThreadDispatcher : MonoBehaviour, IMainThreadDispatcher
    {
        private readonly ConcurrentQueue<DispatchItem> _queue = new();
        private readonly object _lifetimeLock = new();

        private bool _isDestroyed;

        public bool IsMainThread => IPA.Utilities.UnityGame.OnMainThread;

        private void Update()
        {
            AssertOnMainThread();

            while (_queue.TryDequeue(out DispatchItem? item))
            {
                try
                {
                    item.Execute();
                }
                catch (Exception exception)
                {
                    item.Reject(exception);

                    Plugin.Log.Error($"A main-thread operation failed:\n{exception}");
                }
            }
        }

        private void OnDestroy()
        {
            List<DispatchItem> rejectedItems = [];

            lock (_lifetimeLock)
            {
                if (_isDestroyed)
                    return;

                _isDestroyed = true;

                while (_queue.TryDequeue(out DispatchItem? item))
                    rejectedItems.Add(item);
            }

            ObjectDisposedException exception = new(nameof(MainThreadDispatcher));

            foreach (DispatchItem item in rejectedItems)
                item.Reject(exception);
        }

        public void EnqueueAction(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            Enqueue(
                new DispatchItem(
                    action,
                    exception => Plugin.Log.Error($"A queued main-thread operation was rejected:\n{exception}")
                )
            );
        }

        public void EnqueueRoutine(IEnumerator routine)
        {
            if (routine is null)
                throw new ArgumentNullException(nameof(routine));

            EnqueueAction(() => StartCoroutine(routine));
        }

        public void EnqueueStopRoutine(IEnumerator routine)
        {
            if (routine is null)
                throw new ArgumentNullException(nameof(routine));

            EnqueueAction(() => StopCoroutine(routine));
        }

        public void EnqueueStopRoutine(Coroutine routine)
        {
            if (routine is null)
                throw new ArgumentNullException(nameof(routine));

            EnqueueAction(() => StopCoroutine(routine));
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            if (IsMainThread)
            {
                action();
                return Task.CompletedTask;
            }

            TaskCompletionSource<object?> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration registration = default;

            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
            }

            DispatchItem item = new(
                () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            completionSource.TrySetCanceled(cancellationToken);
                            return;
                        }

                        action();
                        completionSource.TrySetResult(null);
                    }
                    finally
                    {
                        registration.Dispose();
                    }
                },
                exception =>
                {
                    registration.Dispose();
                    completionSource.TrySetException(exception);
                }
            );

            try
            {
                Enqueue(item);
            }
            catch (Exception exception)
            {
                registration.Dispose();
                completionSource.TrySetException(exception);
            }

            return completionSource.Task;
        }

        public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<T>(cancellationToken);

            if (IsMainThread)
                return Task.FromResult(action());

            TaskCompletionSource<T> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration registration = default;

            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
            }

            DispatchItem item = new(
                () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            completionSource.TrySetCanceled(cancellationToken);
                            return;
                        }

                        completionSource.TrySetResult(action());
                    }
                    finally
                    {
                        registration.Dispose();
                    }
                },
                exception =>
                {
                    registration.Dispose();
                    completionSource.TrySetException(exception);
                }
            );

            try
            {
                Enqueue(item);
            }
            catch (Exception exception)
            {
                registration.Dispose();
                completionSource.TrySetException(exception);
            }

            return completionSource.Task;
        }

        public static void AssertOnMainThread([CallerMemberName] string callerName = "")
        {
            if (IPA.Utilities.UnityGame.OnMainThread)
                return;

            string message = $"{callerName} must run on Unity's main thread.";

            Plugin.Log.Critical(message);
            throw new InvalidOperationException(message);
        }

        private void Enqueue(DispatchItem item)
        {
            lock (_lifetimeLock)
            {
                if (_isDestroyed)
                {
                    throw new ObjectDisposedException(nameof(MainThreadDispatcher));
                }

                _queue.Enqueue(item);
            }
        }

        private sealed class DispatchItem
        {
            private readonly Action _action;
            private readonly Action<Exception> _rejectionHandler;

            internal DispatchItem(Action action, Action<Exception> rejectionHandler)
            {
                _action = action ?? throw new ArgumentNullException(nameof(action));

                _rejectionHandler = rejectionHandler ?? throw new ArgumentNullException(nameof(rejectionHandler));
            }

            internal void Execute()
            {
                _action();
            }

            internal void Reject(Exception exception)
            {
                try
                {
                    _rejectionHandler(exception);
                }
                catch (Exception rejectionException)
                {
                    Plugin.Log.Error("A main-thread dispatch rejection handler failed:\n" + rejectionException);
                }
            }
        }
    }
}
