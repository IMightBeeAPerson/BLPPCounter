using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Zenject;

namespace BLPPCounter.Utils.Safety
{
    /// <summary>
    /// Raises property change notifications on Unity's main thread.
    /// </summary>
    internal abstract class SafeNotifyPropertyChanged : INotifyPropertyChanged
    {
        protected IMainThreadDispatcher Dispatcher { get; private set; } = null!;

        [Inject]
        private void Construct(IMainThreadDispatcher dispatcher)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (Dispatcher.IsMainThread)
            {
                RaisePropertyChanged(propertyName);
                return;
            }

            Dispatcher.EnqueueAction(() => RaisePropertyChanged(propertyName));
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
