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
        public int Delay;
        private bool taskComplete, taskExited;
        public object Locker { get; private set; }
        public bool IsPaused { get; private set; }
        public TimeLooper(Action task, int msDelay)
        {
            Delay = msDelay;
            GenerateTask(task);
        }
        public TimeLooper(Func<bool> task, int msDelay)
        {
            Delay = msDelay;
            GenerateTask(task);
        }
        public TimeLooper(int msDelay)
        {
            Delay = msDelay;
        }
        public TimeLooper() { }

        public void GenerateTask(Action task)
        {
            SetupVars();
            this.task = Task.Run(() =>
            {
                while (true)
                {
                    waiter.WaitOne();
                    if (taskComplete) break;
                    lock (Locker)
                        task.Invoke();
                    if (taskComplete) break; //double break is so that we don't need to wait for Delay before ending nor will it execute the task if ended while paused
                    Thread.Sleep(Delay);
                }
                taskExited = true;
            });
        }
        public void GenerateTask(Func<bool> task)
        {
            SetupVars();
            this.task = Task.Run(() =>
            {
                while (true)
                {
                    waiter.WaitOne();
                    if (taskComplete) break;
                    lock (Locker)
                    {
                        if (task.Invoke()) SetStatus(true);
                    }
                    if (taskComplete) break; //double break is so that we don't need to wait for Delay before ending nor will it execute the task if ended while paused
                    Thread.Sleep(Delay);
                }
                taskExited = true;
            });
        }
        private void SetupVars()
        {
            waiter = new ManualResetEvent(false); //set == resume, reset == pause. Starts out in paused state.
            taskComplete = false;
            IsPaused = true;
            taskExited = false;
            Locker = new object();
        }
        public void SetStatus(bool isPaused)
        {
            if (IsPaused == isPaused) return;
            if (isPaused) waiter.Reset(); else waiter.Set();
            IsPaused = isPaused;
        }
        public void Start()
        {
            if (IsPaused) waiter.Set();
        }
        public Task End()
        {
            taskComplete = true;
            return Task.Run(() =>
            {
                if (IsPaused) waiter.Set();
                while (!taskExited)
                    Thread.Sleep(100); //Checks every 100ms whether or not the task is complete.
                task.Dispose();
            });
            
        }
    }
}
