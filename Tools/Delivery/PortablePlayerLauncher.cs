using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dreynox.Delivery
{
    internal static class PortablePlayerLauncher
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool verifyOnly = args.Length == 1 && args[0] == "--verify-only";
            if (args.Length != 0 && !verifyOnly) return 2;
            if (verifyOnly)
            {
                try { Prepare(null); return 0; }
                catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var window = new Form())
            {
                window.Text = "Dreynox MMORPG — preparando el juego";
                window.ClientSize = new Size(480, 120);
                window.FormBorderStyle = FormBorderStyle.FixedDialog;
                window.MaximizeBox = false;
                window.MinimizeBox = false;
                window.StartPosition = FormStartPosition.CenterScreen;
                var title = new Label { Text = "Verificando los archivos del juego...", AutoSize = false, Left = 20, Top = 18, Width = 440, Height = 28 };
                var progress = new ProgressBar { Left = 20, Top = 56, Width = 440, Height = 20 };
                var detail = new Label { Text = "No requiere Unity ni modifica la carpeta DATA.", AutoSize = true, Left = 20, Top = 86 };
                window.Controls.Add(title); window.Controls.Add(progress); window.Controls.Add(detail);
                bool completed = false;
                window.FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!completed) e.Cancel = true; };
                window.Shown += async delegate
                {
                    try
                    {
                        Action<int> report = delegate(int value)
                        { if (!window.IsDisposed) window.BeginInvoke((Action)delegate { progress.Value = Math.Max(0, Math.Min(100, value)); }); };
                        string game = await Task.Run(delegate { return Prepare(report); });
                        title.Text = "Iniciando Dreynox MMORPG...";
                        var start = new ProcessStartInfo(game);
                        start.WorkingDirectory = Path.GetDirectoryName(game);
                        start.UseShellExecute = false;
                        using (Process child = Process.Start(start))
                        { if (child == null) throw new InvalidOperationException("Windows no pudo iniciar el juego."); }
                        Environment.ExitCode = 0;
                    }
                    catch (Exception ex)
                    {
                        Environment.ExitCode = 1;
                        MessageBox.Show(window, "No se ha iniciado el juego.\n\n" + ex.Message,
                            "Dreynox — comprobación del paquete", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    finally { completed = true; window.Close(); }
                };
                Application.Run(window);
            }
            return Environment.ExitCode;
        }
        private static Stream Payload()
        {
            Stream value = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dreynox.PlayerPayload");
            if (value == null) throw new InvalidDataException("El ejecutable no contiene el juego.");
            return value;
        }
        private static string Prepare(Action<int> report)
        {
            // Identity is embedded in this PE, never downloaded or read from cache.
            using (Stream payload = Payload())
                if (VerifiedPlayerArchive.Sha256(payload) != PayloadIdentity.Sha256)
                    throw new InvalidDataException("El paquete está incompleto o dañado. No se ejecutó ningún archivo.");
            string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dreynox", "PlayableClient");
            VerifiedPlayerArchive.RejectReparseAncestors(parent);
            Directory.CreateDirectory(parent);
            string target = Path.Combine(parent, PayloadIdentity.Sha256);
            bool acquired = false;
            using (var mutex = new Mutex(false, "Local\\DreynoxPlayer-" + PayloadIdentity.Sha256))
            {
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromMinutes(10)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Otra preparación sigue en curso. Inténtalo al terminar.");
                    if (Directory.Exists(target))
                    {
                        using (Stream payload = Payload()) VerifiedPlayerArchive.VerifyInstalled(payload, target);
                        if (report != null) report(100);
                    }
                    else
                    {
                        string staging = Path.Combine(parent, ".preparing-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(staging);
                        // Failed staging is retained; do not recursively delete a path
                        // that another local process could replace.
                        using (Stream payload = Payload()) VerifiedPlayerArchive.ExtractAndVerify(payload, staging, report);
                        Directory.Move(staging, target);
                    }
                    string game = Path.Combine(target, VerifiedPlayerArchive.GameExecutable);
                    if (!File.Exists(game)) throw new FileNotFoundException("El juego verificado no está presente.", game);
                    return game;
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }
    }
}
