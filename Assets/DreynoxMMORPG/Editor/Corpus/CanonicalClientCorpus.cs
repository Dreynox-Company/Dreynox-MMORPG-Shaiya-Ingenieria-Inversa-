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
        public const string GameExeSha256 = "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d";
        public const string InnerZipSha256 = "78136f45ee45d3b0c6e03b829412189cab4d32ae5670a8d8b65892154673cfd5";
        public const string CorpusRootEnvironmentVariable = "DREYNOX_CORPUS_ROOT";
        private const string EditorPrefsKey = "Dreynox.Mmorpg.CanonicalCorpusRoot";
        public string RootPath { get; }
        public string DataRootPath { get; }
        public string GameExePath => Path.Combine(IsSelectedData ? Directory.GetParent(RootPath).FullName : RootPath, "game.exe");
        private bool IsSelectedData => CanonicalCorpusPaths.IsDataName(new DirectoryInfo(RootPath).Name);

        public CanonicalClientCorpus(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Corpus root is required.");
            RootPath = Path.GetFullPath(rootPath);
            DataRootPath = CanonicalCorpusPaths.FindDataRoot(RootPath);
        }

        public static string StoredRoot
        {
            get
            {
                string environment = Environment.GetEnvironmentVariable(CorpusRootEnvironmentVariable);
                return string.IsNullOrWhiteSpace(environment)
                    ? EditorPrefs.GetString(EditorPrefsKey, string.Empty) : Path.GetFullPath(environment);
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) EditorPrefs.DeleteKey(EditorPrefsKey);
                else EditorPrefs.SetString(EditorPrefsKey, Path.GetFullPath(value));
            }
        }

        public static CanonicalClientCorpus FromStoredRoot()
        {
            string root = StoredRoot;
            return string.IsNullOrWhiteSpace(root) ? null : new CanonicalClientCorpus(root);
        }

        public CorpusValidationResult Validate()
        {
            var result = new CorpusValidationResult { baselineId = BaselineId, rootPath = RootPath };
            // Assets may be imported without copying game.exe beside DATA. Do not equate
            // matching content anchors with verification of a native executable or the full tree.
            CheckHash(result, "DATA_Español/excelxml/wingposition.xml", "8a2c376c898bb025550b5fe34b92a40dbbbb9e39063619cfee4756006908cd03");
            CheckHash(result, "DATA_Español/world/Login.wld", "f5508581e39ab01db155432de44fd5eebba240bd49a3d491f4564fbb45f03365");
            CheckHash(result, "DATA_Español/character/human/ani6/humf_019_select.ani", "8786f0ecb423c2cd4446d862a8f34433da99b59c29720ddd7ef38681048c50d8");
            result.contentAnchorsVerified = result.errors.Count == 0 && result.missingFiles.Count == 0;
            RequireFile(result, "DATA_Español/character/wing/wing.mon");
            RequireFile(result, "DATA_Español/interface/Login/BG.tga");
            if (File.Exists(GameExePath))
            {
                result.gameExeBytes = new FileInfo(GameExePath).Length;
                result.gameExeSha256 = FileFingerprint.Sha256(GameExePath);
                result.referenceExecutableVerified = result.gameExeBytes == GameExeBytes &&
                    string.Equals(result.gameExeSha256, GameExeSha256, StringComparison.OrdinalIgnoreCase);
                if (!result.referenceExecutableVerified) result.errors.Add("Present game.exe is not the pinned ps0032 reference.");
            }
            else result.warnings.Add("Assets-only corpus: game.exe is absent. Native executable identity is not verified here.");
            return result;
        }

        public string Resolve(string relativePath) => CanonicalCorpusPaths.Resolve(RootPath, relativePath);

        private void RequireFile(CorpusValidationResult result, string path)
        {
            if (File.Exists(Resolve(path))) result.presentFiles.Add(path); else result.missingFiles.Add(path);
        }
        private void CheckHash(CorpusValidationResult result, string path, string expected)
        {
            string resolved = Resolve(path);
            if (!File.Exists(resolved)) { result.missingFiles.Add(path); return; }
            string actual = FileFingerprint.Sha256(resolved);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) result.errors.Add("Content anchor mismatch: " + path);
            else result.presentFiles.Add(path);
        }
    }

    [Serializable]
    public sealed class CorpusValidationResult
    {
        public string baselineId;
        public string rootPath;
        public long gameExeBytes;
        public string gameExeSha256;
        public bool contentAnchorsVerified;
        public bool referenceExecutableVerified;
        public readonly List<string> presentFiles = new List<string>();
        public readonly List<string> missingFiles = new List<string>();
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        // Compatibility name for existing import gates; this asserts content anchors only.
        public bool IsCanonical => contentAnchorsVerified && errors.Count == 0 && missingFiles.Count == 0;
    }
}
