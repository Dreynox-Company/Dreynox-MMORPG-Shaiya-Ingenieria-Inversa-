using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Dreynox.Mmorpg.Core
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        [SerializeField, Min(1)] private int maxActionsPerFrame = 512;

        public void Enqueue(Action action)
        {
            if (action != null) _queue.Enqueue(action);
        }

        private void Update()
        {
            int processed = 0;
            while (processed < maxActionsPerFrame && _queue.TryDequeue(out Action action))
            {
                try { action(); }
                catch (Exception ex) { Debug.LogException(ex); }
                processed++;
            }
        }
    }
}
