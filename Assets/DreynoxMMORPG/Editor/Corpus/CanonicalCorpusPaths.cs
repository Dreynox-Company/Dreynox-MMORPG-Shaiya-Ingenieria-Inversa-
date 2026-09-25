using System;
using System.IO;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    /// <summary>Map logical DATA paths to an explicitly selected, read-only extracted tree.</summary>
    public static class CanonicalCorpusPaths
    {
        public static bool IsDataName(string name) =>
            string.Equals(name, "DATA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "DATA_Español", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "DATA_Espanol", StringComparison.OrdinalIgnoreCase);

        public static string FindDataRoot(string selected)
        {
            if (string.IsNullOrWhiteSpace(selected)) throw new ArgumentException("Select a corpus folder.");
            string full = Path.GetFullPath(selected);
            if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
            RejectLink(full);
            if (IsDataName(new DirectoryInfo(full).Name)) return full;
            string found = null;
            foreach (string child in Directory.EnumerateDirectories(full))
            {
                if (!IsDataName(Path.GetFileName(child))) continue;
                RejectLink(child);
                if (found != null) throw new IOException("Multiple DATA folders; select one explicitly.");
                found = child;
            }
            return found ?? throw new DirectoryNotFoundException("No DATA, DATA_Español or DATA_Espanol at selected root.");
        }

        public static string Resolve(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative) ||
                Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0)
                throw new ArgumentException("An explicit root and a relative resource path are required.");
            string[] parts = relative.Replace('\\', '/').Split('/');
            foreach (string part in parts)
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(new[] {'*','?','\0'}) >= 0)
                    throw new ArgumentException("Invalid resource path segment.");
            string current = Path.GetFullPath(root);
            RejectLink(current);
            int start = 0;
            if (IsDataName(parts[0])) { current = FindDataRoot(current); start = 1; }
            for (int i = start; i < parts.Length; i++)
            {
                string found = null;
                if (Directory.Exists(current))
                    foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                    {
                        if (!Path.GetFileName(entry).Equals(parts[i], StringComparison.OrdinalIgnoreCase)) continue;
                        if (found != null) throw new IOException("Ambiguous case for resource " + relative);
                        found = entry;
                    }
                current = found ?? Path.Combine(current, parts[i]);
                if (File.Exists(current) || Directory.Exists(current)) RejectLink(current);
            }
            return current;
        }

        private static void RejectLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Corpus links/junctions are not followed: " + path);
        }
    }
}
