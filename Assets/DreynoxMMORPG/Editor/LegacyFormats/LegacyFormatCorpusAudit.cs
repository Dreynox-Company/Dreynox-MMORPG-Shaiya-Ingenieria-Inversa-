using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyFormatCorpusAudit
    {
        [Serializable]
        private sealed class AuditReport
        {
            public string baselineId;
            public string generatedUtc;
            public string dataRoot;
            public bool cancelled;

            public int threeDcFiles;
            public int threeDcSections;
            public int threeDcStandardSections;
            public int threeDcSkeletonlessSections;
            public int threeDcTexturePrefixedSections;
            public int threeDcConcatenatedFiles;
            public int threeDcFailures;

            public int aniFiles;
            public int aniLegacyFiles;
            public int aniV2Files;
            public int aniFailures;

            public List<string> failures = new List<string>();
        }

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Audit Entire Canonical 3DC + ANI Corpus")]
        public static void Run()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                throw new InvalidOperationException(
                    "Configure the canonical ps0032 corpus first.");
            }

            string[] allFiles =
                Directory
                    .EnumerateFiles(
                        corpus.DataRootPath,
                        "*",
                        SearchOption.AllDirectories)
                    .ToArray();

            string[] threeDc =
                allFiles
                    .Where(path =>
                        string.Equals(
                            Path.GetExtension(path),
                            ".3dc",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            string[] ani =
                allFiles
                    .Where(path =>
                        string.Equals(
                            Path.GetExtension(path),
                            ".ani",
                            StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            var report = new AuditReport
            {
                baselineId = CanonicalClientCorpus.BaselineId,
                generatedUtc =
                    DateTime.UtcNow.ToString("O"),
                dataRoot = corpus.DataRootPath,
                threeDcFiles = threeDc.Length,
                aniFiles = ani.Length
            };

            try
            {
                int total = threeDc.Length + ani.Length;
                int ordinal = 0;

                for (int i = 0; i < threeDc.Length; i++, ordinal++)
                {
                    if (DisplayProgress(
                            "3DC corpus audit",
                            threeDc[i],
                            ordinal,
                            total))
                    {
                        report.cancelled = true;
                        break;
                    }

                    try
                    {
                        IReadOnlyList<Legacy3dcFile> sections =
                            Legacy3dcParser.ParseMany(threeDc[i]);

                        report.threeDcSections += sections.Count;

                        if (sections.Count > 1)
                            report.threeDcConcatenatedFiles++;

                        for (int s = 0; s < sections.Count; s++)
                        {
                            switch (sections[s].Layout)
                            {
                                case Legacy3dcLayout.Standard:
                                    report.threeDcStandardSections++;
                                    break;
                                case Legacy3dcLayout.Skeletonless:
                                    report.threeDcSkeletonlessSections++;
                                    break;
                                case Legacy3dcLayout.TexturePrefixed:
                                    report.threeDcTexturePrefixedSections++;
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        report.threeDcFailures++;
                        report.failures.Add(
                            Relative(corpus, threeDc[i]) +
                            " :: " + ex.Message);
                    }
                }

                if (!report.cancelled)
                {
                    for (int i = 0; i < ani.Length; i++, ordinal++)
                    {
                        if (DisplayProgress(
                                "ANI corpus audit",
                                ani[i],
                                ordinal,
                                total))
                        {
                            report.cancelled = true;
                            break;
                        }

                        try
                        {
                            LegacyAniFile parsed =
                                LegacyAniParser.Parse(ani[i]);

                            if (parsed.IsV2)
                                report.aniV2Files++;
                            else
                                report.aniLegacyFiles++;
                        }
                        catch (Exception ex)
                        {
                            report.aniFailures++;
                            report.failures.Add(
                                Relative(corpus, ani[i]) +
                                " :: " + ex.Message);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string outputDirectory =
                Path.GetFullPath("Artifacts/Parity");

            Directory.CreateDirectory(outputDirectory);

            string output =
                Path.Combine(
                    outputDirectory,
                    "legacy-format-corpus-audit.json");

            File.WriteAllText(
                output,
                JsonUtility.ToJson(report, true));

            Debug.Log(
                "Dreynox MMORPG legacy corpus audit: " +
                report.threeDcFiles + " 3DC files / " +
                report.threeDcSections + " sections / " +
                report.threeDcFailures + " failures; " +
                report.aniFiles + " ANI files / " +
                report.aniFailures + " failures. Report: " +
                output);

            EditorUtility.RevealInFinder(output);
        }

        private static bool DisplayProgress(
            string title,
            string file,
            int ordinal,
            int total)
        {
            float progress =
                total <= 0
                    ? 0f
                    : ordinal / (float)total;

            return EditorUtility.DisplayCancelableProgressBar(
                title,
                Path.GetFileName(file),
                progress);
        }

        private static string Relative(
            CanonicalClientCorpus corpus,
            string absolute)
        {
            return absolute
                .Substring(corpus.RootPath.Length)
                .TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');
        }
    }
}
