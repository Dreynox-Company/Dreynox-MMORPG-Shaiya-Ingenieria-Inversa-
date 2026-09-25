using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.ParityCore;
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

        public const string CharacterSelectRoot =
            LocalRoot + "/Interface/CharacterSelect";

        public const string CharacterMakeRoot =
            LocalRoot + "/Interface/CharacterMake";

        public const string CharacterMakeAppearanceRoot =
            CharacterMakeRoot + "/Appearance";

        private static readonly string[] LoginKeys =
        {
            "login.background",
            "login.logo",
            "login.check"
        };

        private static readonly string[] CharacterSelectKeys =
        {
            "character.select.background",
            "character.select.buttons",
            "character.select.start",
            "character.select.restore",
            "character.select.slotAtlas",
            "character.select.startUsa"
        };

        private static readonly string[] CharacterMakeKeys =
        {
            "character.make.background",
            "character.make.modeBackground",
            "character.make.basicInfo",
            "character.make.appearance",
            "character.make.tab",
            "character.make.nav.left",
            "character.make.nav.right",
            "character.make.nav.play",
            "character.make.nav.stop",
            "character.make.nav.zoomIn",
            "character.make.nav.zoomOut",
            "character.make.name",
            "character.make.sex.male",
            "character.make.sex.female",
            "character.make.mode.basic",
            "character.make.mode.ultimate",
            "character.make.infoFrame",
            "character.make.classInfo.background",
            "character.make.classInfo.fighterBars",
            "character.make.classInfo.defenderBars",
            "character.make.classInfo.priestBars",
            "character.make.classInfo.rangerBars",
            "character.make.classInfo.archerBars",
            "character.make.classInfo.mageBars",
            "character.make.class.fighter",
            "character.make.class.defender",
            "character.make.class.priest",
            "character.make.class.ranger",
            "character.make.class.archer",
            "character.make.class.mage",
            "character.make.sex.maleAtlas",
            "character.make.sex.femaleAtlas",
            "character.make.weapon.oneHandSword.icon",
            "character.make.weapon.oneHandSword.text",
            "character.make.weapon.twoHandSword.icon",
            "character.make.weapon.twoHandSword.text",
            "character.make.weapon.dualSword.icon",
            "character.make.weapon.dualSword.text",
            "character.make.weapon.spear.icon",
            "character.make.weapon.spear.text",
            "character.make.weapon.oneHandBlunt.icon",
            "character.make.weapon.oneHandBlunt.text",
            "character.make.weapon.twoHandBlunt.icon",
            "character.make.weapon.twoHandBlunt.text",
            "character.make.weapon.shield.icon",
            "character.make.weapon.shield.text",
            "character.make.weapon.oneHandAxe.icon",
            "character.make.weapon.oneHandAxe.text",
            "character.make.weapon.twoHandAxe.icon",
            "character.make.weapon.twoHandAxe.text",
            "character.make.weapon.dualAxe.icon",
            "character.make.weapon.dualAxe.text",
            "character.make.weapon.reversedSword.icon",
            "character.make.weapon.reversedSword.text",
            "character.make.weapon.dagger.icon",
            "character.make.weapon.dagger.text",
            "character.make.weapon.knuckle.icon",
            "character.make.weapon.knuckle.text",
            "character.make.weapon.bow.icon",
            "character.make.weapon.bow.text",
            "character.make.weapon.crossbow.icon",
            "character.make.weapon.crossbow.text",
            "character.make.weapon.throwingWeapon.icon",
            "character.make.weapon.throwingWeapon.text",
            "character.make.weapon.staff.icon",
            "character.make.weapon.staff.text",
            "character.make.classInfo.soloPartyText",
            "character.make.classInfo.atkDefText"
        };

        [MenuItem("Dreynox MMORPG/Client Parity/Import Canonical Login UI")]
        public static void ImportCanonicalLoginUi()
        {
            ImportGroup(LoginRoot, LoginKeys);
        }

        [MenuItem("Dreynox MMORPG/Client Parity/Import Canonical Character Select UI")]
        public static void ImportCanonicalCharacterSelectUi()
        {
            ImportGroup(CharacterSelectRoot, CharacterSelectKeys);
        }

        [MenuItem("Dreynox MMORPG/Client Parity/Import Canonical Character Make UI")]
        public static void ImportCanonicalCharacterMakeUi()
        {
            ImportGroup(
                CharacterMakeRoot,
                CharacterMakeKeys);

            ImportCharacterMakeAppearanceThumbnails();
        }

        public static Sprite LoadLoginSprite(string fileName)
        {
            return LoadSprite(LoginRoot, fileName);
        }

        public static Sprite LoadCharacterSelectSprite(string fileName)
        {
            return LoadSprite(CharacterSelectRoot, fileName);
        }

        public static Sprite LoadCharacterMakeSprite(string fileName)
        {
            return LoadSprite(CharacterMakeRoot, fileName);
        }

        public static Texture2D LoadLoginTexture(string fileName)
        {
            return LoadTexture(LoginRoot, fileName);
        }

        public static Texture2D LoadCharacterSelectTexture(string fileName)
        {
            return LoadTexture(CharacterSelectRoot, fileName);
        }

        public static Texture2D LoadCharacterMakeTexture(string fileName)
        {
            return LoadTexture(CharacterMakeRoot, fileName);
        }

        public static Texture2D LoadCharacterMakeAppearanceTexture(
            int family,
            int sex,
            bool face,
            int variant)
        {
            string fileName =
                LegacyCharacterAppearanceUiCore
                    .ResolveThumbnailFileName(
                        family,
                        sex,
                        face,
                        variant);

            return LoadTexture(
                CharacterMakeAppearanceRoot,
                fileName);
        }

        public static Texture2D LoadCharacterMakeTextureByKey(string key)
        {
            string relative;
            if (!LegacyUiReferenceManifest.Paths.TryGetValue(
                    key,
                    out relative))
            {
                throw new KeyNotFoundException(
                    "UI manifest key not found: " + key);
            }

            return LoadTexture(
                CharacterMakeRoot,
                DestinationFileName(
                    key,
                    relative));
        }

        public static string ResolveCaseInsensitive(
            string root,
            string relativePath)
        {
            return CanonicalCorpusPaths.Resolve(root, relativePath);
        }

        private static void ImportCharacterMakeAppearanceThumbnails()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null)
            {
                throw new InvalidOperationException(
                    "No canonical corpus selected.");
            }

            CorpusValidationResult validation =
                corpus.Validate();

            if (!validation.IsCanonical)
            {
                throw new InvalidOperationException(
                    "The selected corpus is not the canonical ps0032 baseline.");
            }

            string sourceRoot =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/interface/CharacterMake/appearance");

            if (!Directory.Exists(
                    sourceRoot))
            {
                throw new DirectoryNotFoundException(
                    "Canonical CharacterMake appearance folder missing: " +
                    sourceRoot);
            }

            EnsureAssetFolder(
                CharacterMakeAppearanceRoot);

            for (int family = 0;
                 family < 4;
                 family++)
            {
                for (int sex = 0;
                     sex < 2;
                     sex++)
                {
                    for (int variant = 0;
                         variant <
                            LegacyCharacterAppearanceUiCore.VariantCount;
                         variant++)
                    {
                        ImportAppearanceThumbnail(
                            sourceRoot,
                            family,
                            sex,
                            true,
                            variant);

                        ImportAppearanceThumbnail(
                            sourceRoot,
                            family,
                            sex,
                            false,
                            variant);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ImportAppearanceThumbnail(
            string sourceRoot,
            int family,
            int sex,
            bool face,
            int variant)
        {
            string fileName =
                LegacyCharacterAppearanceUiCore
                    .ResolveThumbnailFileName(
                        family,
                        sex,
                        face,
                        variant);

            string source =
                ResolveCaseInsensitive(
                    sourceRoot,
                    fileName);

            if (!File.Exists(
                    source))
            {
                throw new FileNotFoundException(
                    "Canonical CharacterMake appearance thumbnail missing: " +
                    fileName,
                    source);
            }

            string destination =
                CharacterMakeAppearanceRoot +
                "/" +
                fileName.ToLowerInvariant();

            string absoluteDestination =
                Path.GetFullPath(
                    destination);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    absoluteDestination));

            File.Copy(
                source,
                absoluteDestination,
                true);

            AssetDatabase.ImportAsset(
                destination,
                ImportAssetOptions.ForceSynchronousImport);

            ConfigureTexture(
                destination);
        }

        private static void ImportGroup(
            string destinationRoot,
            IReadOnlyList<string> keys)
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null)
                throw new InvalidOperationException(
                    "No canonical corpus selected. Use Client Parity > " +
                    "Canonical ps0032 Corpus first.");

            CorpusValidationResult validation =
                corpus.Validate();

            if (!validation.IsCanonical)
                throw new InvalidOperationException(
                    "The selected corpus is not the canonical ps0032 baseline.");

            EnsureAssetFolder(destinationRoot);

            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];

                string relative;
                if (!LegacyUiReferenceManifest.Paths.TryGetValue(
                        key,
                        out relative))
                {
                    throw new KeyNotFoundException(
                        "UI manifest key not found: " + key);
                }

                string source =
                    ResolveCaseInsensitive(
                        corpus.RootPath,
                        relative);

                if (!File.Exists(source))
                    throw new FileNotFoundException(
                        "Canonical UI asset missing: " + relative,
                        source);

                string fileName =
                    DestinationFileName(
                        key,
                        source)
                        .ToLowerInvariant();

                string destination =
                    destinationRoot + "/" + fileName;

                string absoluteDestination =
                    Path.GetFullPath(destination);

                string directory =
                    Path.GetDirectoryName(absoluteDestination);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(
                    source,
                    absoluteDestination,
                    true);

                AssetDatabase.ImportAsset(
                    destination,
                    ImportAssetOptions.ForceSynchronousImport);

                ConfigureTexture(destination);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "Dreynox MMORPG: canonical UI group imported locally to " +
                destinationRoot + ".");
        }

        private static Sprite LoadSprite(
            string root,
            string fileName)
        {
            string assetPath =
                root + "/" + fileName.ToLowerInvariant();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Texture2D LoadTexture(
            string root,
            string fileName)
        {
            string assetPath =
                root + "/" + fileName.ToLowerInvariant();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static string DestinationFileName(
            string key,
            string sourcePath)
        {
            string extension =
                Path.GetExtension(
                    sourcePath);

            if (string.IsNullOrWhiteSpace(
                    extension))
                extension = ".tga";

            bool requiresAlias =
                key.StartsWith(
                    "character.make.class.",
                    StringComparison.Ordinal) ||
                key.StartsWith(
                    "character.make.classInfo.",
                    StringComparison.Ordinal) ||
                key.StartsWith(
                    "character.make.weapon.",
                    StringComparison.Ordinal) ||
                key.EndsWith(
                    "Atlas",
                    StringComparison.Ordinal) ||
                string.Equals(
                    key,
                    "character.make.infoFrame",
                    StringComparison.Ordinal);

            if (!requiresAlias)
                return Path.GetFileName(
                    sourcePath);

            string prefix =
                "character.make.";

            string value =
                key.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                    ? key.Substring(
                        prefix.Length)
                    : key;

            return value
                .Replace('.', '_')
                .Replace('/', '_')
                .Replace('\\', '_') +
                extension.ToLowerInvariant();
        }

        private static void ConfigureTexture(string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath)
                    as TextureImporter;

            if (importer == null)
                return;

            importer.textureType =
                TextureImporterType.Sprite;

            importer.spriteImportMode =
                SpriteImportMode.Single;

            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(
            string assetPath)
        {
            string[] parts =
                assetPath.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
