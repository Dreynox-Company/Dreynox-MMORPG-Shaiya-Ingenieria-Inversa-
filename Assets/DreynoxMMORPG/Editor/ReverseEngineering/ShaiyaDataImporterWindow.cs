using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Data;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Meshes;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering
{
    public sealed class ShaiyaDataImporterWindow : EditorWindow
    {
        private string _dataFolder = string.Empty;
        private readonly List<ScannedAsset> _files = new List<ScannedAsset>();
        private Vector2 _scroll;
        private Vector2 _fileScroll;
        private string _search = string.Empty;
        private int _selected = -1;
        private string _hexDump = string.Empty;
        private string _meshSource = string.Empty;
        private LegacyMeshLayoutProfile _meshProfile;
        private string _meshOutput = "Assets/DreynoxMMORPG/Imported/Meshes/ImportedMesh.asset";

        [MenuItem("Dreynox MMORPG/Legacy Client/Extracted DATA Inspector")]
        public static void Open()
        {
            ShaiyaDataImporterWindow window = GetWindow<ShaiyaDataImporterWindow>();
            window.titleContent = new GUIContent("Legacy DATA");
            window.minSize = new Vector2(920f, 650f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Dreynox MMORPG · Legacy Client DATA", new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 });
            EditorGUILayout.HelpBox(
                "Este módulo trabaja únicamente con la carpeta DATA ya extraída. El montaje o descifrado de SPK/archives pertenece a Shaiya Studio y no forma parte del cliente Unity.",
                MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawDataInspector();
            EditorGUILayout.Space(16);
            DrawMeshImporter();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDataInspector()
        {
            EditorGUILayout.LabelField("Inventario DATA extraído", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("DATA", _dataFolder);
                if (GUILayout.Button("Seleccionar...", GUILayout.Width(120)))
                {
                    string p = EditorUtility.OpenFolderPanel("Seleccionar DATA extraído", _dataFolder, string.Empty);
                    if (!string.IsNullOrWhiteSpace(p)) { _dataFolder = p; _files.Clear(); _selected = -1; }
                }
            }
            using (new EditorGUI.DisabledScope(!Directory.Exists(_dataFolder)))
            {
                if (GUILayout.Button("Escanear DATA", GUILayout.Height(30))) ScanData();
            }

            if (_files.Count == 0) return;
            EditorGUILayout.LabelField($"{_files.Count:N0} archivos");
            var groups = _files.GroupBy(x => x.kind).OrderByDescending(g => g.Count());
            EditorGUILayout.HelpBox(string.Join("  ", groups.Select(g => g.Key + ":" + g.Count())), MessageType.None);
            _search = EditorGUILayout.TextField("Buscar", _search);
            _fileScroll = EditorGUILayout.BeginScrollView(_fileScroll, GUILayout.Height(250));
            int shown = 0;
            for (int i = 0; i < _files.Count && shown < 800; i++)
            {
                ScannedAsset f = _files[i];
                if (!string.IsNullOrWhiteSpace(_search) && f.relativePath.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                if (GUILayout.Toggle(i == _selected, $"[{f.kind}] {f.relativePath}", "Button"))
                {
                    if (_selected != i) { _selected = i; InspectSelected(); }
                }
            }
            EditorGUILayout.EndScrollView();
            if (_selected >= 0 && _selected < _files.Count)
            {
                ScannedAsset f = _files[_selected];
                EditorGUILayout.LabelField("SHA-256", FileFingerprint.Sha256(f.absolutePath));
                EditorGUILayout.TextArea(_hexDump, GUILayout.MinHeight(150));
            }
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
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Dreynox DATA", ex.Message, "OK");
            }
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

        private void DrawMeshImporter()
        {
            EditorGUILayout.LabelField("Conversión de malla confirmada", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Convierte un recurso ya extraído mediante un layout confirmado. No adivina offsets ni procesa archivos SPK.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("3DC / 3DO / SVMAP", _meshSource);
                if (GUILayout.Button("Seleccionar...", GUILayout.Width(120)))
                {
                    string p = EditorUtility.OpenFilePanel("Seleccionar recurso extraído", Path.GetDirectoryName(_meshSource), string.Empty);
                    if (!string.IsNullOrWhiteSpace(p)) _meshSource = p;
                }
            }
            _meshProfile = (LegacyMeshLayoutProfile)EditorGUILayout.ObjectField("Layout", _meshProfile, typeof(LegacyMeshLayoutProfile), false);
            _meshOutput = EditorGUILayout.TextField("Mesh asset", _meshOutput);
            if (GUILayout.Button("Crear layout profile"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Crear layout", "LegacyMeshLayoutProfile", "asset", "Perfil de offsets confirmado.");
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
                if (GUILayout.Button("Convertir a Mesh Unity", GUILayout.Height(34)))
                {
                    Mesh mesh = ProfileDrivenMeshImporter.Import(_meshSource, _meshProfile, _meshOutput);
                    Selection.activeObject = mesh;
                    EditorGUIUtility.PingObject(mesh);
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
    }
}
