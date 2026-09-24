using System;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Editor.ProjectTools
{
    public static class LegacyCharacterFlowSceneBuilder
    {
        public const string CharacterSelectScenePath =
            "Assets/DreynoxMMORPG/Game/Scenes/Generated/" +
            "LegacyCharacterSelectParity.unity";

        public const string CharacterMakeScenePath =
            "Assets/DreynoxMMORPG/Game/Scenes/Generated/" +
            "LegacyCharacterMakeParity.unity";

        private const string CharacterPrefabPath =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/" +
            "Characters/HumanMale003/Prefabs/" +
            "HumanMale003_Canonical.prefab";

        private static readonly Vector2 ReferenceResolution =
            new Vector2(1920f, 1200f);

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Character Select Scene")]
        public static void BuildCharacterSelect()
        {
            LegacyUiAssetImporter.ImportCanonicalCharacterSelectUi();
            LegacyCharacterImporter.ImportCanonicalHumanMale003();
            EnsureGeneratedSceneFolder();

            CanonicalClientCorpus corpus =
                RequireCanonicalCorpus();

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            CreateEventSystem();

            LegacyDungeonPreviewBuildResult environment =
                LegacyDungeonPreviewEnvironmentImporter
                    .CreateCanonicalLogin(
                        corpus);

            Vector3 anchor =
                environment.PreviewAnchor;

            Camera camera =
                CreateCamera(
                    anchor +
                    new Vector3(
                        0f,
                        1.55f,
                        -6.2f),
                    anchor +
                    new Vector3(
                        0.75f,
                        1.0f,
                        0f),
                    35f);

            GameObject actor =
                InstantiateCanonicalActor(
                    anchor +
                    new Vector3(
                        0.85f,
                        0f,
                        0f),
                    Quaternion.Euler(
                        0f,
                        180f,
                        0f));

            ShaiyaClientActor clientActor =
                actor.GetComponent<ShaiyaClientActor>();

            if (clientActor != null)
                clientActor.enabled = false;

            CharacterController characterController =
                actor.GetComponent<CharacterController>();

            if (characterController != null)
                characterController.enabled = false;

            SemanticAnimationPlayer animation =
                actor.GetComponent<SemanticAnimationPlayer>();

            AddSelectionLighting();

            Canvas canvas = CreateCanvas("CharacterSelectCanvas");

            LegacyCharacterSelectScreenController controller =
                canvas.gameObject.AddComponent<
                    LegacyCharacterSelectScreenController>();

            BuildCharacterSelectUi(
                canvas.GetComponent<RectTransform>(),
                controller);

            LegacyCharacterSelectParityFixture fixture =
                canvas.gameObject.AddComponent<
                    LegacyCharacterSelectParityFixture>();

            fixture.Bind(controller, animation);

            EditorSceneManager.SaveScene(
                scene,
                CharacterSelectScenePath);

            Selection.activeObject = actor;

            Debug.Log(
                "Dreynox MMORPG: canonical CharacterSelect scene generated at " +
                CharacterSelectScenePath + ".");
        }

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Character Make Scene")]
        public static void BuildCharacterMake()
        {
            LegacyUiAssetImporter.ImportCanonicalCharacterMakeUi();
            LegacyCharacterImporter.ImportCanonicalHumanMale003();
            EnsureGeneratedSceneFolder();

            CanonicalClientCorpus corpus =
                RequireCanonicalCorpus();

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            CreateEventSystem();

            LegacyDungeonPreviewBuildResult environment =
                LegacyDungeonPreviewEnvironmentImporter
                    .CreateCanonicalLogin(
                        corpus);

            Vector3 anchor =
                environment.PreviewAnchor;

            Camera camera =
                CreateCamera(
                    anchor +
                    new Vector3(
                        0f,
                        1.55f,
                        -6.4f),
                    anchor +
                    new Vector3(
                        0.75f,
                        1.0f,
                        0f),
                    34f);

            GameObject actor =
                InstantiateCanonicalActor(
                    anchor +
                    new Vector3(
                        0.85f,
                        0f,
                        0f),
                    Quaternion.Euler(
                        0f,
                        180f,
                        0f));

            ShaiyaClientActor clientActor =
                actor.GetComponent<ShaiyaClientActor>();

            if (clientActor != null)
                clientActor.enabled = false;

            CharacterController characterController =
                actor.GetComponent<CharacterController>();

            if (characterController != null)
                characterController.enabled = false;

            SemanticAnimationPlayer animation =
                actor.GetComponent<SemanticAnimationPlayer>();

            AddSelectionLighting();

            Canvas canvas = CreateCanvas("CharacterMakeCanvas");

            LegacyCharacterMakeScreenController controller =
                canvas.gameObject.AddComponent<
                    LegacyCharacterMakeScreenController>();

            BuildCharacterMakeUi(
                canvas.GetComponent<RectTransform>(),
                controller);

            LegacyCharacterMakeParityFixture fixture =
                canvas.gameObject.AddComponent<
                    LegacyCharacterMakeParityFixture>();

            fixture.Bind(
                controller,
                clientActor,
                animation);

            EditorSceneManager.SaveScene(
                scene,
                CharacterMakeScenePath);

            Selection.activeObject = actor;

            Debug.Log(
                "Dreynox MMORPG: canonical CharacterMake scene generated at " +
                CharacterMakeScenePath + ".");
        }

        private static CanonicalClientCorpus RequireCanonicalCorpus()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                throw new InvalidOperationException(
                    "Configure the canonical ps0032 corpus before building character parity scenes.");
            }

            return corpus;
        }

        private static void BuildCharacterSelectUi(
            RectTransform root,
            LegacyCharacterSelectScreenController controller)
        {
            const int slotCount = 5;

            Button[] buttons = new Button[slotCount];
            Text[] names = new Text[slotCount];
            Text[] metas = new Text[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                RectTransform panel =
                    CreateRect(
                        "CharacterSlot_" + (i + 1),
                        root,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(390f, 80f),
                        new Vector2(235f, -235f - i * 92f),
                        new Vector2(0.5f, 0.5f));

                Image background =
                    panel.gameObject.AddComponent<Image>();

                background.color =
                    new Color(0.16f, 0.10f, 0.04f, 0.84f);

                Button button =
                    panel.gameObject.AddComponent<Button>();

                button.targetGraphic = background;
                buttons[i] = button;

                Text name =
                    CreateText(
                        "Name",
                        panel,
                        "Create Character",
                        22,
                        TextAnchor.MiddleLeft);

                name.fontStyle = FontStyle.Bold;
                SetOffsets(
                    name.rectTransform,
                    18f,
                    34f,
                    -18f,
                    -6f);

                names[i] = name;

                Text meta =
                    CreateText(
                        "Meta",
                        panel,
                        "Empty slot",
                        15,
                        TextAnchor.LowerLeft);

                meta.color =
                    new Color(0.88f, 0.78f, 0.58f, 1f);

                SetOffsets(
                    meta.rectTransform,
                    18f,
                    7f,
                    -18f,
                    -42f);

                metas[i] = meta;
            }

            Button start =
                CreateButton(
                    "StartButton",
                    root,
                    "START",
                    new Vector2(1f, 0f),
                    new Vector2(-165f, 75f),
                    new Vector2(220f, 58f));

            Button create =
                CreateButton(
                    "CreateButton",
                    root,
                    "CREATE",
                    new Vector2(0f, 0f),
                    new Vector2(130f, 75f),
                    new Vector2(180f, 50f));

            Button delete =
                CreateButton(
                    "DeleteButton",
                    root,
                    "DELETE",
                    new Vector2(0f, 0f),
                    new Vector2(325f, 75f),
                    new Vector2(180f, 50f));

            Text status =
                CreateText(
                    "Status",
                    root,
                    string.Empty,
                    16,
                    TextAnchor.MiddleCenter);

            status.rectTransform.anchorMin =
                new Vector2(0.5f, 0f);

            status.rectTransform.anchorMax =
                new Vector2(0.5f, 0f);

            status.rectTransform.pivot =
                new Vector2(0.5f, 0f);

            status.rectTransform.sizeDelta =
                new Vector2(760f, 44f);

            status.rectTransform.anchoredPosition =
                new Vector2(0f, 24f);

            controller.Bind(
                buttons,
                names,
                metas,
                start,
                create,
                delete,
                status);
        }

        private static void BuildCharacterMakeUi(
            RectTransform root,
            LegacyCharacterMakeScreenController controller)
        {
            RectTransform leftPanel =
                CreateRect(
                    "CreationPanel",
                    root,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(530f, 860f),
                    new Vector2(300f, -10f),
                    new Vector2(0.5f, 0.5f));

            Image panelImage =
                leftPanel.gameObject.AddComponent<Image>();

            panelImage.color =
                new Color(0.05f, 0.025f, 0.01f, 0.60f);

            Text title =
                CreateText(
                    "Title",
                    leftPanel,
                    "CREATE CHARACTER",
                    28,
                    TextAnchor.UpperCenter);

            title.fontStyle = FontStyle.Bold;

            SetOffsets(
                title.rectTransform,
                20f,
                28f,
                -20f,
                -780f);

            InputField name =
                CreateInput(
                    "CharacterName",
                    leftPanel,
                    "Character name",
                    new Vector2(0f, 285f));

            Text selection =
                CreateText(
                    "Selection",
                    leftPanel,
                    string.Empty,
                    17,
                    TextAnchor.MiddleCenter);

            selection.rectTransform.anchorMin =
                selection.rectTransform.anchorMax =
                    new Vector2(0.5f, 0.5f);

            selection.rectTransform.sizeDelta =
                new Vector2(480f, 56f);

            selection.rectTransform.anchoredPosition =
                new Vector2(0f, 225f);

            Text jobsLabel =
                CreateText(
                    "JobsLabel",
                    leftPanel,
                    "JOB",
                    18,
                    TextAnchor.MiddleCenter);

            jobsLabel.rectTransform.anchorMin =
                jobsLabel.rectTransform.anchorMax =
                    new Vector2(0.5f, 0.5f);

            jobsLabel.rectTransform.sizeDelta =
                new Vector2(460f, 32f);

            jobsLabel.rectTransform.anchoredPosition =
                new Vector2(0f, 165f);

            for (int job = 0; job < 6; job++)
            {
                int column = job % 3;
                int row = job / 3;

                Button jobButton =
                    CreateButton(
                        "Job_" + job,
                        leftPanel,
                        "JOB " + job,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(
                            -150f + column * 150f,
                            115f - row * 58f),
                        new Vector2(132f, 46f));

                UnityEventTools.AddIntPersistentListener(
                    jobButton.onClick,
                    controller.SelectJob,
                    job);
            }

            Button male =
                CreateButton(
                    "Male",
                    leftPanel,
                    "MALE",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-105f, -30f),
                    new Vector2(180f, 46f));

            UnityEventTools.AddIntPersistentListener(
                male.onClick,
                controller.SelectSex,
                0);

            Button female =
                CreateButton(
                    "Female",
                    leftPanel,
                    "FEMALE",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(105f, -30f),
                    new Vector2(180f, 46f));

            UnityEventTools.AddIntPersistentListener(
                female.onClick,
                controller.SelectSex,
                1);

            CreatePairControl(
                leftPanel,
                "FACE",
                -105f,
                controller.PreviousFace,
                controller.NextFace);

            CreatePairControl(
                leftPanel,
                "HAIR",
                -185f,
                controller.PreviousHair,
                controller.NextHair);

            Button basic =
                CreateButton(
                    "BasicMode",
                    leftPanel,
                    "BASIC",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-105f, -260f),
                    new Vector2(180f, 46f));

            UnityEventTools.AddPersistentListener(
                basic.onClick,
                controller.SelectModeBasic);

            Button ultimate =
                CreateButton(
                    "UltimateMode",
                    leftPanel,
                    "ULTIMATE",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(105f, -260f),
                    new Vector2(180f, 46f));

            UnityEventTools.AddPersistentListener(
                ultimate.onClick,
                controller.SelectModeUltimate);

            Button create =
                CreateButton(
                    "CreateButton",
                    leftPanel,
                    "CREATE",
                    new Vector2(0.5f, 0f),
                    new Vector2(-105f, 55f),
                    new Vector2(180f, 54f));

            Button cancel =
                CreateButton(
                    "CancelButton",
                    leftPanel,
                    "CANCEL",
                    new Vector2(0.5f, 0f),
                    new Vector2(105f, 55f),
                    new Vector2(180f, 54f));

            Text status =
                CreateText(
                    "Status",
                    leftPanel,
                    string.Empty,
                    15,
                    TextAnchor.MiddleCenter);

            status.rectTransform.anchorMin =
                status.rectTransform.anchorMax =
                    new Vector2(0.5f, 0f);

            status.rectTransform.sizeDelta =
                new Vector2(480f, 46f);

            status.rectTransform.anchoredPosition =
                new Vector2(0f, 8f);

            controller.Bind(
                name,
                status,
                selection,
                create,
                cancel);

            controller.Configure(
                1,
                0,
                0,
                0,
                0,
                0);
        }

        private static void CreatePairControl(
            RectTransform parent,
            string label,
            float y,
            UnityEngine.Events.UnityAction previous,
            UnityEngine.Events.UnityAction next)
        {
            Text text =
                CreateText(
                    label + "_Label",
                    parent,
                    label,
                    18,
                    TextAnchor.MiddleCenter);

            text.rectTransform.anchorMin =
                text.rectTransform.anchorMax =
                    new Vector2(0.5f, 0.5f);

            text.rectTransform.sizeDelta =
                new Vector2(180f, 42f);

            text.rectTransform.anchoredPosition =
                new Vector2(0f, y);

            Button prev =
                CreateButton(
                    label + "_Previous",
                    parent,
                    "<",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-140f, y),
                    new Vector2(60f, 42f));

            Button nextButton =
                CreateButton(
                    label + "_Next",
                    parent,
                    ">",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(140f, y),
                    new Vector2(60f, 42f));

            UnityEventTools.AddPersistentListener(
                prev.onClick,
                previous);

            UnityEventTools.AddPersistentListener(
                nextButton.onClick,
                next);
        }

        private static GameObject InstantiateCanonicalActor(
            Vector3 position,
            Quaternion rotation)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CharacterPrefabPath);

            if (prefab == null)
                throw new InvalidOperationException(
                    "Canonical character prefab is missing after import.");

            GameObject actor =
                PrefabUtility.InstantiatePrefab(prefab)
                    as GameObject;

            if (actor == null)
                throw new InvalidOperationException(
                    "Could not instantiate canonical character prefab.");

            actor.name = "CanonicalCharacterPreview";
            actor.transform.position = position;
            actor.transform.rotation = rotation;

            return actor;
        }

        private static Camera CreateCamera(
            Vector3 position,
            Vector3 target,
            float fov)
        {
            GameObject go =
                new GameObject("Main Camera");

            go.tag = "MainCamera";
            go.transform.position = position;
            go.transform.rotation =
                Quaternion.LookRotation(
                    (target - position).normalized,
                    Vector3.up);

            Camera camera =
                go.AddComponent<Camera>();

            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            camera.clearFlags =
                CameraClearFlags.SolidColor;

            camera.backgroundColor = Color.black;

            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateBackdrop(
            Camera camera,
            Texture2D texture,
            string name,
            float distance)
        {
            if (texture == null)
                throw new InvalidOperationException(
                    name + " texture is missing.");

            GameObject quad =
                GameObject.CreatePrimitive(PrimitiveType.Quad);

            quad.name = name;

            Collider collider =
                quad.GetComponent<Collider>();

            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            float aspect =
                ReferenceResolution.x / ReferenceResolution.y;

            float height =
                2f * distance *
                Mathf.Tan(
                    camera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad);

            float width = height * aspect;

            quad.transform.position =
                camera.transform.position +
                camera.transform.forward * distance;

            quad.transform.rotation =
                camera.transform.rotation;

            quad.transform.localScale =
                new Vector3(width, height, 1f);

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Texture");

            Material material =
                new Material(shader)
                {
                    name = name + "_Material"
                };

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else
                material.mainTexture = texture;

            quad.GetComponent<Renderer>().sharedMaterial =
                material;
        }

        private static void AddSelectionLighting()
        {
            GameObject key =
                new GameObject("KeyLight");

            Light keyLight =
                key.AddComponent<Light>();

            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;

            key.transform.rotation =
                Quaternion.Euler(42f, -28f, 0f);

            GameObject fill =
                new GameObject("FillLight");

            Light fillLight =
                fill.AddComponent<Light>();

            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.38f;
            fillLight.shadows = LightShadows.None;

            fill.transform.rotation =
                Quaternion.Euler(25f, 150f, 0f);

            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Trilight;

            RenderSettings.ambientIntensity = 0.8f;
        }

        private static Canvas CreateCanvas(string name)
        {
            GameObject go =
                new GameObject(
                    name,
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

        private static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    anchor,
                    anchor,
                    size,
                    position,
                    new Vector2(0.5f, 0.5f));

            Image image =
                rect.gameObject.AddComponent<Image>();

            image.color =
                new Color(0.28f, 0.12f, 0.035f, 0.92f);

            Button button =
                rect.gameObject.AddComponent<Button>();

            button.targetGraphic = image;

            Text text =
                CreateText(
                    "Label",
                    rect,
                    label,
                    18,
                    TextAnchor.MiddleCenter);

            text.fontStyle = FontStyle.Bold;
            return button;
        }

        private static InputField CreateInput(
            string name,
            RectTransform parent,
            string placeholder,
            Vector2 position)
        {
            RectTransform root =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(420f, 48f),
                    position,
                    new Vector2(0.5f, 0.5f));

            Image image =
                root.gameObject.AddComponent<Image>();

            image.color =
                new Color(0.02f, 0.015f, 0.01f, 0.88f);

            Text value =
                CreateText(
                    "Text",
                    root,
                    string.Empty,
                    20,
                    TextAnchor.MiddleLeft);

            SetOffsets(
                value.rectTransform,
                14f,
                0f,
                -14f,
                0f);

            Text hint =
                CreateText(
                    "Placeholder",
                    root,
                    placeholder,
                    20,
                    TextAnchor.MiddleLeft);

            hint.color =
                new Color(1f, 1f, 1f, 0.42f);

            SetOffsets(
                hint.rectTransform,
                14f,
                0f,
                -14f,
                0f);

            InputField input =
                root.gameObject.AddComponent<InputField>();

            input.textComponent = value;
            input.placeholder = hint;
            input.characterLimit = 19;

            return input;
        }

        private static void SetOffsets(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.offsetMin =
                new Vector2(left, bottom);

            rect.offsetMax =
                new Vector2(right, top);
        }

        private static void EnsureGeneratedSceneFolder()
        {
            EnsureFolder(
                "Assets/DreynoxMMORPG/Game/Scenes/Generated");
        }

        private static void EnsureFolder(string path)
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
