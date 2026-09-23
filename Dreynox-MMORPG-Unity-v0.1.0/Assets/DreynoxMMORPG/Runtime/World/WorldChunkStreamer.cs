using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.World
{
    public sealed class WorldChunkStreamer : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField] private List<WorldChunkDefinition> chunks = new List<WorldChunkDefinition>();
        [SerializeField, Min(0.1f)] private float evaluationInterval = 0.5f;
        private float _nextEvaluation;
        private readonly HashSet<string> _pending = new HashSet<string>();

        private void Update()
        {
            if (observer == null || Time.unscaledTime < _nextEvaluation) return;
            _nextEvaluation = Time.unscaledTime + evaluationInterval;
            foreach (WorldChunkDefinition chunk in chunks)
            {
                if (chunk == null || string.IsNullOrWhiteSpace(chunk.sceneName)) continue;
                float sqr = (observer.position - chunk.center).sqrMagnitude;
                Scene scene = SceneManager.GetSceneByName(chunk.sceneName);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded && !_pending.Contains(chunk.sceneName) && sqr <= chunk.loadRadius * chunk.loadRadius)
                {
                    _pending.Add(chunk.sceneName);
                    AsyncOperation op = SceneManager.LoadSceneAsync(chunk.sceneName, LoadSceneMode.Additive);
                    if (op != null) op.completed += _ => _pending.Remove(chunk.sceneName);
                    else _pending.Remove(chunk.sceneName);
                }
                else if (loaded && !_pending.Contains(chunk.sceneName) && sqr >= chunk.unloadRadius * chunk.unloadRadius)
                {
                    _pending.Add(chunk.sceneName);
                    AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                    if (op != null) op.completed += _ => _pending.Remove(chunk.sceneName);
                    else _pending.Remove(chunk.sceneName);
                }
            }
        }
    }
}
