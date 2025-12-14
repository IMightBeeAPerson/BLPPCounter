using UnityEngine;
using System.Collections;

namespace BLPPCounter.Utils.Misc_Classes
{
    public class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost _instance;
        public static CoroutineHost Instance
        {
            get
            {
                if (_instance is null)
                {
                    GameObject go = new("CoroutineHost");
                    _instance = go.AddComponent<CoroutineHost>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public static Coroutine Start(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        public static void Stop(Coroutine coroutine)
        {
            _instance?.StopCoroutine(coroutine);
        }
    }

}
