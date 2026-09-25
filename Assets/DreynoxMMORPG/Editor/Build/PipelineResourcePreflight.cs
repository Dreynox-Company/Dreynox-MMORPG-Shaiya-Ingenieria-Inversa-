using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Build
{
    /// <summary>Warm package-import dependencies outside an asset postprocess/test callback.</summary>
    public static class PipelineResourcePreflight
    {
        private static readonly string[] Required =
        {
            "Packages/com.unity.render-pipelines.universal/Shaders/AutodeskInteractive/AutodeskInteractiveTransparent.shadergraph",
            "Packages/com.unity.render-pipelines.core/Editor/Lighting/ProbeVolume/RenderingLayerMask/TraceRenderingLayerMask.urtshader"
        };
        [Serializable] private sealed class Record
        {
            public string path, diskPath, guid, loadedType;
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
                    var package = PackageInfo.FindForAssetPath(path);
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
                    if (resource == null)
                    {
                        row.forcedImport = true;
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        resource = AssetDatabase.LoadMainAssetAtPath(path);
                    }
                    row.guid = AssetDatabase.AssetPathToGUID(path);
                    row.loadedType = resource != null ? resource.GetType().FullName : "null";
                    Debug.Log("DREYNOX_PACKAGE_RESOURCE " + path + " type=" + row.loadedType + " bytes=" + row.bytes);
                    if (resource == null || string.IsNullOrWhiteSpace(row.guid))
                        throw new InvalidOperationException("Installed package resource remains unloadable: " + path);
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
