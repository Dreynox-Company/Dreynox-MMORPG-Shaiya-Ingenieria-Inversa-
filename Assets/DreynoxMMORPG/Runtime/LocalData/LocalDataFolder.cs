using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>Read-only, bounded access to a user-selected extracted DATA folder. Never executes game.exe.</summary>
    public sealed class LocalDataFolder
    {
        public string Root { get; }
        private readonly Dictionary<string, string> resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public LocalDataFolder(string selectedFolder)
        {
            if (string.IsNullOrWhiteSpace(selectedFolder)) throw new ArgumentException("Seleccione la carpeta de Shaiya o DATA.");
            string root = Path.GetFullPath(selectedFolder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            RejectLink(root);
            string leaf = new DirectoryInfo(root).Name;
            if (!IsDataName(leaf))
            {
                string match = null;
                foreach (string child in Directory.GetDirectories(root))
                    if (IsDataName(Path.GetFileName(child)))
                    {
                        if (match != null) throw new IOException("Hay varias carpetas DATA. Seleccione una explícitamente.");
                        RejectLink(child); match = child;
                    }
                root = match ?? throw new DirectoryNotFoundException("No se encontró DATA_Español, DATA_Espanol o DATA.");
            }
            Root = root;
            Resolve("character");
        }
        private static bool IsDataName(string value) =>
            value.Equals("DATA", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("DATA_Español", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("DATA_Espanol", StringComparison.OrdinalIgnoreCase);

        public string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0)
                throw new ArgumentException("Se requiere una ruta relativa dentro de DATA.", nameof(relative));
            string[] parts = relative.Replace('\\', '/').Split('/');
            int start = IsDataName(parts[0]) ? 1 : 0;
            string current = Root;
            for (int i = start; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(new[] {'*', '?', '\0'}) >= 0)
                    throw new ArgumentException("Componente de ruta no permitido.", nameof(relative));
                string key = Path.Combine(current, part);
                if (!resolved.TryGetValue(key, out string next))
                {
                    string found = null;
                    foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                        if (Path.GetFileName(entry).Equals(part, StringComparison.OrdinalIgnoreCase))
                        {
                            if (found != null) throw new IOException("Nombre ambiguo por mayúsculas/minúsculas: " + part);
                            found = entry;
                        }
                    next = found ?? throw new FileNotFoundException("Recurso ausente: " + relative);
                    resolved[key] = next;
                }
                RejectLink(next);
                current = next;
            }
            return current;
        }
        public byte[] Read(string relative, CancellationToken token, int maximumBytes = 32 * 1024 * 1024)
        {
            token.ThrowIfCancellationRequested();
            string file = Resolve(relative);
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > maximumBytes)
                    throw new InvalidDataException("Tamaño de recurso fuera del límite: " + relative);
                var bytes = new byte[(int)stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    token.ThrowIfCancellationRequested();
                    int n = stream.Read(bytes, read, Math.Min(65536, bytes.Length - read));
                    if (n == 0) throw new EndOfStreamException(relative);
                    read += n;
                }
                return bytes;
            }
        }
        private static void RejectLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("No se siguen enlaces o junctions del corpus: " + path);
        }
    }
}
