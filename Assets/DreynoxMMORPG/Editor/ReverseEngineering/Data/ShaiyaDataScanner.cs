using System;
using System.Collections.Generic;
using System.IO;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Data
{
    [Serializable]
    public sealed class ScannedAsset
    {
        public string absolutePath;
        public string relativePath;
        public long bytes;
        public ShaiyaAssetKind kind;
    }

    public static class ShaiyaDataScanner
    {
        public static List<ScannedAsset> Scan(string root)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            string fullRoot = Path.GetFullPath(root);
            List<ScannedAsset> result = new List<ScannedAsset>();
            foreach (string path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                FileInfo info = new FileInfo(path);
                result.Add(new ScannedAsset
                {
                    absolutePath = info.FullName,
                    relativePath = Path.GetRelativePath(fullRoot, info.FullName).Replace('\\', '/'),
                    bytes = info.Length,
                    kind = ShaiyaAssetClassifier.Classify(info.FullName)
                });
            }
            result.Sort((a,b) => string.Compare(a.relativePath, b.relativePath, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}
