using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Executable
{
    public sealed class ExecutableParityInspectorWindow : EditorWindow
    {
        private string _legacyPath = string.Empty;
        private string _dreynoxPath = string.Empty;
        private PortableExecutableInfo _legacy;
        private PortableExecutableInfo _dreynox;
        private string _status = "Seleccione el game.exe original y, cuando exista, el build Windows de Dreynox MMORPG.";
        private Vector2 _scroll;

        [MenuItem("Dreynox MMORPG/Reverse Engineering/EXE Parity Inspector")]
        public static void Open()
        {
            var w = GetWindow<ExecutableParityInspectorWindow>();
            w.titleContent = new GUIContent("EXE Parity");
            w.minSize = new Vector2(760, 560);
            w.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Dreynox MMORPG · Executable Parity Inspector", new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 });
            EditorGUILayout.HelpBox("Este comparador no pretende igualdad binaria: Unity y el cliente clásico tienen toolchains distintos. Verifica identidad del game.exe de referencia y captura una línea base PE reproducible para correlacionar arquitectura, imports y builds mientras la paridad funcional/visual se prueba por separado.", MessageType.Info);
            DrawPath("game.exe original", ref _legacyPath);
            DrawPath("DreynoxMmorpg.exe", ref _dreynoxPath);
            if (GUILayout.Button("Analizar ejecutables", GUILayout.Height(34))) Analyze();
            EditorGUILayout.HelpBox(_status, MessageType.None);
            if (_legacy != null) DrawInfo("Cliente clásico", _legacy, true);
            if (_dreynox != null) DrawInfo("Dreynox MMORPG", _dreynox, false);
            if (_legacy != null && _dreynox != null) DrawComparison();
            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            try
            {
                _legacy = File.Exists(_legacyPath) ? PortableExecutableInspector.Analyze(_legacyPath) : null;
                _dreynox = File.Exists(_dreynoxPath) ? PortableExecutableInspector.Analyze(_dreynoxPath) : null;
                if (_legacy == null && _dreynox == null) throw new FileNotFoundException("No se seleccionó ningún ejecutable existente.");
                LegacyGameExeIdentity match = LegacyGameExeBaseline.Match(_legacy);
                _status = match != null
                    ? "game.exe coincide exactamente con baseline conocido: " + match.id + "."
                    : _legacy != null
                        ? "game.exe es un PE válido, pero no coincide con ninguna huella catalogada. Se tratará como una variante independiente."
                        : "Build Dreynox analizado; falta seleccionar el game.exe de referencia.";
            }
            catch (Exception ex)
            {
                _status = ex.Message;
                Debug.LogException(ex);
            }
        }

        private static void DrawInfo(string title, PortableExecutableInfo info, bool legacy)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bytes", info.fileBytes.ToString("N0"));
            EditorGUILayout.SelectableLabel("SHA-256: " + info.sha256, EditorStyles.textField, GUILayout.Height(18));
            EditorGUILayout.LabelField("Machine", info.MachineHex + (info.machine == 0x8664 ? " · x64" : info.machine == 0x014c ? " · x86" : string.Empty));
            EditorGUILayout.LabelField("PE", info.IsPe32Plus ? "PE32+" : "PE32");
            EditorGUILayout.LabelField("EntryPoint RVA", "0x" + info.entryPointRva.ToString("X8"));
            EditorGUILayout.LabelField("ImageBase", "0x" + info.imageBase.ToString("X"));
            EditorGUILayout.LabelField("Subsystem", "0x" + info.subsystem.ToString("X4"));
            EditorGUILayout.LabelField("Sections", string.Join(", ", info.sections.Select(x => x.name)));
            EditorGUILayout.LabelField("Import DLLs", info.importDlls.Count == 0 ? "(no enumeradas)" : string.Join(", ", info.importDlls));
            if (legacy)
            {
                LegacyGameExeIdentity match = LegacyGameExeBaseline.Match(info);
                EditorGUILayout.LabelField("Baseline", match == null ? "UNKNOWN / NUEVA VARIANTE" : match.id);
                if (match != null) EditorGUILayout.HelpBox(match.evidence, MessageType.None);
            }
        }

        private void DrawComparison()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Comparación estructural", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Arquitectura", _legacy.MachineHex + " → " + _dreynox.MachineHex);
            EditorGUILayout.LabelField("Tamaño", _legacy.fileBytes.ToString("N0") + " → " + _dreynox.fileBytes.ToString("N0"));
            EditorGUILayout.LabelField("Imports compartidos", _legacy.importDlls.Intersect(_dreynox.importDlls, StringComparer.OrdinalIgnoreCase).Count().ToString());
            EditorGUILayout.HelpBox("La métrica de progreso principal no será similitud de bytes. La paridad se cerrará mediante fixtures DATA, capturas visuales, estados de animación, física, cámara, networking y comportamiento reproducible.", MessageType.Info);
        }

        private static void DrawPath(string label, ref string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("Seleccionar…", GUILayout.Width(110)))
                {
                    string p = EditorUtility.OpenFilePanel(label, string.IsNullOrWhiteSpace(value) ? Application.dataPath : Path.GetDirectoryName(value), "exe");
                    if (!string.IsNullOrWhiteSpace(p)) value = p;
                }
            }
        }
    }
}
