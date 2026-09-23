using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.AnimationSystem
{
    [CreateAssetMenu(menuName = "Dreynox MMORPG/Animation/Semantic Catalog", fileName = "AnimationStateCatalog")]
    public sealed class AnimationStateCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public string semanticState; public AnimationClip clip; }
        [SerializeField] private List<Entry> entries = new List<Entry>();
        public bool TryGet(string semanticState, out AnimationClip clip)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].semanticState, semanticState, StringComparison.OrdinalIgnoreCase))
                {
                    clip = entries[i].clip;
                    return clip != null;
                }
            }
            clip = null;
            return false;
        }
    }
}
