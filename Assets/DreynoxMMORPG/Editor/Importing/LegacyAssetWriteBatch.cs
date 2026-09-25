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
        private int writes, flushes, batchedGeometryWrites;
        private bool faulted;
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
            if (batch.faulted)
                throw new InvalidOperationException("The current import transaction has failed; rebuild it rather than reusing partial assets.");
            var values = new List<KeyValuePair<string, Object>>(batch.pending);
            var removals = new List<string>(batch.deleted);
            var additions = new List<KeyValuePair<Object, Object>>(batch.children);
            // Meshes and numeric ANI curves have no shader/resource dependencies.
            // Group just these native writes. Materials, terrain, catalogs and
            // subassets remain synchronous, after geometry imports have completed.
            var geometry = new List<KeyValuePair<string, Object>>();
            var geometryPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value.Value == null)
                {
                    batch.faulted = true;
                    throw new InvalidOperationException("Staged Unity object destroyed before persistence: " + value.Key);
                }
                if (value.Value is Mesh || (value.Value is AnimationClip clip &&
                    AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0))
                {
                    geometry.Add(value);
                    geometryPaths.Add(value.Key);
                }
            }
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                foreach (string path in removals)
                    if (!geometryPaths.Contains(path)) AssetDatabase.DeleteAsset(path);
                if (geometry.Count > 0)
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (string path in removals)
                            if (geometryPaths.Contains(path)) AssetDatabase.DeleteAsset(path);
                        foreach (var value in geometry) AssetDatabase.CreateAsset(value.Value, value.Key);
                        // No loads, prefabs, materials or package lookups while imports are suspended.
                    }
                    finally { AssetDatabase.StopAssetEditing(); }
                    batch.batchedGeometryWrites += geometry.Count;
                }
                foreach (var value in values)
                    if (!geometryPaths.Contains(value.Key)) AssetDatabase.CreateAsset(value.Value, value.Key);
                foreach (var value in additions) AssetDatabase.AddObjectToAsset(value.Key, value.Value);
                // A prefab may now reference the imported geometry and the
                // synchronously created materials without transient references.
                batch.writes += values.Count + additions.Count;
                batch.flushes++;
                batch.pending.Clear(); batch.deleted.Clear(); batch.children.Clear();
            }
            catch
            {
                // Dispose must not repeat a partially applied destructive batch
                // or hide the first exception with a second import failure.
                batch.faulted = true;
                throw;
            }
            finally { AssetDatabase.AllowAutoRefresh(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            try { if (!faulted) { Flush(); AssetDatabase.SaveAssets(); } }
            finally
            {
                active = null; disposed = true; timer.Stop();
                UnityEngine.Debug.Log("DREYNOX_IMPORT_BATCH writes=" + writes + " groups=" + flushes + " groupedGeometry=" + batchedGeometryWrites + " faulted=" + faulted + " seconds=" + timer.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        private static void RequireGenerated(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.Replace('\\', '/').StartsWith("Assets/DreynoxMMORPG/LocalLegacyGenerated/", StringComparison.Ordinal))
                throw new InvalidOperationException("Batch may only mutate generated legacy content: " + path);
        }
    }
}
