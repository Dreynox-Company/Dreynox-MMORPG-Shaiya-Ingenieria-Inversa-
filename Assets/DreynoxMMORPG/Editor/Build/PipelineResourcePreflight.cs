using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Build
{
    public static class PipelineResourcePreflight
    {
        private static readonly string[] Required =
        {
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveTransparent.shadergraph",
            "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader"
        };
        [Serializable] private sealed class Record
        {
            public string path, diskPath, guid, loadedType, initialType, importerType;
            public long bytes;
            public bool forcedImport;
        }
        [Serializable] private sealed class Report
        {
            public string unity, failure;
            public bool passed;
            public List<Record> resources = new List<Record>();
        }
        public static void Run()
        {
            var report = new Report { unity = Application.unityVersion };
            Directory.CreateDirectory("Artifacts/StartingWorld");
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (string path in Required)
                {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                    if (package == null) throw new InvalidOperationException("Package not resolved for " + path);
                    string prefix = "Packages/" + package.name + "/";
                    var row = new Record
                    {
                        path = path,
                        diskPath = Path.Combine(package.resolvedPath, path.Substring(prefix.Length))
                    };
                    report.resources.Add(row);
                    if (!File.Exists(row.diskPath)) throw new FileNotFoundException("Installed package resource missing.", row.diskPath);
                    row.bytes = new FileInfo(row.diskPath).Length;
                    var resource = AssetDatabase.LoadMainAssetAtPath(path);
                    row.initialType = resource != null ? resource.GetType().FullName : "null";
                    // A generic DefaultAsset proves only that a file exists, not that
                    // the package's ScriptedImporter produced a usable graphics resource.
                    if (resource == null || resource is DefaultAsset)
                    {
                        row.forcedImport = true;
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate |
                            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.DontDownloadFromCacheServer);
                        resource = AssetDatabase.LoadMainAssetAtPath(path);
                    }
                    var importer = AssetImporter.GetAtPath(path);
                    row.importerType = importer != null ? importer.GetType().FullName : "null";
                    row.guid = AssetDatabase.AssetPathToGUID(path);
                    row.loadedType = resource != null ? resource.GetType().FullName : "null";
                    Debug.Log("DREYNOX_PACKAGE_RESOURCE " + path + " type=" + row.loadedType +
                        " importer=" + row.importerType + " bytes=" + row.bytes);
                    if (resource == null || resource is DefaultAsset || string.IsNullOrWhiteSpace(row.guid))
                        throw new InvalidOperationException("Package resource was not imported by its graphics importer: " + path + " (" + row.loadedType + ")");
                    if (resource is Shader shader && ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("Package shader compilation failed: " + path);
                }
                WorldRenderPipelineSetup.EnsureConfigured();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.passed = true;
                Debug.Log("DREYNOX_PIPELINE_RESOURCE_PREFLIGHT_OK");
            }
            catch (Exception ex) { report.failure = ex.ToString(); throw; }
            finally
            {
                File.WriteAllText("Artifacts/StartingWorld/pipeline-resources.json", JsonUtility.ToJson(report, true));
            }
        }
    }
}
