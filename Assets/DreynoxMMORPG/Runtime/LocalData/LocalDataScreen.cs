// Raw DATA loading is an opt-in developer tool, never a shipping-client feature.
#if DREYNOX_DEV_DATA && !DEVELOPMENT_BUILD && !UNITY_EDITOR
#error DREYNOX_DEV_DATA requires a Development Player build.
#endif
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && DREYNOX_DEV_DATA)
using System;
using System.IO;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>Opt-in qualification screen for raw local DATA; not the completed MMORPG client.</summary>
    [RequireComponent(typeof(LocalCharacterLoader))]
    public sealed class LocalDataScreen : MonoBehaviour
    {
        private LocalCharacterLoader loader;
        private string folder = "", browsing = "", error = "";
        private string[] children = Array.Empty<string>();
        private Vector2 scroll;
        private int rig, face, hair;
        private string settingsPath;
        private Camera viewCamera;
        private float yaw = 180f;
        private float distance = 5f;
        private bool browse;
        [Serializable] private sealed class Selection { public string folder; }
        private void Awake()
        {
            loader = GetComponent<LocalCharacterLoader>();
            viewCamera = Camera.main;
            settingsPath = Path.Combine(Application.persistentDataPath, "local-data-selection.json");
            try { if (File.Exists(settingsPath)) folder = JsonUtility.FromJson<Selection>(File.ReadAllText(settingsPath))?.folder ?? ""; }
            catch (Exception ex) { error = ex.Message; }
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--data-root") folder = args[i + 1];
        }
        private void Start() { if (!string.IsNullOrWhiteSpace(folder)) Load(); }
        private void Update()
        {
            if (loader.Current != null && Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 3f;
                loader.Current.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            if (viewCamera != null)
            {
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.35f, 2f, 10f);
                viewCamera.transform.position = new Vector3(0, 1.3f, -distance);
                viewCamera.transform.LookAt(new Vector3(0, 1.1f, 0));
            }
        }
        private async void Load()
        {
            int requestedRig = rig, requestedFace = face, requestedHair = hair;
            string requestedFolder = folder;
            bool ok = await loader.LoadAsync(requestedFolder, requestedRig, requestedFace, requestedHair);
            if (this == null || !ok) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, JsonUtility.ToJson(new Selection { folder = requestedFolder }));
                loader.Current.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            }
            catch (Exception ex) { error = ex.Message; }
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(430, Screen.width - 24), Screen.height - 24), GUI.skin.box);
            GUILayout.Label("Dreynox MMORPG · DESARROLLADOR · DATA local");
            GUILayout.Label("Herramienta opcional de pruebas. No estará en la entrega final.");
            folder = GUILayout.TextField(folder);
            if (GUILayout.Button("Explorar carpeta")) { browse = !browse; if (browse) ListFolder(Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); }
            if (browse)
            {
                GUILayout.Label(browsing);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Subir")) ListFolder(Directory.GetParent(browsing)?.FullName ?? browsing);
                if (GUILayout.Button("Usar esta carpeta")) { folder = browsing; browse = false; }
                GUILayout.EndHorizontal();
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(180));
                foreach (string child in children) if (GUILayout.Button(Path.GetFileName(child))) { ListFolder(child); break; }
                GUILayout.EndScrollView();
            }
            string prefix = LegacyCharacterRigCore.ResolveNativeRigIndex(rig).Prefix;
            Selector("Rig " + (rig + 1) + "/16: " + prefix, ref rig, 16);
            Selector("Rostro " + (face + 1), ref face, 5);
            Selector("Cabello " + (hair + 1), ref hair, 5);
            if (GUILayout.Button("Cargar selección desde DATA")) Load();
            if (loader.IsLoading && GUILayout.Button("Cancelar carga")) loader.Cancel();
            if (loader.Current != null)
            {
                var player = loader.Current.GetComponent<LocalAniPlayer>();
                if (player != null && GUILayout.Button(player.Paused ? "Reanudar ANI" : "Pausar ANI")) player.Paused = !player.Paused;
            }
            GUILayout.Label(loader.Status);
            GUILayout.Label(error);
            GUILayout.Label("Botón derecho: rotar · Rueda: zoom.\nLa carpeta original nunca se modifica ni se ejecuta game.exe.");
            GUILayout.EndArea();
        }
        private static void Selector(string label, ref int value, int count)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(32))) value = (value + count - 1) % count;
            GUILayout.Label(label);
            if (GUILayout.Button(">", GUILayout.Width(32))) value = (value + 1) % count;
            GUILayout.EndHorizontal();
        }
        private void ListFolder(string path)
        {
            try
            {
                browsing = Path.GetFullPath(path);
                children = Array.FindAll(Directory.GetDirectories(browsing), p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0);
                Array.Sort(children, StringComparer.OrdinalIgnoreCase); error = "";
            }
            catch (Exception ex) { children = Array.Empty<string>(); error = ex.Message; }
        }
    }
}
#endif
