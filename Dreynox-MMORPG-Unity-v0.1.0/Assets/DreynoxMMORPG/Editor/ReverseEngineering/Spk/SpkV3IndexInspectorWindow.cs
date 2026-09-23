using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public sealed class SpkV3IndexInspectorWindow : EditorWindow
    {
        private string _spkPath = string.Empty;
        private string _profilePath = string.Empty;
        private string _zstdPath = string.Empty;
        private SpkV3Index _index;
        private string _status = "Seleccione data.spk y un perfil AES-GCM local.";
        private Vector2 _scroll;

        [MenuItem("Dreynox MMORPG/Reverse Engineering/SPK v3 Complete Index")]
        public static void Open()
        {
            var window = GetWindow<SpkV3IndexInspectorWindow>();
            window.titleContent = new GUIContent("SPK v3 Index");
            window.minSize = new Vector2(760, 560);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Dreynox MMORPG · SPK v3 Complete Index", new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 });
            EditorGUILayout.HelpBox("Pipeline exacto: header → SHA-256 → AES-GCM → Zstandard → registros de 96 bytes → auxiliares de 32 bytes → validación de cobertura/fragmentos. Las claves permanecen locales y nunca se exportan al informe.", MessageType.Info);
            DrawPath("data.spk", ref _spkPath, "spk");
            DrawPath("Perfil AES-GCM", ref _profilePath, "json");
            DrawPath("zstd.exe (opcional si está en PATH/ZSTD_EXE)", ref _zstdPath, string.Empty);

            using (new EditorGUI.DisabledScope(!File.Exists(_spkPath) || !File.Exists(_profilePath)))
            {
                if (GUILayout.Button("Montar y validar índice completo", GUILayout.Height(34))) Analyze();
            }
            EditorGUILayout.HelpBox(_status, _index == null ? MessageType.None : MessageType.Info);
            if (_index != null) DrawSummary();
            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            try
            {
                SpkIndexCryptoProfile profile = SpkIndexCryptoProfile.Load(_profilePath);
                _index = SpkV3CompleteIndexReader.Read(_spkPath, profile, string.IsNullOrWhiteSpace(_zstdPath) ? null : _zstdPath);
                _status = "Índice completo autenticado, decodificado y validado sin huecos ni fragmentos huérfanos.";
            }
            catch (Exception ex)
            {
                _index = null;
                _status = ex.Message;
                Debug.LogException(ex);
            }
        }

        private void DrawSummary()
        {
            int resources = _index.records.Count(x => x.Resource);
            int simple = _index.records.Count(x => x.Simple);
            int fragmented = _index.records.Count(x => x.Fragmented);
            int special = _index.records.Count - resources;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Resumen validado", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Registros", _index.records.Count.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Recursos", resources.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Simples", simple.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Fragmentados", fragmented.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Especiales", special.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Auxiliares", _index.auxiliary.Count.ToString("N0", CultureInfo.InvariantCulture));
            EditorGUILayout.SelectableLabel("Index SHA-256 cifrado: " + _index.encryptedIndexSha256, EditorStyles.textField, GUILayout.Height(18));
            EditorGUILayout.SelectableLabel("Index SHA-256 decodificado: " + _index.decodedIndexSha256, EditorStyles.textField, GUILayout.Height(18));
            if (GUILayout.Button("Exportar informe JSON sin secretos")) ExportReport(resources, simple, fragmented, special);
        }

        private void ExportReport(int resources, int simple, int fragmented, int special)
        {
            string output = EditorUtility.SaveFilePanel("Guardar informe SPK", Path.GetDirectoryName(_spkPath), "dreynox-spk-v3-report.json", "json");
            if (string.IsNullOrWhiteSpace(output)) return;
            StringBuilder b = new StringBuilder();
            b.AppendLine("{");
            b.AppendLine("  \"schema\": 1,");
            b.AppendLine("  \"engine\": \"Dreynox MMORPG Unity\",");
            b.AppendLine("  \"encryptedIndexSha256\": \"" + _index.encryptedIndexSha256 + "\",");
            b.AppendLine("  \"decodedIndexSha256\": \"" + _index.decodedIndexSha256 + "\",");
            b.AppendLine("  \"records\": " + _index.records.Count + ",");
            b.AppendLine("  \"resources\": " + resources + ",");
            b.AppendLine("  \"simpleResources\": " + simple + ",");
            b.AppendLine("  \"fragmentedResources\": " + fragmented + ",");
            b.AppendLine("  \"specialRecords\": " + special + ",");
            b.AppendLine("  \"auxiliaryRecords\": " + _index.auxiliary.Count + ",");
            b.AppendLine("  \"recordBytes\": 96,");
            b.AppendLine("  \"auxiliaryRecordBytes\": 32,");
            b.AppendLine("  \"validatedRelationships\": true");
            b.AppendLine("}");
            File.WriteAllText(output, b.ToString(), Encoding.UTF8);
            EditorUtility.RevealInFinder(output);
        }

        private static void DrawPath(string label, ref string value, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("Seleccionar…", GUILayout.Width(110)))
                {
                    string p = EditorUtility.OpenFilePanel(label, string.IsNullOrWhiteSpace(value) ? Application.dataPath : Path.GetDirectoryName(value), extension);
                    if (!string.IsNullOrWhiteSpace(p)) value = p;
                }
            }
        }
    }
}
