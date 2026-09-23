using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Data;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Meshes;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Spk;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering
{
    public sealed class ShaiyaDataImporterWindow : EditorWindow
    {
        private enum Tab { DataInspector, SpkV3, MeshImporter }
        private Tab _tab;
        private string _dataFolder = string.Empty;
        private readonly List<ScannedAsset> _files = new List<ScannedAsset>();
        private Vector2 _scroll;
        private Vector2 _fileScroll;
        private string _search = string.Empty;
        private int _selected = -1;
        private string _hexDump = string.Empty;
        private string _spkPath = string.Empty;
        private string _profilePath = string.Empty;
        private SpkV3Header _spkHeader;
        private string _spkStatus = "Sin SPK seleccionado.";
        private string _meshSource = string.Empty;
        private LegacyMeshLayoutProfile _meshProfile;
        private string _meshOutput = "Assets/DreynoxMMORPG/Imported/Meshes/ImportedMesh.asset";

        [MenuItem("Dreynox MMORPG/Reverse Engineering/DATA SPK Inspector")]
        public static void Open()
        {
            ShaiyaDataImporterWindow window = GetWindow<ShaiyaDataImporterWindow>();
            window.titleContent = new GUIContent("Dreynox RE");
            window.minSize = new Vector2(920f, 650f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dreynox Mmorpg · Reverse Engineering", new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 });
            EditorGUILayout.LabelField("Fase 1 · Conversión offline de formatos legacy a assets nativos Unity");
            EditorGUILayout.Space(8);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "DATA Inspector", "SPK v3", "Mesh por perfil" });
            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tab == Tab.DataInspector) DrawDataInspector();
            else if (_tab == Tab.SpkV3) DrawSpk();
            else DrawMeshImporter();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDataInspector()
        {
            EditorGUILayout.HelpBox("El origen se abre solo en lectura. No se modifica DATA ni el cliente original.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("DATA", _dataFolder);
                if (GUILayout.Button("Seleccionar...", GUILayout.Width(120)))
                {
                    string p = EditorUtility.OpenFolderPanel("Seleccionar DATA", _dataFolder, string.Empty);
                    if (!string.IsNullOrWhiteSpace(p)) { _dataFolder = p; _files.Clear(); _selected = -1; }
                }
            }
            using (new EditorGUI.DisabledScope(!Directory.Exists(_dataFolder)))
            {
                if (GUILayout.Button("Escanear carpeta DATA", GUILayout.Height(30))) ScanData();
            }

            if (_files.Count == 0) return;
            long total = _files.Sum(x => x.bytes);
            EditorGUILayout.LabelField($"{_files.Count:N0} archivos · {FormatBytes(total)}");
            DrawKindSummary();
            _search = EditorGUILayout.TextField("Buscar", _search);
            _fileScroll = EditorGUILayout.BeginScrollView(_fileScroll, GUILayout.Height(260));
            int shown = 0;
            for (int i = 0; i < _files.Count && shown < 600; i++)
            {
                ScannedAsset f = _files[i];
                if (!string.IsNullOrWhiteSpace(_search) && f.relativePath.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                bool active = i == _selected;
                if (GUILayout.Toggle(active, $"[{f.kind}] {f.relativePath}  ·  {FormatBytes(f.bytes)}", "Button"))
                {
                    if (_selected != i) { _selected = i; InspectSelected(); }
                }
            }
            EditorGUILayout.EndScrollView();
            if (_selected >= 0 && _selected < _files.Count)
            {
                ScannedAsset f = _files[_selected];
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("SHA-256", FileFingerprint.Sha256(f.absolutePath));
                EditorGUILayout.TextArea(_hexDump, GUILayout.MinHeight(180));
            }
        }

        private void DrawKindSummary()
        {
            var groups = _files.GroupBy(x => x.kind).OrderByDescending(g => g.Count());
            StringBuilder b = new StringBuilder();
            foreach (var g in groups) b.Append(g.Key).Append(':').Append(g.Count()).Append("  ");
            EditorGUILayout.HelpBox(b.ToString(), MessageType.None);
        }

        private void ScanData()
        {
            try
            {
                _files.Clear();
                _files.AddRange(ShaiyaDataScanner.Scan(_dataFolder));
                _selected = _files.Count > 0 ? 0 : -1;
                InspectSelected();
            }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("Dreynox RE", ex.Message, "OK"); }
        }

        private void InspectSelected()
        {
            _hexDump = string.Empty;
            if (_selected < 0 || _selected >= _files.Count) return;
            string path = _files[_selected].absolutePath;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int count = (int)Math.Min(512, stream.Length);
                byte[] bytes = new byte[count];
                stream.Read(bytes, 0, count);
                _hexDump = HexDump(bytes);
            }
        }

        private void DrawSpk()
        {
            EditorGUILayout.HelpBox("Soporte actual: parseo exacto del header SPK v3 observado y descifrado autenticado del índice AES-GCM. El perfil de clave se mantiene local y fuera de Git.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("data.spk", _spkPath);
                if (GUILayout.Button("Seleccionar...", GUILayout.Width(120)))
                {
                    string p = EditorUtility.OpenFilePanel("Seleccionar data.spk", Path.GetDirectoryName(_spkPath), "spk");
                    if (!string.IsNullOrWhiteSpace(p)) { _spkPath = p; ParseSpkHeader(); }
                }
            }
            if (_spkHeader != null)
            {
                EditorGUILayout.LabelField("Versión", _spkHeader.VersionHex);
                EditorGUILayout.LabelField("Índice offset", _spkHeader.indexOffset.ToString("N0", CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Índice almacenado", FormatBytes((long)_spkHeader.indexStoredBytes));
                EditorGUILayout.LabelField("Índice decodificado", FormatBytes((long)_spkHeader.indexDecodedBytes));
                EditorGUILayout.LabelField("Registros", _spkHeader.recordCount.ToString("N0"));
                EditorGUILayout.LabelField("Bloque", FormatBytes(_spkHeader.blockBytes));
                EditorGUILayout.LabelField("Aux offset", _spkHeader.auxiliaryOffset.ToString("N0"));
                EditorGUILayout.LabelField("Aux registros", _spkHeader.auxiliaryCount.ToString("N0"));
                EditorGUILayout.LabelField("Index SHA-256", FileFingerprint.ToHex(_spkHeader.encryptedIndexSha256));
            }
            EditorGUILayout.HelpBox(_spkStatus, MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Perfil local", _profilePath);
                if (GUILayout.Button("Seleccionar JSON...", GUILayout.Width(140)))
                {
                    string p = EditorUtility.OpenFilePanel("Perfil AES-GCM local", Application.dataPath, "json");
                    if (!string.IsNullOrWhiteSpace(p)) _profilePath = p;
                }
            }
            using (new EditorGUI.DisabledScope(!File.Exists(_spkPath) || !File.Exists(_profilePath)))
            {
                if (GUILayout.Button("Descifrar y guardar índice autenticado", GUILayout.Height(32))) DecryptIndex();
            }
        }

        private void ParseSpkHeader()
        {
            try
            {
                _spkHeader = SpkV3HeaderParser.Parse(_spkPath);
                _spkStatus = _spkHeader.LooksObservedV3 ? "Header SPK v3 reconocido." : "Header leído, pero no coincide con el layout v3 observado.";
            }
            catch (Exception ex) { _spkHeader = null; _spkStatus = ex.Message; }
        }

        private void DecryptIndex()
        {
            try
            {
                SpkIndexCryptoProfile profile = SpkIndexCryptoProfile.Load(_profilePath);
                SpkIndexDecryptionResult result = SpkV3IndexDecryptor.Decrypt(_spkPath, profile);
                string output = EditorUtility.SaveFilePanel("Guardar índice descifrado", Path.GetDirectoryName(_spkPath), "data.spk.index.decrypted.bin", "bin");
                if (string.IsNullOrWhiteSpace(output)) return;
                File.WriteAllBytes(output, result.decryptedBytes);
                _spkStatus = $"Índice autenticado y descifrado: {result.decryptedBytes.Length:N0} bytes · SHA-256 {result.decryptedSha256}" + (result.startsWithZstdMagic ? " · Zstandard detectado" : string.Empty);
            }
            catch (Exception ex) { _spkStatus = ex.Message; Debug.LogException(ex); }
        }

        private void DrawMeshImporter()
        {
            EditorGUILayout.HelpBox("Importador determinista basado en un layout confirmado. No adivina offsets: define el layout como LegacyMeshLayoutProfile y genera un Mesh nativo Unity.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Archivo legacy", _meshSource);
                if (GUILayout.Button("Seleccionar...", GUILayout.Width(120)))
                {
                    string p = EditorUtility.OpenFilePanel("Seleccionar .svmap/.3DC/.3DO", Path.GetDirectoryName(_meshSource), string.Empty);
                    if (!string.IsNullOrWhiteSpace(p)) _meshSource = p;
                }
            }
            _meshProfile = (LegacyMeshLayoutProfile)EditorGUILayout.ObjectField("Layout profile", _meshProfile, typeof(LegacyMeshLayoutProfile), false);
            _meshOutput = EditorGUILayout.TextField("Mesh asset", _meshOutput);
            if (GUILayout.Button("Crear nuevo layout profile"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Crear layout", "LegacyMeshLayoutProfile", "asset", "Guarda el perfil de offsets confirmado.");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _meshProfile = CreateInstance<LegacyMeshLayoutProfile>();
                    AssetDatabase.CreateAsset(_meshProfile, path);
                    AssetDatabase.SaveAssets();
                    Selection.activeObject = _meshProfile;
                }
            }
            using (new EditorGUI.DisabledScope(!File.Exists(_meshSource) || _meshProfile == null || string.IsNullOrWhiteSpace(_meshOutput)))
            {
                if (GUILayout.Button("Convertir a Mesh nativo Unity", GUILayout.Height(34)))
                {
                    try
                    {
                        Mesh mesh = ProfileDrivenMeshImporter.Import(_meshSource, _meshProfile, _meshOutput);
                        Selection.activeObject = mesh;
                        EditorGUIUtility.PingObject(mesh);
                    }
                    catch (Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("Importación fallida", ex.Message, "OK"); }
                }
            }
        }

        private static string HexDump(byte[] bytes)
        {
            StringBuilder b = new StringBuilder();
            for (int o = 0; o < bytes.Length; o += 16)
            {
                b.Append(o.ToString("X8")).Append("  ");
                int n = Math.Min(16, bytes.Length - o);
                for (int i = 0; i < 16; i++) b.Append(i < n ? bytes[o + i].ToString("X2") + " " : "   ");
                b.Append(" |");
                for (int i = 0; i < n; i++) { byte v = bytes[o + i]; b.Append(v >= 32 && v <= 126 ? (char)v : '.'); }
                b.AppendLine("|");
            }
            return b.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] s = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < s.Length - 1) { v /= 1024; i++; }
            return v.ToString("0.##", CultureInfo.InvariantCulture) + " " + s[i];
        }
    }
}
