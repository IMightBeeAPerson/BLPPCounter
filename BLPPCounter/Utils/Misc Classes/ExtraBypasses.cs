using BeatSaberMarkupLanguage.Components;
using BLPPCounter.Patches;
using HMUI;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace BLPPCounter.Utils.Misc_Classes
{
    public static class ExtraBypasses
    {
        #region TabSelector Bypasses
        private static readonly MethodInfo _tabSelected =
        typeof(TabSelector).GetMethod("TabSelected", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo _tabsField =
            typeof(TabSelector).GetField("_tabs", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Forces TabSelector to switch as if the user selected this index.
        /// </summary>
        public static void ForceSelectAndNotify(this TabSelector selector, int index)
        {
#if NEW_VERSION
            if (selector is null || selector.TextSegmentedControl is null) return;
            TextSegmentedControl segmented = selector.TextSegmentedControl;
#else
            if (selector is null || selector.textSegmentedControl is null) return;
            TextSegmentedControl segmented = selector.textSegmentedControl;
#endif
            segmented.SelectCellWithNumber(index);

            // Explicitly fire TabSelected so content gets updated
            _tabSelected?.Invoke(selector, [segmented, index]);
        }

        /// <summary>
        /// Returns the private list of Tab objects inside TabSelector.
        /// </summary>
        public static object[] GetTabs(this TabSelector selector)
        {
            return _tabsField?.GetValue(selector) as object[];
        }
#endregion
        #region Coroutine Bypasses
        public static Task AsTask(this IEnumerator coroutine, MonoBehaviour owner)
        {
            var tcs = new TaskCompletionSource<bool>();
            owner.StartCoroutine(Run(coroutine, tcs));
            return tcs.Task;
        }

        private static IEnumerator Run(IEnumerator coroutine, TaskCompletionSource<bool> tcs)
        {
            while (coroutine.MoveNext())
            {
                yield return coroutine.Current;
            }
            tcs.SetResult(true);
        }
        #endregion
    }
}
