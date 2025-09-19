using BLPPCounter.Utils.Misc_Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils
{
    internal class TimeLooper
    {
        private ManualResetEvent waiter;
        private Task task;
        private Action lastTaskAction;
        private CancellationTokenSource cts;
        public int Delay;
        public readonly object Locker;
        public bool IsPaused { get; private set; }
        public TimeLooper(Action task, int msDelay) : this(msDelay)
        {
            GenerateTask(task).GetAwaiter().GetResult();
        }
        public TimeLooper(Func<bool> task, int msDelay) : this(msDelay)
        {
            GenerateTask(task).GetAwaiter().GetResult();
        }
        public TimeLooper(int msDelay)
        {
            Delay = msDelay;
            //Locker = new object();
        }
        public TimeLooper() : this(0) { }

        public async Task GenerateTask(Action task)
        {
            if (!this.task.IsCompleted)
                await End();
            waiter = new ManualResetEvent(false); //set == resume, reset == pause. Starts out in paused state.
            IsPaused = true;
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            lastTaskAction = task;
            this.task = Task.Run(() =>
            {
                while (true)
                {
                    waiter.WaitOne();
                    lock (Locker)
                    {
                        if (ct.IsCancellationRequested) return;
                        task.Invoke();
                    }
                    if (ct.IsCancellationRequested) return; //double break is so that we don't need to wait for Delay before ending nor will it execute the task if ended while paused
                    Thread.Sleep(Delay);
                }
            }, ct);
        }
        public Task GenerateTask(Func<bool> task) =>
            GenerateTask(() =>
            {
                if (task.Invoke()) SetStatus(true);
            });
        private Task GenerateTask() => GenerateTask(lastTaskAction);
        public void SetStatus(bool isPaused)
        {
            if (IsPaused == isPaused) return;
            if (isPaused) waiter.Reset(); else waiter.Set();
            IsPaused = isPaused;
        }
        public async Task Start()
        {
            if (!task.IsCompleted)
                await End();
            await GenerateTask();
        }
        public Task End()
        {
            return Task.Run(async () =>
            {
                cts.Cancel();
                SetStatus(false);
                await task;
                cts.Dispose();
                waiter.Dispose();
            });
            
        }
    }
}
