using System;
using HMUI;
using Zenject;

namespace BLPPCounter.Utils.Safety
{
    /// <summary>
    /// Presents and dismisses flow coordinators on Unity's main thread.
    /// </summary>
    internal abstract class SafeFlowCoordinator : FlowCoordinator
    {
        protected IMainThreadDispatcher Dispatcher { get; private set; } = null!;

        [Inject]
        private void Construct(IMainThreadDispatcher dispatcher)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        protected abstract FlowCoordinator ParentFlowCoordinator { get; set; }

        public virtual void PresentFlowCoordinator(Action? callback = null, bool immediately = false)
        {
            void Present()
            {
                ParentFlowCoordinator.PresentFlowCoordinator(this, callback, immediately: immediately);
            }

            if (Dispatcher.IsMainThread)
            {
                Present();
                return;
            }

            Dispatcher.EnqueueAction(Present);
        }

        public virtual void DismissFlowCoordinator(Action? callback = null, bool immediately = false)
        {
            void Dismiss()
            {
                ParentFlowCoordinator.DismissFlowCoordinator(
                    this,
                    finishedCallback: callback,
                    immediately: immediately
                );
            }

            if (Dispatcher.IsMainThread)
            {
                Dismiss();
                return;
            }

            Dispatcher.EnqueueAction(Dismiss);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            DismissFlowCoordinator();
        }
    }
}
