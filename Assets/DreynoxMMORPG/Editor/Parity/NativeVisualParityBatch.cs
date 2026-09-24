using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.ParityCore;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Parity
{
    [Serializable]
    public sealed class NativeVisualParityScenarioReport
    {
        public string scenarioId = string.Empty;
        public string referenceFile = string.Empty;
        public string candidateFile = string.Empty;
        public string referenceSha256 = string.Empty;
        public string candidateSha256 = string.Empty;
        public int width;
        public int height;
        public double mae;
        public double rmse;
        public double psnr;
        public double ssim;
        public string diffFile = string.Empty;
        public string status = string.Empty;
    }

    [Serializable]
    public sealed class NativeVisualParityReport
    {
        public string generatedUtc = string.Empty;
        public string referenceClientSha256 =
            NativeVisualReferenceCore.OriginalClientSha256;
        public string diagnosticCaptureClientSha256 =
            NativeVisualReferenceCore.DiagnosticCaptureClientSha256;
        public string referenceRoot = string.Empty;
        public string candidateRoot = string.Empty;
        public int compared;
        public int missingCandidates;
        public int invalidReferences;
        public List<NativeVisualParityScenarioReport> scenarios =
            new List<NativeVisualParityScenarioReport>();
    }

    public static class NativeVisualParityBatch
    {
        private const string ReferenceRootEnvironment =
            "DREYNOX_NATIVE_REFERENCE_ROOT";

        private const string CandidateRootEnvironment =
            "DREYNOX_UNITY_CAPTURE_ROOT";

        private const string ReferenceRootEditorPref =
            "Dreynox.Mmorpg.NativeReferenceRoot";

        private const string CandidateRootEditorPref =
            "Dreynox.Mmorpg.UnityCaptureRoot";

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Compare Native Reference Suite")]
        public static void RunInteractive()
        {
            string referenceRoot =
                EditorUtility.OpenFolderPanel(
                    "Native game.exe reference screenshots",
                    EditorPrefs.GetString(
                        ReferenceRootEditorPref,
                        string.Empty),
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    referenceRoot))
                return;

            string candidateRoot =
                EditorUtility.OpenFolderPanel(
                    "Unity parity screenshots",
                    EditorPrefs.GetString(
                        CandidateRootEditorPref,
                        string.Empty),
                    string.Empty);

            if (string.IsNullOrWhiteSpace(
                    candidateRoot))
                return;

            EditorPrefs.SetString(
                ReferenceRootEditorPref,
                referenceRoot);

            EditorPrefs.SetString(
                CandidateRootEditorPref,
                candidateRoot);

            string reportPath =
                Compare(
                    referenceRoot,
                    candidateRoot,
                    Path.GetFullPath(
                        "Artifacts/Parity/Reports"));

            EditorUtility.RevealInFinder(
                reportPath);

            Debug.Log(
                "Dreynox native visual parity report: " +
                reportPath);
        }

        public static void RunFromEnvironment()
        {
            string referenceRoot =
                Environment.GetEnvironmentVariable(
                    ReferenceRootEnvironment);

            string candidateRoot =
                Environment.GetEnvironmentVariable(
                    CandidateRootEnvironment);

            if (string.IsNullOrWhiteSpace(
                    referenceRoot))
            {
                throw new InvalidOperationException(
                    ReferenceRootEnvironment +
                    " is not configured.");
            }

            if (string.IsNullOrWhiteSpace(
                    candidateRoot))
            {
                throw new InvalidOperationException(
                    CandidateRootEnvironment +
                    " is not configured.");
            }

            string reportPath =
                Compare(
                    referenceRoot,
                    candidateRoot,
                    Path.GetFullPath(
                        "Artifacts/Parity/Reports"));

            Debug.Log(
                "Dreynox native visual parity report: " +
                reportPath);
        }

        public static string Compare(
            string referenceRoot,
            string candidateRoot,
            string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(
                    referenceRoot) ||
                !Directory.Exists(
                    referenceRoot))
            {
                throw new DirectoryNotFoundException(
                    "Native reference root not found: " +
                    referenceRoot);
            }

            if (string.IsNullOrWhiteSpace(
                    candidateRoot) ||
                !Directory.Exists(
                    candidateRoot))
            {
                throw new DirectoryNotFoundException(
                    "Unity capture root not found: " +
                    candidateRoot);
            }

            Directory.CreateDirectory(
                outputRoot);

            string diffRoot =
                Path.Combine(
                    outputRoot,
                    "diff");

            Directory.CreateDirectory(
                diffRoot);

            var report =
                new NativeVisualParityReport
                {
                    generatedUtc =
                        DateTime.UtcNow
                            .ToString(
                                "O",
                                CultureInfo.InvariantCulture),
                    referenceRoot =
                        Path.GetFullPath(
                            referenceRoot),
                    candidateRoot =
                        Path.GetFullPath(
                            candidateRoot)
                };

            for (int i = 0;
                 i < NativeVisualReferenceCore.All.Count;
                 i++)
            {
                NativeVisualReference reference =
                    NativeVisualReferenceCore.All[i];

                var scenario =
                    new NativeVisualParityScenarioReport
                    {
                        scenarioId =
                            reference.ScenarioId,
                        referenceFile =
                            reference.FileName,
                        candidateFile =
                            reference.ScenarioId +
                            ".png",
                        referenceSha256 =
                            reference.Sha256,
                        width =
                            reference.Width,
                        height =
                            reference.Height
                    };

                report.scenarios.Add(
                    scenario);

                string referencePath =
                    FindReference(
                        referenceRoot,
                        reference.FileName);

                if (referencePath == null)
                {
                    scenario.status =
                        "reference-missing";

                    report.invalidReferences++;
                    continue;
                }

                string actualReferenceHash =
                    FileFingerprint.Sha256(
                        referencePath);

                if (!string.Equals(
                        actualReferenceHash,
                        reference.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    scenario.status =
                        "reference-sha-mismatch";

                    scenario.referenceSha256 =
                        actualReferenceHash;

                    report.invalidReferences++;
                    continue;
                }

                string candidatePath =
                    Path.Combine(
                        candidateRoot,
                        reference.ScenarioId +
                        ".png");

                if (!File.Exists(
                        candidatePath))
                {
                    scenario.status =
                        "candidate-missing";

                    report.missingCandidates++;
                    continue;
                }

                scenario.candidateSha256 =
                    FileFingerprint.Sha256(
                        candidatePath);

                Texture2D native =
                    LoadPng(
                        referencePath);

                Texture2D unity =
                    LoadPng(
                        candidatePath);

                try
                {
                    if (native.width !=
                            reference.Width ||
                        native.height !=
                            reference.Height)
                    {
                        scenario.status =
                            "reference-dimension-mismatch";

                        report.invalidReferences++;
                        continue;
                    }

                    if (unity.width !=
                            reference.Width ||
                        unity.height !=
                            reference.Height)
                    {
                        scenario.status =
                            "candidate-dimension-mismatch";

                        continue;
                    }

                    Color32[] nativePixels =
                        native.GetPixels32();

                    Color32[] unityPixels =
                        unity.GetPixels32();

                    VisualMetricResult metrics =
                        VisualMetricCore.CompareRgb24(
                            ToRgb24(
                                nativePixels),
                            ToRgb24(
                                unityPixels));

                    scenario.mae =
                        metrics.Mae;

                    scenario.rmse =
                        metrics.Rmse;

                    scenario.psnr =
                        metrics.Psnr;

                    scenario.ssim =
                        metrics.Ssim;

                    string diffPath =
                        Path.Combine(
                            diffRoot,
                            reference.ScenarioId +
                            ".png");

                    WriteDiff(
                        native.width,
                        native.height,
                        nativePixels,
                        unityPixels,
                        diffPath);

                    scenario.diffFile =
                        diffPath;

                    scenario.status =
                        "compared";

                    report.compared++;
                }
                finally
                {
                    UnityEngine.Object
                        .DestroyImmediate(
                            native);

                    UnityEngine.Object
                        .DestroyImmediate(
                            unity);
                }
            }

            string reportPath =
                Path.Combine(
                    outputRoot,
                    "native-vs-unity.json");

            File.WriteAllText(
                reportPath,
                JsonUtility.ToJson(
                    report,
                    true));

            return reportPath;
        }

        private static string FindReference(
            string root,
            string fileName)
        {
            string direct =
                Path.Combine(
                    root,
                    fileName);

            if (File.Exists(
                    direct))
                return direct;

            foreach (string path in
                     Directory.EnumerateFiles(
                         root,
                         "*.png",
                         SearchOption.AllDirectories))
            {
                if (string.Equals(
                        Path.GetFileName(path),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return null;
        }

        private static Texture2D LoadPng(
            string path)
        {
            byte[] bytes =
                File.ReadAllBytes(
                    path);

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false,
                    true);

            if (!ImageConversion.LoadImage(
                    texture,
                    bytes,
                    false))
            {
                UnityEngine.Object
                    .DestroyImmediate(
                        texture);

                throw new InvalidDataException(
                    "Unity could not decode PNG: " +
                    path);
            }

            return texture;
        }

        private static byte[] ToRgb24(
            Color32[] pixels)
        {
            byte[] result =
                new byte[
                    pixels.Length *
                    3];

            int offset = 0;

            for (int i = 0;
                 i < pixels.Length;
                 i++)
            {
                result[offset++] =
                    pixels[i].r;

                result[offset++] =
                    pixels[i].g;

                result[offset++] =
                    pixels[i].b;
            }

            return result;
        }

        private static void WriteDiff(
            int width,
            int height,
            Color32[] reference,
            Color32[] candidate,
            string output)
        {
            Texture2D diff =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    true);

            try
            {
                Color32[] pixels =
                    new Color32[
                        reference.Length];

                for (int i = 0;
                     i < pixels.Length;
                     i++)
                {
                    pixels[i] =
                        new Color32(
                            (byte)Mathf.Abs(
                                reference[i].r -
                                candidate[i].r),
                            (byte)Mathf.Abs(
                                reference[i].g -
                                candidate[i].g),
                            (byte)Mathf.Abs(
                                reference[i].b -
                                candidate[i].b),
                            255);
                }

                diff.SetPixels32(
                    pixels);

                diff.Apply(
                    false,
                    false);

                File.WriteAllBytes(
                    output,
                    diff.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object
                    .DestroyImmediate(
                        diff);
            }
        }
    }
}
