using System;
using System.Collections;
using System.Collections.Generic;
using BeatSaberMarkupLanguage.Components;
using BLPPCounter.Helpfuls;
using BLPPCounter.Settings.Configs;
using BLPPCounter.Utils.List_Settings;
using BLPPCounter.Utils.Misc_Classes;
using BLPPCounter.Utils.Profile_Utils;
using BLPPCounter.Utils.Safety;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BLPPCounter.Settings.SettingHandlers
{
    internal sealed class ProfilePanelPresenter : IDisposable
    {
        private const int PlayTableButtonWidths = 60;

        private readonly PluginConfig _config;
        private readonly SessionListInfo.Factory _sessionListInfoFactory;
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly HelpfulFormatter _formatter;
        private readonly Table.Factory _tableFactory;
        private readonly List<object> _sessionRows = [];

        private Profile? _profile;
        private Session? _session;
        private Table? _playTable;

        private TextMeshProUGUI? _playTableText;
        private HorizontalLayoutGroup? _playTableButtons;
        private GameObject? _playTableModal;
        private GameObject? _sessionWindow;
        private TextMeshProUGUI? _sessionPlaysSet;
        private TextMeshProUGUI? _sessionPpGained;
        private CustomCellListTableData? _sessionTable;
        private TextMeshProUGUI? _plusOneLabel;
        private TextMeshProUGUI? _plusOneText;

        private bool _playTableOpen;
        private bool _sessionTableOpen;
        private bool _initialized;
        private bool _disposed;

        public ProfilePanelPresenter(
            PluginConfig config,
            SessionListInfo.Factory sessionListInfoFactory,
            IMainThreadDispatcher dispatcher,
            HelpfulFormatter formatter,
            Table.Factory tableFactory
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _sessionListInfoFactory =
                sessionListInfoFactory ?? throw new ArgumentNullException(nameof(sessionListInfoFactory));

            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));

            _tableFactory = tableFactory ?? throw new ArgumentNullException(nameof(tableFactory));
        }

        public IReadOnlyList<object> SessionRows => _sessionRows;

        public Profile? CurrentProfile => _profile;

        public void Initialize(
            TextMeshProUGUI playTableText,
            HorizontalLayoutGroup playTableButtons,
            GameObject playTableModal,
            GameObject sessionWindow,
            TextMeshProUGUI sessionPlaysSet,
            TextMeshProUGUI sessionPpGained,
            CustomCellListTableData sessionTable,
            TextMeshProUGUI plusOneLabel,
            TextMeshProUGUI plusOneText
        )
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _playTableText = playTableText ?? throw new ArgumentNullException(nameof(playTableText));

            _playTableButtons = playTableButtons ?? throw new ArgumentNullException(nameof(playTableButtons));

            _playTableModal = playTableModal ?? throw new ArgumentNullException(nameof(playTableModal));

            _sessionWindow = sessionWindow ?? throw new ArgumentNullException(nameof(sessionWindow));

            _sessionPlaysSet = sessionPlaysSet ?? throw new ArgumentNullException(nameof(sessionPlaysSet));

            _sessionPpGained = sessionPpGained ?? throw new ArgumentNullException(nameof(sessionPpGained));

            _sessionTable = sessionTable ?? throw new ArgumentNullException(nameof(sessionTable));

            _plusOneLabel = plusOneLabel ?? throw new ArgumentNullException(nameof(plusOneLabel));

            _plusOneText = plusOneText ?? throw new ArgumentNullException(nameof(plusOneText));

            _initialized = true;

            if (_profile is not null)
                Refresh();
        }

        public void Bind(Profile profile)
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (ReferenceEquals(_profile, profile))
            {
                EnsureSessionSubscription();
                Refresh();
                return;
            }

            UnsubscribeFromModel();

            _profile = profile;
            _profile.Changed += HandleModelChanged;

            EnsureSessionSubscription();

            _playTable = null;
            _sessionRows.Clear();

            Refresh();
        }

        public void Refresh()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (!_initialized || _profile is null)
            {
                return;
            }

            EnsureSessionSubscription();

            UpdateProfileSummary();
            UpdatePlayTableValues();
            UpdateSessionRows();

            if (_sessionTableOpen)
                ReloadSessionTable();
        }

        public void UpdateProfilePpPreview(
            float rawPp,
            TextMeshProUGUI weightedLabel,
            TextMeshProUGUI weightedValue,
            TextMeshProUGUI profileLabel,
            TextMeshProUGUI profileValue
        )
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (weightedLabel is null || weightedValue is null || profileLabel is null || profileValue is null)
            {
                throw new ArgumentNullException("Profile preview UI is not initialized.");
            }

            string ppLabel = _profile?.PpLabel.ToLowerInvariant() ?? "pp";

            weightedLabel.SetText($"<color=green>Weighted</color> {ppLabel}");

            profileLabel.SetText($"<color=yellow>Profile</color> {ppLabel}");

            if (_profile is null)
            {
                weightedValue.SetText($"<color=#A72>Unknown</color> {ppLabel}");

                profileValue.SetText($"<color=purple>Unknown</color> {ppLabel}");

                return;
            }

            float weightedPp = _profile.GetWeightedPp(rawPp);

            float profilePp = _profile.GetProfilePpRaw(rawPp);

            weightedValue.SetText(
                weightedPp < 0f
                    ? $"<color=#A72>Unknown</color> {ppLabel}"
                    : $"<color=#A72>{weightedPp}</color> {ppLabel}"
            );

            profileValue.SetText(
                profilePp < 0f
                    ? $"<color=purple>Unknown</color> {ppLabel}"
                    : $"<color=purple>{profilePp}</color> {ppLabel}"
            );
        }

        public void ShowPlayTable()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _playTableOpen = true;

            if (!_initialized || _profile is null)
            {
                return;
            }

            UpdatePlayTableValues();
            QueuePlayTableLayout();
        }

        public void HidePlayTable()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _playTableOpen = false;
        }

        public void PageUp()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (_profile is null)
                return;

            _profile.PageUp();
            UpdatePlayTableValues();
            QueuePlayTableLayout();
        }

        public void PageTop()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (_profile is null)
                return;

            _profile.PageTop();
            UpdatePlayTableValues();
            QueuePlayTableLayout();
        }

        public void PageDown()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            if (_profile is null)
                return;

            _profile.PageDown();
            UpdatePlayTableValues();
            QueuePlayTableLayout();
        }

        public void ShowSessionTable()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _sessionTableOpen = true;

            if (!_initialized || _profile is null)
            {
                return;
            }

            EnsureSessionSubscription();
            UpdateSessionRows();
            UpdateSessionSummary();
            QueueSessionTableLayout();
        }

        public void HideSessionTable()
        {
            ThrowIfDisposed();
            MainThreadDispatcher.AssertOnMainThread();

            _sessionTableOpen = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            UnsubscribeFromModel();

            _profile = null;
            _session = null;
            _playTable = null;
            _sessionRows.Clear();
        }

        private void HandleModelChanged()
        {
            if (_disposed)
                return;

            try
            {
                _dispatcher.EnqueueAction(() =>
                {
                    if (!_disposed)
                        Refresh();
                });
            }
            catch (ObjectDisposedException)
            {
                // The app/menu context is already shutting down.
            }
        }

        private void EnsureSessionSubscription()
        {
            if (_profile is null)
                return;

            Session currentSession = _profile.CurrentSession;

            if (ReferenceEquals(_session, currentSession))
                return;

            _session?.Changed -= HandleModelChanged;

            _session = currentSession;
            _session.Changed += HandleModelChanged;

            _sessionRows.Clear();
        }

        private void UnsubscribeFromModel()
        {
            _profile?.Changed -= HandleModelChanged;

            _session?.Changed -= HandleModelChanged;
        }

        private void UpdateProfileSummary()
        {
            if (_profile is null || _plusOneLabel is null || _plusOneText is null)
            {
                return;
            }

            string ppLabel = _profile.PpLabel;

            _plusOneLabel.SetText($"+1 {ppLabel.ToLowerInvariant()}");

            _plusOneText.SetText($"<color=#0F0>{_profile.PlusOne}</color> " + $"{ppLabel.ToLowerInvariant()}");

            UpdateSessionSummary();
        }

        private void UpdateSessionSummary()
        {
            if (_profile is null || _session is null || _sessionPlaysSet is null || _sessionPpGained is null)
            {
                return;
            }

            SessionSnapshot snapshot = _session.GetSnapshot();

            string ppLabel = _profile.PpLabel.ToUpperInvariant();

            _sessionPlaysSet.SetText(
                $"<color=#0F0>{snapshot.PlaysSet}</color> "
                    + $"Top {Profile.GetPlusOneCount(_profile.Leaderboard)} "
                    + $"Score{(snapshot.PlaysSet == 1 ? "" : "s")}"
            );

            _sessionPpGained.SetText($"+<color=purple>{snapshot.GainedProfilePp}</color> " + $"Profile {ppLabel}");
        }

        private void UpdatePlayTableValues()
        {
            if (_profile is null || _playTableText is null)
            {
                return;
            }

            string[][] values = CreatePlayTableValues(_profile);

            string[] headers = CreatePlayTableHeaders(_profile);

            if (_playTable is null)
            {
                _playTable = _tableFactory.Create(_playTableText, values, headers);
                _playTable.HasEndColumn = true;
            }
            else
            {
                _playTable.SetValues(values);
            }

            if (_playTableOpen)
                QueuePlayTableLayout();
        }

        private void UpdateSessionRows()
        {
            if (_profile is null || _session is null)
            {
                return;
            }

            SessionSnapshot snapshot = _session.GetSnapshot();

            string ppLabel = _profile.PpLabel;

            while (_sessionRows.Count > snapshot.Scores.Count)
            {
                _sessionRows.RemoveAt(_sessionRows.Count - 1);
            }

            for (int index = 0; index < snapshot.Scores.Count; index++)
            {
                Play play = snapshot.Scores[index];

                if (index < _sessionRows.Count && _sessionRows[index] is SessionListInfo existing)
                {
                    existing.Update(play);
                    continue;
                }

                _sessionRows.Add(_sessionListInfoFactory.Create(play, ppLabel));
            }

            UpdateSessionSummary();
        }

        private void ReloadSessionTable()
        {
            if (_sessionTable is null)
                return;

#if NEW_VERSION
            _sessionTable.Data = _sessionRows;

            _sessionTable.TableView.ReloadData();
#else
            _sessionTable.data = _sessionRows;

            _sessionTable.tableView.ReloadData();
#endif
        }

        private void QueuePlayTableLayout()
        {
            if (!_playTableOpen || _playTable is null)
            {
                return;
            }

            _dispatcher.EnqueueRoutine(UpdatePlayTableLayout());
        }

        private IEnumerator UpdatePlayTableLayout()
        {
            yield return new WaitForEndOfFrame();

            if (
                _disposed
                || !_playTableOpen
                || _playTable is null
                || _playTableButtons is null
                || _playTableModal is null
            )
            {
                yield break;
            }

            _playTable.UpdateTable();

            _playTableButtons.spacing = (_playTable.TableWidth - PlayTableButtonWidths) / 2f;

            if (_playTableModal.transform is RectTransform rectTransform)
            {
                rectTransform.sizeDelta = new Vector2(_playTable.TableWidth, _playTable.TableHeight * 1.5f);
            }
        }

        private void QueueSessionTableLayout()
        {
            if (!_sessionTableOpen)
                return;

            _dispatcher.EnqueueRoutine(UpdateSessionTableLayout());
        }

        private IEnumerator UpdateSessionTableLayout()
        {
            yield return new WaitForEndOfFrame();

            if (_disposed || !_sessionTableOpen)
            {
                yield break;
            }

            ReloadSessionTable();

            if (_sessionWindow?.transform is RectTransform rectTransform)
            {
                rectTransform.sizeDelta = new Vector2(120, 50);
            }
        }

        private string[][] CreatePlayTableValues(Profile profile)
        {
            Play[] plays = profile.GetCurrentPageSnapshot();

            int pageOffset = (profile.PageNumber - 1) * Profile.PLAYS_TO_SHOW;

            string[][] values = new string[plays.Length][];

            for (int index = 0; index < plays.Length; index++)
            {
                Play play = plays[index];

                List<string> row =
                [
                    $"<color=#FA0>#{pageOffset + index + 1}</color>",
                    play.MapName.ClampString(40),
                    ColorizeDifficulty(play.Difficulty),
                    $"<color=purple>"
                        + $"{Math.Round(play.Pp, _config.DecimalPrecision)}"
                        + $"</color> {profile.PpLabel}",
                    $"<color=#4AF>{play.MapKey}</color>",
                    play.Rank == 0
                        ? "<color=#777>Unknown</color>"
                        : $"{_formatter.GetWeightedRankColor(play.Rank)}" + $"#{play.Rank}</color>",
                ];

                if (profile.IsAccSaberOverall)
                {
                    row.Add($"<color=#CCC>" + $"{play.AccSaberCategory}</color>");
                }

                values[index] = [.. row];
            }

            return values;
        }

        private static string[] CreatePlayTableHeaders(Profile profile)
        {
            List<string> headers =
            [
                "<color=#FA0>Score #</color>",
                "Beatmap Name",
                "<color=#0F0>D</color>" + "<color=#CC0>i</color>" + "<color=#F70>f</color>" + "<color=#C16>f</color>",
                $"<color=purple>{profile.PpLabel}</color>",
                "<color=#4AF>Key</color>",
                "<color=#FF0>Rank</color>",
            ];

            if (profile.IsAccSaberOverall)
            {
                headers.Add("<color=#777>Category</color>");
            }

            return [.. headers];
        }

        private static string ColorizeDifficulty(BeatmapDifficulty difficulty)
        {
            return difficulty switch
            {
                BeatmapDifficulty.Easy => "<color=#0F0>Easy</color>",

                BeatmapDifficulty.Normal => "<color=#CC0>Normal</color>",

                BeatmapDifficulty.Hard => "<color=#F70>Hard</color>",

                BeatmapDifficulty.Expert => "<color=#C16>Expert</color>",

                BeatmapDifficulty.ExpertPlus => "<color=#F0F>Expert+</color>",

                _ => $"<color=#AAA>{difficulty}</color>",
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProfilePanelPresenter));
            }
        }
    }
}
