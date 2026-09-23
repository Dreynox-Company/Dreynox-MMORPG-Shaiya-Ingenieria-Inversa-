using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public static class ZstdCliDecoder
    {
        private static readonly byte[] Magic = { 0x28, 0xB5, 0x2F, 0xFD };

        public static byte[] Decompress(byte[] packed, int expectedBytes, string preferredExecutable = null)
        {
            if (packed == null) throw new ArgumentNullException(nameof(packed));
            if (packed.Length < 4 || packed[0] != Magic[0] || packed[1] != Magic[1] || packed[2] != Magic[2] || packed[3] != Magic[3])
                throw new InvalidDataException("El índice descifrado no contiene un frame Zstandard.");

            string executable = ResolveExecutable(preferredExecutable);
            if (string.IsNullOrWhiteSpace(executable))
                throw new FileNotFoundException("No se encontró zstd.exe. Configure ZSTD_EXE, seleccione el ejecutable en el inspector o colóquelo en Tools/zstd/zstd.exe. Solo se usa durante la conversión en Editor.");

            string tempRoot = Path.Combine(Path.GetTempPath(), "DreynoxMMORPG", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string input = Path.Combine(tempRoot, "index.zst");
            string output = Path.Combine(tempRoot, "index.bin");
            try
            {
                File.WriteAllBytes(input, packed);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-d -f --no-progress \"" + input + "\" -o \"" + output + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (Process process = Process.Start(psi))
                {
                    if (process == null) throw new InvalidOperationException("No se pudo iniciar zstd.");
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(120000))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("zstd excedió 120 segundos.");
                    }
                    if (process.ExitCode != 0) throw new InvalidDataException("zstd falló: " + (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr));
                }
                if (!File.Exists(output)) throw new FileNotFoundException("zstd no produjo el archivo decodificado.", output);
                byte[] decoded = File.ReadAllBytes(output);
                if (expectedBytes > 0 && decoded.Length != expectedBytes)
                    throw new InvalidDataException($"Zstandard produjo {decoded.Length:N0} bytes; se esperaban {expectedBytes:N0}.");
                return decoded;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        public static string ResolveExecutable(string preferredExecutable = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredExecutable) && File.Exists(preferredExecutable)) return Path.GetFullPath(preferredExecutable);
            string env = Environment.GetEnvironmentVariable("ZSTD_EXE");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return Path.GetFullPath(env);
            string projectLocal = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "zstd", "zstd.exe"));
            if (File.Exists(projectLocal)) return projectLocal;
            return ResolveFromPath();
        }

        private static string ResolveFromPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string raw in path.Split(Path.PathSeparator))
            {
                string dir = raw.Trim().Trim('"');
                if (dir.Length == 0) continue;
                try
                {
                    string candidate = Path.Combine(dir, "zstd.exe");
                    if (File.Exists(candidate)) return candidate;
                    candidate = Path.Combine(dir, "zstd");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }
    }
}
