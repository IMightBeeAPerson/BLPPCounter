using System;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.Settings;
using BLPPCounter.Helpfuls;
using BLPPCounter.Patches;
using BLPPCounter.Settings.SettingHandlers;
using BLPPCounter.Settings.SettingHandlers.MenuSettingHandlers;
using BLPPCounter.Settings.SettingHandlers.MenuViews;
using BLPPCounter.Utils.Misc_Classes;
using Zenject;

namespace BLPPCounter.Utils.Safety
{
    internal sealed class MenuLifecycle : IInitializable, IDisposable
    {
        private readonly PluginActivationState _activationState;

        private readonly MenuSettingsHandler _menuSettingsHandler;

        private readonly PpInfoTabHandler _ppInfoTabHandler;

        private readonly SettingsHandler _settingsHandler;

        private readonly SimpleSettingsHandler _simpleSettingsHandler;

        private readonly ITargeter _targeter;
        private readonly IMainThreadDispatcher _dispatcher;

        private readonly IBackgroundTaskRunner _taskRunner;

        private readonly CancellationTokenSource _lifetimeSource = new();

        private bool _activationSubscribed;
        private bool _menuEventSubscribed;
        private bool _menusRegistered;
        private int _disposed;

        public MenuLifecycle(
            PluginActivationState activationState,
            MenuSettingsHandler menuSettingsHandler,
            PpInfoTabHandler ppInfoTabHandler,
            SettingsHandler settingsHandler,
            SimpleSettingsHandler simpleSettingsHandler,
            ITargeter targeter,
            IMainThreadDispatcher dispatcher,
            IBackgroundTaskRunner taskRunner
        )
        {
            _activationState = activationState ?? throw new ArgumentNullException(nameof(activationState));

            _menuSettingsHandler = menuSettingsHandler ?? throw new ArgumentNullException(nameof(menuSettingsHandler));

            _ppInfoTabHandler = ppInfoTabHandler ?? throw new ArgumentNullException(nameof(ppInfoTabHandler));

            _settingsHandler = settingsHandler ?? throw new ArgumentNullException(nameof(settingsHandler));

            _simpleSettingsHandler =
                simpleSettingsHandler ?? throw new ArgumentNullException(nameof(simpleSettingsHandler));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
        }

        public void Initialize()
        {
            _activationState.Changed += HandleActivationChanged;

            _activationSubscribed = true;

            if (_activationState.IsEnabled)
                HandleActivationChanged(true);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (_activationSubscribed)
            {
                _activationState.Changed -= HandleActivationChanged;
            }

            UnsubscribeFromMenuEvent();

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();

            RemoveMenus();
        }

        private void HandleActivationChanged(bool enabled)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (enabled)
            {
                TabSelectionPatch.AddTabName(PpInfoTabHandler.TabName);

                SubscribeToMenuEvent();
                return;
            }

            UnsubscribeFromMenuEvent();

            _taskRunner.Run(RemoveMenusAsync, "Remove BL PP Counter menus", _lifetimeSource.Token);
        }

        private void SubscribeToMenuEvent()
        {
            if (_menuEventSubscribed)
                return;

#if NEW_VERSION
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += HandleMainMenuInitializing;
#else
            BS_Utils.Utilities.BSEvents.menuSceneActive += HandleMainMenuInitializing;
#endif

            _menuEventSubscribed = true;
        }

        private void UnsubscribeFromMenuEvent()
        {
            if (!_menuEventSubscribed)
                return;

#if NEW_VERSION
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing -= HandleMainMenuInitializing;
#else
            BS_Utils.Utilities.BSEvents.menuSceneActive -= HandleMainMenuInitializing;
#endif

            _menuEventSubscribed = false;
        }

        private void HandleMainMenuInitializing()
        {
            _taskRunner.Run(RegisterMenusAsync, "Register BL PP Counter menus", _lifetimeSource.Token);
        }

        private async Task RegisterMenusAsync(CancellationToken cancellationToken)
        {
            await _dispatcher.InvokeAsync(RegisterMenus, cancellationToken).ConfigureAwait(false);

            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(_settingsHandler.UpdateTargetLists, cancellationToken).ConfigureAwait(false);
        }

        private void RegisterMenus()
        {
            MainThreadDispatcher.AssertOnMainThread();

            TabSelectionPatch.ClearData();
            TabSelectionPatch.AddStartPatch();

            if (!_menusRegistered)
            {
#if NEW_VERSION
                BSMLSettings.Instance.AddSettingsMenu(
                    "BL PP Counter",
                    HelpfulPaths.SETTINGS_BSML,
                    _menuSettingsHandler
                );

                GameplaySetup.Instance.AddTab(PpInfoTabHandler.TabName, HelpfulPaths.PP_CALC_BSML, _ppInfoTabHandler);
#else
                BSMLSettings.instance.AddSettingsMenu(
                    "BL PP Counter",
                    HelpfulPaths.SETTINGS_BSML,
                    _menuSettingsHandler
                );

                GameplaySetup.instance.AddTab(PpInfoTabHandler.TabName, HelpfulPaths.PP_CALC_BSML, _ppInfoTabHandler);
#endif

                _menusRegistered = true;
            }

            _simpleSettingsHandler.ChangeMenuTab(removeTab: false);
        }

        private Task RemoveMenusAsync(CancellationToken cancellationToken)
        {
            return _dispatcher.InvokeAsync(RemoveMenus, cancellationToken);
        }

        private void RemoveMenus()
        {
            if (!_menusRegistered)
                return;

            if (!IPA.Utilities.UnityGame.OnMainThread)
            {
                try
                {
                    _dispatcher.EnqueueAction(RemoveMenus);
                }
                catch (ObjectDisposedException)
                {
                    // Unity/menu context is already shutting down.
                }

                return;
            }

#if NEW_VERSION
            GameplaySetup.Instance.RemoveTab("BL PP Counter");

            GameplaySetup.Instance.RemoveTab(PpInfoTabHandler.TabName);

            BSMLSettings.Instance.RemoveSettingsMenu(_menuSettingsHandler);
#else
            GameplaySetup.instance.RemoveTab("BL PP Counter");

            GameplaySetup.instance.RemoveTab(PpInfoTabHandler.TabName);

            BSMLSettings.instance.RemoveSettingsMenu(_menuSettingsHandler);
#endif

            TabSelectionPatch.Reset();
            _menusRegistered = false;
        }
    }
}
