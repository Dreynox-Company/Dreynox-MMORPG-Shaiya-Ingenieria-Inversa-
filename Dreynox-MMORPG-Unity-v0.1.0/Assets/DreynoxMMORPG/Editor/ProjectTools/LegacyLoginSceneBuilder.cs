using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Editor.ProjectTools
{
    public static class LegacyLoginSceneBuilder
    {
        public const string ScenePath =
            "Assets/DreynoxMMORPG/Game/Scenes/Generated/" +
            "LegacyLoginParity.unity";

        private static readonly Vector2 ReferenceResolution =
            new Vector2(1920f, 1200f);

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Login Scene")]
        public static void Build()
        {
            LegacyUiAssetImporter.ImportCanonicalLoginUi();
            EnsureFolder(
                "Assets/DreynoxMMORPG/Game/Scenes/Generated");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateEventSystem();

            Canvas canvas = CreateCanvas();
            RectTransform root =
                canvas.GetComponent<RectTransform>();

            Sprite background =
                LegacyUiAssetImporter.LoadLoginSprite("bg.tga");

            Sprite logo =
                LegacyUiAssetImporter.LoadLoginSprite(
                    "shaiyalogo01_new.tga");

            Sprite check =
                LegacyUiAssetImporter.LoadLoginSprite(
                    "logincheck.tga");

            CreateImage(
                "LegacyLoginBackground",
                root,
                background,
                Vector2.zero,
                Vector2.zero,
                Vector2.one,
                Vector2.one,
                Vector2.zero);

            CreateImage(
                "ShaiyaLogo",
                root,
                logo,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(512f, 256f),
                new Vector2(0f, -56f),
                new Vector2(0.5f, 1f));

            RectTransform panel =
                CreateRect(
                    "LoginControls",
                    root,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(460f, 260f),
                    new Vector2(0f, -170f),
                    new Vector2(0.5f, 0.5f));

            InputField account =
                CreateInput(
                    "AccountInput",
                    panel,
                    "Account",
                    new Vector2(0f, 70f),
                    false);

            InputField password =
                CreateInput(
                    "PasswordInput",
                    panel,
                    "Password",
                    new Vector2(0f, 10f),
                    true);

            Toggle saveId =
                CreateToggle(
                    "SaveId",
                    panel,
                    check,
                    new Vector2(-145f, -42f));

            Button login =
                CreateButton(
                    "LoginButton",
                    panel,
                    "LOGIN",
                    new Vector2(-85f, -100f));

            Button exit =
                CreateButton(
                    "ExitButton",
                    panel,
                    "EXIT",
                    new Vector2(85f, -100f));

            Text status =
                CreateText(
                    "Status",
                    panel,
                    string.Empty,
                    16,
                    TextAnchor.MiddleCenter);

            RectTransform statusRect =
                status.rectTransform;

            statusRect.anchorMin =
                statusRect.anchorMax =
                    new Vector2(0.5f, 0f);

            statusRect.pivot =
                new Vector2(0.5f, 0f);

            statusRect.sizeDelta =
                new Vector2(440f, 32f);

            statusRect.anchoredPosition =
                new Vector2(0f, -150f);

            LegacyLoginScreenController controller =
                canvas.gameObject.AddComponent<
                    LegacyLoginScreenController>();

            controller.Bind(
                account,
                password,
                saveId,
                login,
                exit,
                status);

            EditorSceneManager.SaveScene(
                scene,
                ScenePath);

            Selection.activeObject =
                canvas.gameObject;

            Debug.Log(
                "Dreynox MMORPG: canonical ps0032 Login parity scene " +
                "generated locally at " + ScenePath + ".");
        }

        private static Canvas CreateCanvas()
        {
            GameObject go =
                new GameObject(
                    "LegacyLoginCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));

            Canvas canvas =
                go.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler =
                go.GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                ReferenceResolution;

            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void CreateEventSystem()
        {
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private static Image CreateImage(
            string name,
            RectTransform parent,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 anchoredPosition,
            Vector2 pivot)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;

            if (anchorMin == anchorMax)
                rect.sizeDelta = size;
            else
                rect.sizeDelta = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;

            return image;
        }

        private static RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position,
            Vector2 pivot)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform));

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            return rect;
        }

        private static InputField CreateInput(
            string name,
            RectTransform parent,
            string placeholder,
            Vector2 position,
            bool password)
        {
            RectTransform root =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(360f, 44f),
                    position,
                    new Vector2(0.5f, 0.5f));

            Image background =
                root.gameObject.AddComponent<Image>();

            background.color =
                new Color(0.03f, 0.03f, 0.03f, 0.78f);

            Text text =
                CreateText(
                    "Text",
                    root,
                    string.Empty,
                    20,
                    TextAnchor.MiddleLeft);

            SetInset(text.rectTransform, 12f);

            Text placeholderText =
                CreateText(
                    "Placeholder",
                    root,
                    placeholder,
                    20,
                    TextAnchor.MiddleLeft);

            placeholderText.color =
                new Color(1f, 1f, 1f, 0.45f);

            SetInset(
                placeholderText.rectTransform,
                12f);

            InputField input =
                root.gameObject.AddComponent<InputField>();

            input.textComponent = text;
            input.placeholder = placeholderText;

            if (password)
                input.contentType =
                    InputField.ContentType.Password;

            return input;
        }

        private static Toggle CreateToggle(
            string name,
            RectTransform parent,
            Sprite checkSprite,
            Vector2 position)
        {
            RectTransform root =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(170f, 32f),
                    position,
                    new Vector2(0.5f, 0.5f));

            RectTransform box =
                CreateRect(
                    "Background",
                    root,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(28f, 28f),
                    new Vector2(14f, 0f),
                    new Vector2(0.5f, 0.5f));

            Image bg =
                box.gameObject.AddComponent<Image>();

            bg.color =
                new Color(0f, 0f, 0f, 0.75f);

            RectTransform mark =
                CreateRect(
                    "Checkmark",
                    box,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(28f, 28f),
                    Vector2.zero,
                    new Vector2(0.5f, 0.5f));

            Image markImage =
                mark.gameObject.AddComponent<Image>();

            markImage.sprite = checkSprite;
            markImage.preserveAspect = true;

            Text label =
                CreateText(
                    "Label",
                    root,
                    "Save ID",
                    18,
                    TextAnchor.MiddleLeft);

            RectTransform labelRect =
                label.rectTransform;

            labelRect.anchorMin =
                new Vector2(0f, 0f);

            labelRect.anchorMax =
                new Vector2(1f, 1f);

            labelRect.offsetMin =
                new Vector2(38f, 0f);

            labelRect.offsetMax =
                Vector2.zero;

            Toggle toggle =
                root.gameObject.AddComponent<Toggle>();

            toggle.targetGraphic = bg;
            toggle.graphic = markImage;

            return toggle;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 position)
        {
            RectTransform root =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(150f, 42f),
                    position,
                    new Vector2(0.5f, 0.5f));

            Image image =
                root.gameObject.AddComponent<Image>();

            image.color =
                new Color(0.25f, 0.06f, 0.05f, 0.92f);

            Button button =
                root.gameObject.AddComponent<Button>();

            button.targetGraphic = image;

            Text text =
                CreateText(
                    "Label",
                    root,
                    label,
                    18,
                    TextAnchor.MiddleCenter);

            text.color = Color.white;

            return button;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string value,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject go =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text =
                go.GetComponent<Text>();

            text.text = value;
            text.font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        private static void SetInset(
            RectTransform rect,
            float horizontal)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin =
                new Vector2(horizontal, 0f);
            rect.offsetMax =
                new Vector2(-horizontal, 0f);
        }

        private static void EnsureFolder(
            string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
