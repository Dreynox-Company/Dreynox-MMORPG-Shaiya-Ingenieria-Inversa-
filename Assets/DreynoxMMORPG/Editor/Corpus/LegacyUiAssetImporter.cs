using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    public static class LegacyUiAssetImporter
    {
        public const string LocalRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated";

        public const string LoginRoot =
            LocalRoot + "/Interface/Login";

        private static readonly string[] LoginKeys =
        {
            "login.background",
            "login.logo",
            "login.check"
        };

        [MenuItem("Dreynox MMORPG/Client Parity/Import Canonical Login UI")]
        public static void ImportCanonicalLoginUi()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null)
                throw new InvalidOperationException(
                    "No canonical corpus selected. Use Client Parity > " +
                    "Canonical ps0032 Corpus first.");

            CorpusValidationResult validation = corpus.Validate();
            if (!validation.IsCanonical)
                throw new InvalidOperationException(
                    "The selected corpus is not the canonical ps0032 baseline.");

            EnsureAssetFolder(LoginRoot);

            for (int i = 0; i < LoginKeys.Length; i++)
            {
                string key = LoginKeys[i];
                string relative = LegacyUiReferenceManifest.Paths[key];
                string source = ResolveCaseInsensitive(corpus.RootPath, relative);

                if (!File.Exists(source))
                    throw new FileNotFoundException(
                        "Canonical UI asset missing: " + relative,
                        source);

                string fileName =
                    Path.GetFileName(source).ToLowerInvariant();

                string destination =
                    LoginRoot + "/" + fileName;

                string absoluteDestination =
                    Path.GetFullPath(destination);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(absoluteDestination));

                File.Copy(
                    source,
                    absoluteDestination,
                    true);

                AssetDatabase.ImportAsset(
                    destination,
                    ImportAssetOptions.ForceSynchronousImport);

                ConfigureSprite(destination);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "Dreynox MMORPG: canonical Login UI imported locally from " +
                CanonicalClientCorpus.BaselineId + ".");
        }

        public static Sprite LoadLoginSprite(string fileName)
        {
            string assetPath =
                LoginRoot + "/" + fileName.ToLowerInvariant();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static string ResolveCaseInsensitive(
            string root,
            string relativePath)
        {
            string current = root;
            string[] parts = relativePath
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!Directory.Exists(current))
                    return Path.Combine(current, parts[i]);

                string[] entries = Directory.GetFileSystemEntries(current);
                string match = null;

                for (int j = 0; j < entries.Length; j++)
                {
                    if (string.Equals(
                            Path.GetFileName(entries[j]),
                            parts[i],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        match = entries[j];
                        break;
                    }
                }

                current = match ?? Path.Combine(current, parts[i]);
            }

            return current;
        }

        private static void ConfigureSprite(string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] parts =
                assetPath.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
