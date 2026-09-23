using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Corpus
{
    public sealed class CanonicalCorpusWindow : EditorWindow
    {
        private string _root = string.Empty;
        private CorpusValidationResult _validation;
        private Vector2 _scroll;

        [MenuItem("Dreynox MMORPG/Client Parity/Canonical ps0032 Corpus")]
        public static void Open()
        {
            CanonicalCorpusWindow window =
                GetWindow<CanonicalCorpusWindow>();

            window.titleContent =
                new GUIContent("ps0032 Corpus");

            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _root = CanonicalClientCorpus.StoredRoot;
            if (!string.IsNullOrWhiteSpace(_root))
                Validate();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField(
                "Dreynox MMORPG · Canonical ps0032 client corpus",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 17
                });

            EditorGUILayout.HelpBox(
                "Seleccione la carpeta extraída que contiene directamente " +
                "game.exe y DATA_Español/. La ruta se guarda únicamente en " +
                "EditorPrefs local; DATA no se copia ni se sube a Git.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _root = EditorGUILayout.TextField(
                    "Corpus root",
                    _root);

                if (GUILayout.Button(
                        "Seleccionar…",
                        GUILayout.Width(110f)))
                {
                    string selected =
                        EditorUtility.OpenFolderPanel(
                            "Canonical ps0032 corpus",
                            string.IsNullOrWhiteSpace(_root)
                                ? Application.dataPath
                                : _root,
                            string.Empty);

                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        _root = selected;
                        Validate();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Validar corpus",
                        GUILayout.Height(30f)))
                {
                    Validate();
                }

                if (GUILayout.Button(
                        "Guardar como canónico",
                        GUILayout.Height(30f)))
                {
                    Validate();

                    if (_validation != null &&
                        _validation.IsCanonical)
                    {
                        CanonicalClientCorpus.StoredRoot = _root;
                        ShowNotification(
                            new GUIContent(
                                "Corpus ps0032 validado y guardado localmente."));
                    }
                    else
                    {
                        ShowNotification(
                            new GUIContent(
                                "El corpus no superó la validación."));
                    }
                }
            }

            DrawValidation();

            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(_root))
            {
                _validation = null;
                return;
            }

            CanonicalClientCorpus corpus =
                new CanonicalClientCorpus(_root);

            _validation = corpus.Validate();
            Repaint();
        }

        private void DrawValidation()
        {
            if (_validation == null)
                return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "Resultado",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Baseline",
                _validation.baselineId);

            EditorGUILayout.LabelField(
                "game.exe bytes",
                _validation.gameExeBytes.ToString("N0"));

            EditorGUILayout.SelectableLabel(
                "SHA-256: " +
                (_validation.gameExeSha256 ?? "(sin calcular)"),
                EditorStyles.textField,
                GUILayout.Height(18f));

            if (_validation.IsCanonical)
            {
                EditorGUILayout.HelpBox(
                    "Corpus canónico ps0032 confirmado.",
                    MessageType.Info);
            }

            for (int i = 0;
                 i < _validation.errors.Count;
                 i++)
            {
                EditorGUILayout.HelpBox(
                    _validation.errors[i],
                    MessageType.Error);
            }

            if (_validation.presentFiles.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "Recursos de control encontrados",
                    EditorStyles.boldLabel);

                for (int i = 0;
                     i < _validation.presentFiles.Count;
                     i++)
                {
                    EditorGUILayout.LabelField(
                        "✓ " + _validation.presentFiles[i]);
                }
            }

            for (int i = 0;
                 i < _validation.missingFiles.Count;
                 i++)
            {
                EditorGUILayout.HelpBox(
                    "Falta: " + _validation.missingFiles[i],
                    MessageType.Warning);
            }

            if (Directory.Exists(_root))
            {
                if (GUILayout.Button(
                        "Mostrar carpeta"))
                {
                    EditorUtility.RevealInFinder(_root);
                }
            }
        }
    }
}
