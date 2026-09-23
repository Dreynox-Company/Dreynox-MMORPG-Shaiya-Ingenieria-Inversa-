using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ProjectTools;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Build
{
    public static class DreynoxWindowsBuild
    {
        private const string DefaultOutput = "Builds/Windows/DreynoxMmorpg.exe";

        [MenuItem("Dreynox MMORPG/Build/Windows x64 Release")]
        public static void BuildMenu()
        {
            Build(DefaultOutput);
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds/Windows"));
        }

        public static void BuildBatch()
        {
            Build(DefaultOutput);
        }

        public static void Build(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Ruta de build vacía.", nameof(outputPath));
            ClientParitySceneBuilder.CreateSandbox();
            PlayerSettings.companyName = "Dreynox";
            PlayerSettings.productName = "Dreynox Mmorpg";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            string full = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ClientParitySceneBuilder.ScenePath },
                locationPathName = full,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Build Windows falló: {report.summary.result} · {report.summary.totalErrors} errores.");

            string exeHash = File.Exists(full) ? FileFingerprint.Sha256(full) : string.Empty;
            string manifest = Path.Combine(Path.GetDirectoryName(full), "dreynox-build-manifest.txt");
            File.WriteAllText(manifest,
                "product=Dreynox Mmorpg\n" +
                "target=StandaloneWindows64\n" +
                "unity=" + Application.unityVersion + "\n" +
                "exe=" + Path.GetFileName(full) + "\n" +
                "sha256=" + exeHash + "\n" +
                "size=" + (File.Exists(full) ? new FileInfo(full).Length.ToString() : "0") + "\n");
            Debug.Log("Dreynox MMORPG Windows build OK: " + full + " · SHA-256 " + exeHash);
        }
    }
}
