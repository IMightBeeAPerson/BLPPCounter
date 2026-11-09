using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BeatSaberMarkupLanguage.ViewControllers;
using UnityEngine;
using Zenject;

namespace BLPPCounter.Utils.Safety
{
    /// <summary>
    /// Provides main-thread-safe BSML property notifications.
    /// </summary>
    internal abstract class BSMLSafeAutomaticViewController : BSMLAutomaticViewController
    {
        protected IMainThreadDispatcher Dispatcher { get; private set; } = null!;

        [Inject]
        private void Construct(IMainThreadDispatcher dispatcher)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        protected new void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (Dispatcher.IsMainThread)
            {
                base.NotifyPropertyChanged(propertyName);
                return;
            }

            Dispatcher.EnqueueAction(() => base.NotifyPropertyChanged(propertyName));
        }

        public new Coroutine StartCoroutine(IEnumerator routine)
        {
            if (routine is null)
                throw new ArgumentNullException(nameof(routine));

            MainThreadDispatcher.AssertOnMainThread();
            return base.StartCoroutine(routine);
        }
    }
}
