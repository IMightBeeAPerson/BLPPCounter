using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using HarmonyLib;
using Zenject;

namespace BLPPCounter.Utils.Safety
{
    internal sealed class PluginLifecycle : IInitializable, IDisposable
    {
        private const string HarmonyId = "Person.BLPPCounter";

        private readonly PluginActivationState _activationState;

        private readonly PluginConfig _config;
        private readonly IProfileRepository _profileRepository;

        private readonly ITargeter _targeter;
        private readonly ILocalReplayService _localReplayService;

        private readonly IMainThreadDispatcher _dispatcher;

        private readonly IBackgroundTaskRunner _taskRunner;

        private readonly AsyncLock _transitionLock = new();

        private readonly CancellationTokenSource _lifetimeSource = new();

        private readonly object _stateLock = new();

        private Harmony? _harmony;
        private CancellationTokenSource? _activationSource;

        private bool _active;
        private int _disposed;

        public PluginLifecycle(
            PluginActivationState activationState,
            PluginConfig config,
            IProfileRepository profileRepository,
            ITargeter targeter,
            ILocalReplayService localReplayService,
            IMainThreadDispatcher dispatcher,
            IBackgroundTaskRunner taskRunner
        )
        {
            _activationState = activationState ?? throw new ArgumentNullException(nameof(activationState));

            _config = config ?? throw new ArgumentNullException(nameof(config));

            _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));

            _targeter = targeter ?? throw new ArgumentNullException(nameof(targeter));

            _localReplayService = localReplayService ?? throw new ArgumentNullException(nameof(localReplayService));

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _taskRunner = taskRunner ?? throw new ArgumentNullException(nameof(taskRunner));
        }

        public void Initialize()
        {
            _activationState.Changed += HandleActivationChanged;

            if (_activationState.IsEnabled)
                HandleActivationChanged(true);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _activationState.Changed -= HandleActivationChanged;

            CancellationTokenSource? activationSource;

            lock (_stateLock)
            {
                activationSource = _activationSource;

                _activationSource = null;
                _active = false;
            }

            activationSource?.Cancel();
            activationSource?.Dispose();

            _lifetimeSource.Cancel();
            _lifetimeSource.Dispose();
            _transitionLock.Dispose();

            _harmony?.UnpatchSelf();
            _harmony = null;
        }

        private void HandleActivationChanged(bool enabled)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (enabled)
            {
                CancellationTokenSource source;

                lock (_stateLock)
                {
                    _activationSource?.Cancel();
                    _activationSource?.Dispose();

                    _activationSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeSource.Token);

                    source = _activationSource;
                }

                _taskRunner.Run(
                    cancellationToken => ActivateAsync(cancellationToken),
                    "Activate BL PP Counter",
                    source.Token
                );

                return;
            }

            CancellationTokenSource? oldSource;

            lock (_stateLock)
            {
                oldSource = _activationSource;
                _activationSource = null;
            }

            oldSource?.Cancel();
            oldSource?.Dispose();

            _taskRunner.Run(DeactivateAsync, "Deactivate BL PP Counter", _lifetimeSource.Token);
        }

        private async Task ActivateAsync(CancellationToken cancellationToken)
        {
            using AsyncLock.Releaser releaser = await _transitionLock
                .LockAsync(cancellationToken)
                .ConfigureAwait(false);

            lock (_stateLock)
            {
                if (_active)
                    return;
            }

            await _dispatcher.InvokeAsync(ApplyHarmonyPatches, cancellationToken).ConfigureAwait(false);

            await _profileRepository.LoadAsync(cancellationToken).ConfigureAwait(false);

            await _targeter.WaitUntilInitializedAsync(cancellationToken).ConfigureAwait(false);

            if (_config.LocalReplays)
            {
                await _localReplayService.LoadAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_stateLock)
                _active = true;
        }

        private async Task DeactivateAsync(CancellationToken cancellationToken)
        {
            using AsyncLock.Releaser releaser = await _transitionLock
                .LockAsync(cancellationToken)
                .ConfigureAwait(false);

            bool wasActive;

            lock (_stateLock)
            {
                wasActive = _active;
                _active = false;
            }

            if (!wasActive)
            {
                await _dispatcher.InvokeAsync(RemoveHarmonyPatches, cancellationToken).ConfigureAwait(false);

                return;
            }

            Task saveProfiles = _profileRepository.SaveAsync(cancellationToken);

            Task saveTargets = _targeter.SaveAllAsync(cancellationToken);

            await Task.WhenAll(saveProfiles, saveTargets).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(RemoveHarmonyPatches, cancellationToken).ConfigureAwait(false);
        }

        private void ApplyHarmonyPatches()
        {
            MainThreadDispatcher.AssertOnMainThread();

            if (_harmony is not null)
                return;

            _harmony = new Harmony(HarmonyId);

            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private void RemoveHarmonyPatches()
        {
            MainThreadDispatcher.AssertOnMainThread();

            _harmony?.UnpatchSelf();
            _harmony = null;
        }
    }
}
