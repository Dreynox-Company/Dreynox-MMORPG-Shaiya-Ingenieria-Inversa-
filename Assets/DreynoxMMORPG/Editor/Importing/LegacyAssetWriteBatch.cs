using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Editor.Importing
{
    /// <summary>
    /// Stages native Unity assets in memory until a prefab needs persistent references.
    /// Raw image/audio imports remain synchronous. Never loads newly suspended imports.
    /// Existing callers outside Begin retain ordinary AssetDatabase semantics.
    /// </summary>
    public sealed class LegacyAssetWriteBatch : IDisposable
    {
        private static LegacyAssetWriteBatch active;
        private readonly Dictionary<string, Object> pending = new Dictionary<string, Object>(StringComparer.Ordinal);
        private readonly HashSet<string> deleted = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<Object, Object>> children = new List<KeyValuePair<Object, Object>>();
        private readonly Stopwatch timer = Stopwatch.StartNew();
        private int writes, flushes;
        private bool disposed;
        private LegacyAssetWriteBatch() { }
        public static LegacyAssetWriteBatch Begin()
        {
            if (active != null) throw new InvalidOperationException("Nested world import is not supported.");
            active = new LegacyAssetWriteBatch();
            return active;
        }
        public static void CreateAsset(Object value, string path)
        {
            if (active == null) { AssetDatabase.CreateAsset(value, path); return; }
            if (value == null) throw new ArgumentNullException(nameof(value));
            RequireGenerated(path);
            if (active.pending.ContainsKey(path))
                throw new InvalidOperationException("Duplicate pending asset without DeleteAsset: " + path);
            active.pending.Add(path, value);
        }
        public static bool DeleteAsset(string path)
        {
            if (active == null) return AssetDatabase.DeleteAsset(path);
            RequireGenerated(path);
            active.pending.Remove(path);
            active.deleted.Add(path);
            return true;
        }
        public static T LoadAssetAtPath<T>(string path) where T : Object
        {
            if (active != null)
            {
                if (active.pending.TryGetValue(path, out Object value)) return value as T;
                if (active.deleted.Contains(path)) return null;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        public static void AddObjectToAsset(Object value, Object parent)
        {
            if (active == null) { AssetDatabase.AddObjectToAsset(value, parent); return; }
            active.children.Add(new KeyValuePair<Object, Object>(value, parent));
        }
        public static GameObject SaveAsPrefabAsset(GameObject root, string path)
        {
            Flush();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        public static void SaveAssets()
        {
            if (active == null) AssetDatabase.SaveAssets();
        }
        public static void Refresh()
        {
            if (active == null) AssetDatabase.Refresh();
        }
        public static void Flush()
        {
            var batch = active;
            if (batch == null || (batch.pending.Count == 0 && batch.deleted.Count == 0 && batch.children.Count == 0)) return;
            // Snapshot before pausing imports; no LoadAssetAtPath/GetImporter in this scope.
            var values = new List<KeyValuePair<string, Object>>(batch.pending);
            var removals = new List<string>(batch.deleted);
            var additions = new List<KeyValuePair<Object, Object>>(batch.children);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in removals) AssetDatabase.DeleteAsset(path);
                foreach (var value in values)
                {
                    if (value.Value == null) throw new InvalidOperationException("Staged Unity object destroyed before persistence: " + value.Key);
                    AssetDatabase.CreateAsset(value.Value, value.Key);
                }
                foreach (var value in additions) AssetDatabase.AddObjectToAsset(value.Key, value.Value);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            batch.writes += values.Count + additions.Count;
            batch.flushes++;
            batch.pending.Clear(); batch.deleted.Clear(); batch.children.Clear();
        }
        public void Dispose()
        {
            if (disposed) return;
            try { Flush(); AssetDatabase.SaveAssets(); }
            finally
            {
                active = null; disposed = true; timer.Stop();
                UnityEngine.Debug.Log("DREYNOX_IMPORT_BATCH writes=" + writes + " groups=" + flushes + " seconds=" + timer.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        private static void RequireGenerated(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.Replace('\\', '/').StartsWith("Assets/DreynoxMMORPG/LocalLegacyGenerated/", StringComparison.Ordinal))
                throw new InvalidOperationException("Batch may only mutate generated legacy content: " + path);
        }
    }
}
