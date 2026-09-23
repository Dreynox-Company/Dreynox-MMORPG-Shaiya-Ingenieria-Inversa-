using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.AnimationSystem
{
    [CreateAssetMenu(
        menuName = "Dreynox MMORPG/Animation/Semantic Catalog",
        fileName = "AnimationStateCatalog")]
    public sealed class AnimationStateCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string semanticState;
            public AnimationClip clip;
            public float playbackSpeed;
        }

        private static readonly string[] CoreSemantics =
        {
            "idle",
            "walk",
            "run",
            "jump",
            "mount_idle",
            "mount_walk",
            "mount_run",
            "hover",
            "flight",
            "combat_idle",
            "combat_move",
            "dead"
        };

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public bool TryGet(string semanticState, out AnimationClip clip)
        {
            float speed;
            string resolved;
            return TryResolve(semanticState, out clip, out speed, out resolved);
        }

        public bool TryResolve(
            string semanticState,
            out AnimationClip clip,
            out float playbackSpeed,
            out string resolvedSemantic)
        {
            clip = null;
            playbackSpeed = 1f;
            resolvedSemantic = string.Empty;

            if (string.IsNullOrWhiteSpace(semanticState))
                return false;

            EnsureLookup();

            string normalized = semanticState.Trim().ToLowerInvariant();
            Entry entry;
            if (TryEntry(normalized, out entry))
            {
                Assign(entry, normalized, out clip, out playbackSpeed, out resolvedSemantic);
                return true;
            }

            string fallback = WeaponAgnosticFallback(normalized);
            if (!string.Equals(fallback, normalized, StringComparison.Ordinal) &&
                TryEntry(fallback, out entry))
            {
                Assign(entry, fallback, out clip, out playbackSpeed, out resolvedSemantic);
                return true;
            }

            return false;
        }

        public List<string> MissingCoreSemantics()
        {
            EnsureLookup();
            List<string> missing = new List<string>();
            for (int i = 0; i < CoreSemantics.Length; i++)
            {
                Entry entry;
                if (!TryEntry(CoreSemantics[i], out entry) || entry.clip == null)
                    missing.Add(CoreSemantics[i]);
            }
            return missing;
        }

        public List<string> DuplicateSemantics()
        {
            Dictionary<string, int> counts =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                string key = Normalize(entries[i].semanticState);
                if (string.IsNullOrEmpty(key)) continue;
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
            }

            List<string> duplicates = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
                if (pair.Value > 1) duplicates.Add(pair.Key);

            duplicates.Sort(StringComparer.Ordinal);
            return duplicates;
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                string key = Normalize(entry.semanticState);
                if (string.IsNullOrEmpty(key)) continue;

                if (entry.playbackSpeed <= 0f)
                    entry.playbackSpeed = 1f;

                // First entry wins so accidental duplicates are deterministic
                // and can be surfaced by DuplicateSemantics().
                if (!_lookup.ContainsKey(key))
                    _lookup.Add(key, entry);
            }
        }

        private void EnsureLookup()
        {
            if (_lookup == null)
                RebuildLookup();
        }

        private bool TryEntry(string key, out Entry entry)
        {
            return _lookup.TryGetValue(key, out entry) && entry.clip != null;
        }

        private static void Assign(
            Entry entry,
            string semantic,
            out AnimationClip clip,
            out float playbackSpeed,
            out string resolvedSemantic)
        {
            clip = entry.clip;
            playbackSpeed = entry.playbackSpeed <= 0f ? 1f : entry.playbackSpeed;
            resolvedSemantic = semantic;
        }

        private static string WeaponAgnosticFallback(string semantic)
        {
            string[] suffixes = { "_spear", "_bow", "_staff", "_dagger", "_twohand", "_onehand" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (semantic.EndsWith(suffixes[i], StringComparison.Ordinal))
                    return semantic.Substring(0, semantic.Length - suffixes[i].Length);
            }
            return semantic;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
