using System;

namespace BLPPCounter.Utils.Safety
{
    internal sealed class PluginActivationState
    {
        private readonly object _sync = new();

        private bool _isEnabled;

        public event Action<bool>? Changed;

        public bool IsEnabled
        {
            get
            {
                lock (_sync)
                    return _isEnabled;
            }
        }

        public void SetEnabled(bool enabled)
        {
            Action<bool>? handlers;

            lock (_sync)
            {
                if (_isEnabled == enabled)
                    return;

                _isEnabled = enabled;
                handlers = Changed;
            }

            if (handlers is null)
                return;

            foreach (Action<bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(enabled);
                }
                catch (Exception exception)
                {
                    Plugin.Log.Error("A plugin activation subscriber failed:\n" + exception);
                }
            }
        }
    }
}
