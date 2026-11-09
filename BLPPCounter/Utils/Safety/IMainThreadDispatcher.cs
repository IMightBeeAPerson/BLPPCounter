using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BLPPCounter.Utils.Safety
{
    internal interface IMainThreadDispatcher
    {
        bool IsMainThread { get; }

        void EnqueueAction(Action action);

        void EnqueueRoutine(IEnumerator routine);

        void EnqueueStopRoutine(IEnumerator routine);

        void EnqueueStopRoutine(Coroutine routine);

        Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

        Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
    }
}
