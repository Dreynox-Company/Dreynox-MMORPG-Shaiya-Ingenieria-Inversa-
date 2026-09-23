using System;
using System.IO;
using System.Linq;
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
        private const string ParityOutput = "Builds/WindowsParity/DreynoxMmorpg-Parity.exe";
        private const string ReleaseOutput = "Builds/Windows/DreynoxMmorpg.exe";

        private static readonly string[] ReleaseScenes =
        {
            "Assets/DreynoxMMORPG/Game/Scenes/Boot.unity",
            "Assets/DreynoxMMORPG/Game/Scenes/Login.unity",
            "Assets/DreynoxMMORPG/Game/Scenes/World.unity"
        };

        [MenuItem("Dreynox MMORPG/Build/Windows x64/Parity Lab")]
        public static void BuildParityMenu()
        {
            BuildParityLab(ParityOutput);
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds/WindowsParity"));
        }

        [MenuItem("Dreynox MMORPG/Build/Windows x64/Client Release")]
        public static void BuildReleaseMenu()
        {
            BuildClientRelease(ReleaseOutput);
            EditorUtility.RevealInFinder(Path.GetFullPath("Builds/Windows"));
        }

        // Kept as the CI entrypoint used by the current workflow.
        public static void BuildBatch()
        {
            BuildParityLab(ParityOutput);
        }

        public static void BuildParityBatch()
        {
            BuildParityLab(ParityOutput);
        }

        public static void BuildReleaseBatch()
        {
            BuildClientRelease(ReleaseOutput);
        }

        public static void BuildParityLab(string outputPath)
        {
            ClientParitySceneBuilder.CreateSandbox();
            ConfigureIdentity();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Build(
                new[] { ClientParitySceneBuilder.ScenePath },
                outputPath,
                "parity-lab");
        }

        public static void BuildClientRelease(string outputPath)
        {
            string[] missing = ReleaseScenes.Where(scene => !File.Exists(scene)).ToArray();
            if (missing.Length > 0)
            {
                throw new BuildFailedException(
                    "Client Release bloqueado: faltan escenas reales del cliente. " +
                    "No se sustituirán por placeholders de diagnóstico. Faltan: " +
                    string.Join(", ", missing));
            }

            ConfigureIdentity();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            Build(ReleaseScenes, outputPath, "client-release");
        }

        private static void ConfigureIdentity()
        {
            PlayerSettings.companyName = "Dreynox";
            PlayerSettings.productName = "Dreynox Mmorpg";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
        }

        private static void Build(string[] scenes, string outputPath, string buildKind)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Ruta de build vacía.", nameof(outputPath));

            string full = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(full);
            if (string.IsNullOrWhiteSpace(directory))
                throw new BuildFailedException("No se pudo resolver el directorio de salida.");

            Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = full,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "Build Windows falló: " + report.summary.result +
                    " · " + report.summary.totalErrors + " errores.");
            }

            string exeHash = File.Exists(full) ? FileFingerprint.Sha256(full) : string.Empty;
            string manifest = Path.Combine(directory, "dreynox-build-manifest.txt");
            File.WriteAllText(
                manifest,
                "product=Dreynox Mmorpg\n" +
                "kind=" + buildKind + "\n" +
                "target=StandaloneWindows64\n" +
                "unity=" + Application.unityVersion + "\n" +
                "backend=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) + "\n" +
                "exe=" + Path.GetFileName(full) + "\n" +
                "sha256=" + exeHash + "\n" +
                "size=" + (File.Exists(full) ? new FileInfo(full).Length.ToString() : "0") + "\n");

            Debug.Log(
                "Dreynox MMORPG Windows " + buildKind +
                " build OK: " + full + " · SHA-256 " + exeHash);
        }
    }
}
