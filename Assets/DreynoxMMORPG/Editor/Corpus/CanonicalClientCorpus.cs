using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using UnityEditor;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    public sealed class CanonicalClientCorpus
    {
        public const string BaselineId = "ps0032-x86-3.3.2.10";
        public const long GameExeBytes = 5352488;
        public const string GameExeSha256 =
            "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d";
        public const string InnerZipSha256 =
            "78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5";

        private const string EditorPrefsKey =
            "Dreynox.Mmorpg.CanonicalCorpusRoot";

        public string RootPath { get; }
        public string GameExePath => Path.Combine(RootPath, "game.exe");
        public string DataRootPath => Path.Combine(RootPath, "DATA_Español");

        public CanonicalClientCorpus(string rootPath)
        {
            RootPath = Path.GetFullPath(rootPath ?? string.Empty);
        }

        public static string StoredRoot
        {
            get => EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    EditorPrefs.DeleteKey(EditorPrefsKey);
                else
                    EditorPrefs.SetString(
                        EditorPrefsKey,
                        Path.GetFullPath(value));
            }
        }

        public static CanonicalClientCorpus FromStoredRoot()
        {
            string root = StoredRoot;
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new CanonicalClientCorpus(root);
        }

        public CorpusValidationResult Validate()
        {
            var result = new CorpusValidationResult
            {
                baselineId = BaselineId,
                rootPath = RootPath
            };

            if (!Directory.Exists(RootPath))
            {
                result.errors.Add("Corpus root does not exist.");
                return result;
            }

            if (!File.Exists(GameExePath))
            {
                result.errors.Add("Missing game.exe at corpus root.");
            }
            else
            {
                FileInfo info = new FileInfo(GameExePath);
                result.gameExeBytes = info.Length;

                if (info.Length != GameExeBytes)
                {
                    result.errors.Add(
                        "game.exe size mismatch. Expected " +
                        GameExeBytes + ", got " + info.Length + ".");
                }

                result.gameExeSha256 =
                    FileFingerprint.Sha256(GameExePath);

                if (!string.Equals(
                        result.gameExeSha256,
                        GameExeSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.errors.Add(
                        "game.exe SHA-256 does not match canonical ps0032.");
                }
            }

            if (!Directory.Exists(DataRootPath))
                result.errors.Add("Missing DATA_Español directory.");

            ValidateRequiredFile(
                result,
                "DATA_Español/excelxml/wingposition.xml");
            ValidateRequiredFile(
                result,
                "DATA_Español/world/Login.wld");
            ValidateRequiredFile(
                result,
                "DATA_Español/character/wing/wing.mon");
            ValidateRequiredFile(
                result,
                "DATA_Español/interface/Login/BG.tga");

            return result;
        }

        public string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException(
                    "Relative corpus path is required.",
                    nameof(relativePath));

            string normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            string full = Path.GetFullPath(
                Path.Combine(RootPath, normalized));

            string root = RootPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!full.StartsWith(
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Resolved path escaped canonical corpus root.");
            }

            return full;
        }

        private void ValidateRequiredFile(
            CorpusValidationResult result,
            string relativePath)
        {
            string full = Resolve(relativePath);
            if (File.Exists(full))
                result.presentFiles.Add(relativePath);
            else
                result.missingFiles.Add(relativePath);
        }
    }

    [Serializable]
    public sealed class CorpusValidationResult
    {
        public string baselineId;
        public string rootPath;
        public long gameExeBytes;
        public string gameExeSha256;
        public readonly List<string> presentFiles =
            new List<string>();
        public readonly List<string> missingFiles =
            new List<string>();
        public readonly List<string> errors =
            new List<string>();

        public bool IsCanonical =>
            errors.Count == 0 &&
            missingFiles.Count == 0;
    }
}
