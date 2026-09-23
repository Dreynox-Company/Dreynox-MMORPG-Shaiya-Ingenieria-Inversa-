using System;
using System.IO;
using Dreynox.Mmorpg.ParityCore;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Parity
{
    public sealed class VisualParityComparatorWindow : EditorWindow
    {
        private string _referencePath = string.Empty;
        private string _candidatePath = string.Empty;
        private string _diffPath = string.Empty;
        private string _status = "Seleccione capturas con la misma resolución.";
        private VisualMetricResult? _result;
        private Texture2D _referencePreview;
        private Texture2D _candidatePreview;
        private Texture2D _diffPreview;
        private Vector2 _scroll;

        [MenuItem("Dreynox MMORPG/Client Parity/Visual Screenshot Comparator")]
        public static void Open()
        {
            var window = GetWindow<VisualParityComparatorWindow>();
            window.titleContent = new GUIContent("Visual Parity");
            window.minSize = new Vector2(820f, 620f);
            window.Show();
        }

        private void OnDisable()
        {
            DestroyImmediate(_referencePreview);
            DestroyImmediate(_candidatePreview);
            DestroyImmediate(_diffPreview);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField(
                "Dreynox MMORPG · game.exe ↔ Unity visual parity",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 });
            EditorGUILayout.HelpBox(
                "Use capturas del mismo escenario, cámara y resolución. " +
                "La herramienta calcula MAE, RMSE, PSNR y SSIM global, y genera un mapa de diferencias. " +
                "No convierte estas métricas en un porcentaje de 'juego completado'.",
                MessageType.Info);

            DrawPath("Referencia game.exe", ref _referencePath);
            DrawPath("Captura Unity", ref _candidatePath);

            if (GUILayout.Button("Comparar capturas", GUILayout.Height(34f)))
                Compare();

            EditorGUILayout.HelpBox(_status, MessageType.None);

            if (_result.HasValue)
            {
                VisualMetricResult value = _result.Value;
                EditorGUILayout.LabelField("Pixels", value.PixelCount.ToString("N0"));
                EditorGUILayout.LabelField("MAE normalizado", value.Mae.ToString("0.000000"));
                EditorGUILayout.LabelField("RMSE normalizado", value.Rmse.ToString("0.000000"));
                EditorGUILayout.LabelField(
                    "PSNR",
                    double.IsPositiveInfinity(value.Psnr) ? "∞ dB" : value.Psnr.ToString("0.00") + " dB");
                EditorGUILayout.LabelField("SSIM global", value.Ssim.ToString("0.000000"));

                if (!string.IsNullOrWhiteSpace(_diffPath) && File.Exists(_diffPath))
                {
                    EditorGUILayout.SelectableLabel(_diffPath, EditorStyles.textField, GUILayout.Height(18f));
                    if (GUILayout.Button("Mostrar mapa de diferencias"))
                        EditorUtility.RevealInFinder(_diffPath);
                }
            }

            DrawPreviews();
            EditorGUILayout.EndScrollView();
        }

        private void Compare()
        {
            try
            {
                Texture2D reference = LoadPng(_referencePath);
                Texture2D candidate = LoadPng(_candidatePath);

                if (reference.width != candidate.width || reference.height != candidate.height)
                {
                    DestroyImmediate(reference);
                    DestroyImmediate(candidate);
                    throw new InvalidOperationException(
                        "Las capturas deben tener la misma resolución. Referencia=" +
                        reference.width + "x" + reference.height +
                        ", Unity=" + candidate.width + "x" + candidate.height + ".");
                }

                Color32[] a = reference.GetPixels32();
                Color32[] b = candidate.GetPixels32();
                byte[] rgbA = ToRgb24(a);
                byte[] rgbB = ToRgb24(b);
                _result = VisualMetricCore.CompareRgb24(rgbA, rgbB);

                Texture2D diff = new Texture2D(reference.width, reference.height, TextureFormat.RGBA32, false, true);
                Color32[] diffPixels = new Color32[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    byte dr = (byte)Mathf.Abs(a[i].r - b[i].r);
                    byte dg = (byte)Mathf.Abs(a[i].g - b[i].g);
                    byte db = (byte)Mathf.Abs(a[i].b - b[i].b);
                    diffPixels[i] = new Color32(dr, dg, db, 255);
                }
                diff.SetPixels32(diffPixels);
                diff.Apply(false, false);

                string outputFolder = Path.GetFullPath("Artifacts/Parity");
                Directory.CreateDirectory(outputFolder);
                _diffPath = Path.Combine(
                    outputFolder,
                    "visual-diff-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".png");
                File.WriteAllBytes(_diffPath, diff.EncodeToPNG());

                ReplacePreview(ref _referencePreview, reference);
                ReplacePreview(ref _candidatePreview, candidate);
                ReplacePreview(ref _diffPreview, diff);

                VisualMetricResult value = _result.Value;
                _status =
                    "Comparación completada. SSIM=" + value.Ssim.ToString("0.000000") +
                    ", RMSE=" + value.Rmse.ToString("0.000000") +
                    ", PSNR=" + (double.IsPositiveInfinity(value.Psnr) ? "∞" : value.Psnr.ToString("0.00")) + " dB.";
            }
            catch (Exception ex)
            {
                _result = null;
                _status = ex.Message;
                Debug.LogException(ex);
            }
        }

        private void DrawPreviews()
        {
            if (_referencePreview == null || _candidatePreview == null || _diffPreview == null) return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Previsualización", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTexture("game.exe", _referencePreview);
                DrawTexture("Unity", _candidatePreview);
                DrawTexture("Diff", _diffPreview);
            }
        }

        private static void DrawTexture(string label, Texture2D texture)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                Rect rect = GUILayoutUtility.GetAspectRect(
                    texture.width / (float)Mathf.Max(1, texture.height),
                    GUILayout.Width(250f));
                EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
            }
        }

        private static Texture2D LoadPng(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("No existe la captura seleccionada.", path);

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                DestroyImmediate(texture);
                throw new InvalidDataException("Unity no pudo decodificar la imagen: " + path);
            }
            return texture;
        }

        private static byte[] ToRgb24(Color32[] pixels)
        {
            byte[] result = new byte[pixels.Length * 3];
            int o = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                result[o++] = pixels[i].r;
                result[o++] = pixels[i].g;
                result[o++] = pixels[i].b;
            }
            return result;
        }

        private static void ReplacePreview(ref Texture2D slot, Texture2D value)
        {
            if (slot != null && slot != value) DestroyImmediate(slot);
            slot = value;
        }

        private static void DrawPath(string label, ref string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("Seleccionar…", GUILayout.Width(110f)))
                {
                    string directory = string.IsNullOrWhiteSpace(value)
                        ? Application.dataPath
                        : Path.GetDirectoryName(value);
                    string selected = EditorUtility.OpenFilePanel(label, directory, "png");
                    if (!string.IsNullOrWhiteSpace(selected)) value = selected;
                }
            }
        }
    }
}
