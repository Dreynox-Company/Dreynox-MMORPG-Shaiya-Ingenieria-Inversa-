using Dreynox.Mmorpg.Core;
using Dreynox.Mmorpg.Networking;
using UnityEngine;

namespace Dreynox.Mmorpg.App
{
    [DefaultExecutionOrder(-1000)]
    public sealed class DreynoxBootstrap : MonoBehaviour
    {
        private static DreynoxBootstrap _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;

            if (GetComponent<MainThreadDispatcher>() == null)
                gameObject.AddComponent<MainThreadDispatcher>();
            if (GetComponent<AdaptiveQualityController>() == null)
                gameObject.AddComponent<AdaptiveQualityController>();
            if (GetComponent<NetworkPump>() == null)
                gameObject.AddComponent<NetworkPump>();
        }
    }
}
