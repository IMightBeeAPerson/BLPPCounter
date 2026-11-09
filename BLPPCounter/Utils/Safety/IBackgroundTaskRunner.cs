using System;
using System.Threading;
using System.Threading.Tasks;

namespace BLPPCounter.Utils.Safety
{
    internal interface IBackgroundTaskRunner
    {
        void Forget(Task task, string operationName);

        void Run(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken = default
        );
    }
}
