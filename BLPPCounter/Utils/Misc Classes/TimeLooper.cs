using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Misc_Classes
{
    internal class TimeLooper
    {
#pragma warning disable CS4014 // Start calls inside of constructors do not need to be awaited
        private CancellationTokenSource cts = new();
        private Task loopTask = Task.CompletedTask;
        private Action mainAction;

        private TaskCompletionSource<bool> pauseTcs;

        public bool IsPaused { get; set; }
        public int DelayMs { get; set; }

        public TimeLooper() { }

        public TimeLooper(Action action, int delayMs)
        {
            SetAction(action);
            DelayMs = delayMs;
            Start();
        }

        public TimeLooper(Func<bool> func, int delayMs)
        {
            SetAction(func);
            DelayMs = delayMs;
            Start();
        }

        public void SetAction(Action action)
        {
            mainAction = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void SetAction(Func<bool> func)
        {
            if (func is null)
                throw new ArgumentNullException(nameof(func));
            mainAction = () =>
            {
                if (func())
                    PauseAsync();
                else
                    Resume();
            };
        }

        // ───────────────────────────────────────────────────────────
        // Start / Stop
        // ───────────────────────────────────────────────────────────
        public async Task Start()
        {
            await End();
            cts = new CancellationTokenSource();
            loopTask = Task.Run(async () => await LoopAsync(cts.Token), cts.Token);
        }

        public async Task End()
        {
            if (loopTask.IsCompleted)
                return;

            try
            {
                cts.Cancel();
                await loopTask;
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            finally
            {
                cts.Dispose();
            }
        }

        // ───────────────────────────────────────────────────────────
        // Pause / Resume
        // ───────────────────────────────────────────────────────────

        /// <summary>
        /// Pauses the loop and returns a Task that completes
        /// ONCE the loop acknowledges it is paused.
        /// </summary>
        public Task PauseAsync()
        {
            if (IsPaused)
                return Task.CompletedTask;

            IsPaused = true;
            pauseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            return pauseTcs.Task;
        }

        /// <summary>
        /// Synchronous pause (if you can't use async in the caller).
        /// </summary>
        public void Pause()
        {
            PauseAsync().GetAwaiter().GetResult();
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            // loop continues automatically
        }

        // ───────────────────────────────────────────────────────────
        // Internal Loop
        // ───────────────────────────────────────────────────────────
        private async Task LoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Handle pause
                    if (IsPaused)
                    {
                        // Signal that the pause is recognized
                        pauseTcs?.TrySetResult(true);

                        // Stay paused until Resume() or cancellation
                        await Task.Delay(10, ct);

                        continue;
                    }

                    // Execute action
                    try
                    {
                        mainAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex);
                    }

                    // Delay before next run
                    await Task.Delay(DelayMs, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on cancellation
            }
        }
    }
}
