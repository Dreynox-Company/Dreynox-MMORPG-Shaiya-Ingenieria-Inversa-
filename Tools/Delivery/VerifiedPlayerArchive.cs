using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Dreynox.Delivery
{
    // C# 5 / Windows Framework. No shell, network, elevation or original DATA access.
    public static class VerifiedPlayerArchive
    {
        public const string GameExecutable = "DreynoxMmorpg-Map1.exe";
        public const string ManifestName = "SHA256SUMS.txt";
        private const long MaximumTotalBytes = 8L * 1024 * 1024 * 1024;
        private const long MaximumEntryBytes = 2L * 1024 * 1024 * 1024;
        private const int MaximumFiles = 50000;
        public static string SafeRelativeName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 220 || name.IndexOf('\\') >= 0 || name[0] == '/')
                throw new InvalidDataException("Invalid package path.");
            foreach (string part in name.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal))
                    throw new InvalidDataException("Ambiguous package path: " + name);
                foreach (char c in part)
                    if (c < 32 || "<>:\"|?*".IndexOf(c) >= 0)
                        throw new InvalidDataException("Unsupported package path: " + name);
                string stem = part.Split('.')[0].ToUpperInvariant();
                if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" ||
                    (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                     "123456789\u00b9\u00b2\u00b3".IndexOf(stem[3]) >= 0))
                    throw new InvalidDataException("Reserved Windows path: " + name);
            }
            return name;
        }
        public static string Sha256(Stream input) { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(input)); }
        public static string Hex(byte[] value) { return BitConverter.ToString(value).Replace("-", "").ToLowerInvariant(); }
        public static void ExtractAndVerify(Stream input, string destination, Action<int> progress)
        {
            PlayerZipMetadata.Validate(input);
            string root = Path.GetFullPath(destination);
            if (!Directory.Exists(root) || Directory.GetFileSystemEntries(root).Length != 0)
                throw new InvalidOperationException("Extraction requires a new empty directory.");
            RejectReparseAncestors(root);
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read, true))
            {
                var files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    { SafeRelativeName(entry.FullName.TrimEnd('/')); continue; }
                    string name = SafeRelativeName(entry.FullName);
                    if (files.ContainsKey(name)) throw new InvalidDataException("Duplicate package path: " + name);
                    if (entry.Length < 0 || entry.Length > MaximumEntryBytes)
                        throw new InvalidDataException("Package entry exceeds its size budget.");
                    total = checked(total + entry.Length);
                    if (total > MaximumTotalBytes || files.Count >= MaximumFiles)
                        throw new InvalidDataException("Package exceeds its total size budget.");
                    files.Add(name, entry);
                }
                if (!files.ContainsKey(GameExecutable) || !files.ContainsKey("UnityPlayer.dll") || !files.ContainsKey(ManifestName) || !HasDataFolder(files))
                    throw new InvalidDataException("This is not a complete Map1 Unity Player.");
                var expected = ReadManifest(files[ManifestName]);
                if (expected.Count != files.Count - 1) throw new InvalidDataException("The Player manifest does not cover the entire payload.");
                foreach (string name in files.Keys)
                    if (name != ManifestName && !expected.ContainsKey(name)) throw new InvalidDataException("Unverified Player file: " + name);
                int complete = 0; byte[] buffer = new byte[128 * 1024];
                foreach (var pair in files)
                {
                    string path = Path.Combine(root, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                    string directory = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(directory); RejectReparseAncestors(directory);
                    long copied = 0;
                    using (var source = pair.Value.Open())
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        int count;
                        while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            copied = checked(copied + count);
                            if (copied > pair.Value.Length) throw new InvalidDataException("Payload size mismatch.");
                            output.Write(buffer, 0, count);
                        }
                        output.Flush(true);
                    }
                    if (copied != pair.Value.Length) throw new InvalidDataException("Truncated Player file.");
                    if (pair.Key != ManifestName) VerifyFile(path, expected[pair.Key]);
                    complete++; if (progress != null) progress((int)(100L * complete / files.Count));
                }
            }
        }
        public static void VerifyInstalled(Stream originalPayload, string directory)
        {
            PlayerZipMetadata.Validate(originalPayload);
            string root = Path.GetFullPath(directory); RejectReparseAncestors(root);
            Dictionary<string, string> expected;
            using (var archive = new ZipArchive(originalPayload, ZipArchiveMode.Read, true))
            {
                ZipArchiveEntry manifest = archive.GetEntry(ManifestName);
                if (manifest == null) throw new InvalidDataException("Embedded manifest is missing.");
                expected = ReadManifest(manifest);
            }
            if (!expected.ContainsKey(GameExecutable) || !expected.ContainsKey("UnityPlayer.dll"))
                throw new InvalidDataException("Installed Player manifest is incomplete.");
            RejectUnexpectedInstalledFiles(root, expected);
            foreach (var pair in expected)
            {
                string path = Path.Combine(root, SafeRelativeName(pair.Key).Replace('/', Path.DirectorySeparatorChar));
                RejectReparseAncestors(path); VerifyFile(path, pair.Value);
            }
        }
        private static void RejectUnexpectedInstalledFiles(string root, Dictionary<string, string> expected)
        {
            var pending = new Stack<string>(); pending.Push(root); int entries = 0;
            while (pending.Count > 0)
            {
                string directory = pending.Pop(); RejectReparseAncestors(directory);
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > MaximumFiles * 2) throw new InvalidDataException("Installed cache exceeds its entry budget.");
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked entry in Player cache.");
                    if ((attributes & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                    string relative = path.Substring(root.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace('\\', '/');
                    SafeRelativeName(relative);
                    if (!String.Equals(relative, ManifestName, StringComparison.Ordinal) && !expected.ContainsKey(relative))
                        throw new InvalidDataException("Unexpected file in Player cache: " + relative);
                }
            }
        }
        public static void RejectReparseAncestors(string path)
        {
            for (string current = Path.GetFullPath(path); !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A linked path cannot be used for the Player cache: " + current);
        }
        private static void VerifyFile(string path, string expected)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                if (!String.Equals(Sha256(stream), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Player file checksum mismatch: " + Path.GetFileName(path));
        }
        private static bool HasDataFolder(Dictionary<string, ZipArchiveEntry> files)
        {
            foreach (string name in files.Keys) if (name.StartsWith("DreynoxMmorpg-Map1_Data/", StringComparison.Ordinal)) return true;
            return false;
        }
        private static Dictionary<string, string> ReadManifest(ZipArchiveEntry entry)
        {
            if (entry.Length > 8 * 1024 * 1024) throw new InvalidDataException("Manifest exceeds its budget.");
            using (var source = entry.Open()) using (var reader = new StreamReader(source, Encoding.UTF8, true)) return ParseManifest(reader.ReadToEnd());
        }
        private static Dictionary<string, string> ParseManifest(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StringReader(text))
            {
                string row;
                while ((row = reader.ReadLine()) != null)
                {
                    if (row.Length == 0) continue;
                    if (row.Length < 67 || row.Substring(64, 2) != "  ") throw new InvalidDataException("Invalid manifest row.");
                    string hash = row.Substring(0, 64);
                    foreach (char c in hash) if (!Uri.IsHexDigit(c)) throw new InvalidDataException("Invalid SHA-256.");
                    string name = SafeRelativeName(row.Substring(66));
                    if (String.Equals(name, ManifestName, StringComparison.OrdinalIgnoreCase) || result.ContainsKey(name))
                        throw new InvalidDataException("Duplicate/self-referential manifest entry.");
                    result.Add(name, hash);
                }
            }
            return result;
        }
    }
}
