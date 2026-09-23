using System;
using System.Collections.Generic;
using System.IO;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    public static class CanonicalResourceIndex
    {
        private sealed class Index
        {
            public readonly Dictionary<string, List<string>> ByFileName =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<string, Index> Cache =
            new Dictionary<string, Index>(
                StringComparer.OrdinalIgnoreCase);

        public static string FindUnique(
            string rootDirectory,
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException(
                    "Resource root is required.",
                    nameof(rootDirectory));

            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            string normalizedRoot =
                Path.GetFullPath(rootDirectory);

            if (!Directory.Exists(normalizedRoot))
                return null;

            Index index;
            if (!Cache.TryGetValue(
                    normalizedRoot,
                    out index))
            {
                index = Build(normalizedRoot);
                Cache.Add(normalizedRoot, index);
            }

            string key =
                Path.GetFileName(fileName.Trim());

            List<string> matches;
            if (!index.ByFileName.TryGetValue(
                    key,
                    out matches) ||
                matches.Count == 0)
            {
                return null;
            }

            if (matches.Count == 1)
                return matches[0];

            string extension =
                Path.GetExtension(fileName);

            if (!string.IsNullOrWhiteSpace(extension))
            {
                List<string> exactExtension =
                    matches.FindAll(
                        path =>
                            string.Equals(
                                Path.GetExtension(path),
                                extension,
                                StringComparison.OrdinalIgnoreCase));

                if (exactExtension.Count == 1)
                    return exactExtension[0];
            }

            throw new InvalidDataException(
                "Canonical resource name is ambiguous: '" +
                fileName + "'. Matches: " +
                string.Join(", ", matches));
        }

        public static void Clear()
        {
            Cache.Clear();
        }

        private static Index Build(string root)
        {
            var result = new Index();

            foreach (string path in
                     Directory.EnumerateFiles(
                         root,
                         "*",
                         SearchOption.AllDirectories))
            {
                string name =
                    Path.GetFileName(path);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                List<string> list;
                if (!result.ByFileName.TryGetValue(
                        name,
                        out list))
                {
                    list = new List<string>();
                    result.ByFileName.Add(name, list);
                }

                list.Add(path);
            }

            return result;
        }
    }
}
