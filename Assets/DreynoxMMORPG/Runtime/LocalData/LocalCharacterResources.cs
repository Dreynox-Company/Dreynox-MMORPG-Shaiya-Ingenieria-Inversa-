using System.Collections.Generic;
using UnityEngine;
namespace Dreynox.Mmorpg.LocalData
{
    public sealed class LocalCharacterResources : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public void Own(UnityEngine.Object value) { owned.Add(value); }
        private void OnDestroy()
        {
            foreach (var resource in owned)
                if (resource != null)
                {
                    if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
                }
            owned.Clear();
        }
    }
}
