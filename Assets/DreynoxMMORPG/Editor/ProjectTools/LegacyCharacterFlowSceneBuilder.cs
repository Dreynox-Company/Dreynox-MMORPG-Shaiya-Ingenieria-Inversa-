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
            new Vector2(1024f, 768f);

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Character Select Scene")]
        public static void BuildCharacterSelect()
        {
            LegacyUiAssetImporter.ImportCanonicalCharacterSelectUi();
            LegacyCharacterImporter.ImportCanonicalHumanMale003();
            EnsureGeneratedSceneFolder();

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            CreateEventSystem();

            Camera camera =
                CreateCamera(
                    new Vector3(0f, 1.55f, -6.2f),
                    new Vector3(0.75f, 1.0f, 0f),
                    35f);

            Texture2D background =
                LegacyUiAssetImporter
                    .LoadCharacterSelectTexture(
                        "selectbg.tga");

            CreateBackdrop(
                camera,
                background,
                "CharacterSelect_Backdrop",
                30f);

            GameObject actor =
                InstantiateCanonicalActor(
                    new Vector3(0.85f, 0f, 0f),
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
                CharacterSelectScenePath +
                " · selectbg.tga parity backdrop active.");
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

            int loginEffects =
                LegacyDungeonPreviewEnvironmentImporter
                    .AttachRuntimeEffects(
                        corpus,
                        environment,
                        actor.transform);

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
                CharacterMakeScenePath +
                " · DG meshes=" +
                environment.Dungeon.Source.MeshCount +
                " · lightmaps=" +
                environment.Dungeon.Source.LightmapCount +
                " · login EFT placements=" +
                loginEffects +
                ".");
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

            Texture2D slotAtlas =
                LegacyUiAssetImporter
                    .LoadCharacterSelectTexture(
                        "selectbtn_us.tga");

            Texture2D startAtlas =
                LegacyUiAssetImporter
                    .LoadCharacterSelectTexture(
                        "select_start_usa.tga");

            if (slotAtlas == null)
                throw new InvalidOperationException(
                    "Canonical CharacterSelect slot atlas is missing.");

            if (startAtlas == null)
                throw new InvalidOperationException(
                    "Canonical CharacterSelect Game Start atlas is missing.");

            Button[] buttons =
                new Button[slotCount];

            Text[] names =
                new Text[slotCount];

            Text[] metas =
                new Text[slotCount];

            for (int i = 0;
                 i < slotCount;
                 i++)
            {
                float y =
                    22f +
                    i * 132f;

                CreateLegacyAtlasLayer(
                    "CharacterSlotVisual_" + i,
                    root,
                    slotAtlas,
                    new Vector2(
                        45f,
                        y),
                    new Vector2(
                        334f,
                        118f),
                    new Rect(
                        0f,
                        0f,
                        334f,
                        118f));

                Button button =
                    CreateTransparentButton(
                        "CharacterSlot_" + i,
                        root,
                        new Vector2(
                            45f,
                            y),
                        new Vector2(
                            334f,
                            118f));

                buttons[i] =
                    button;

                Text name =
                    CreateLegacyTopLeftText(
                        "Name_" + i,
                        root,
                        string.Empty,
                        new Vector2(
                            116f,
                            y + 8f),
                        new Vector2(
                            130f,
                            16f),
                        10,
                        new Color(
                            0.35f,
                            0.86f,
                            1f,
                            1f),
                        TextAnchor.MiddleLeft);

                name.raycastTarget =
                    false;

                names[i] =
                    name;

                Text meta =
                    CreateLegacyTopLeftText(
                        "Meta_" + i,
                        root,
                        "Please create a character.",
                        new Vector2(
                            64f,
                            y + 8f),
                        new Vector2(
                            292f,
                            99f),
                        10,
                        Color.white,
                        TextAnchor.MiddleCenter);

                meta.raycastTarget =
                    false;

                metas[i] =
                    meta;
            }

            CreateLegacyAtlasLayer(
                "GameStartVisual",
                root,
                startAtlas,
                new Vector2(
                    596f,
                    675f),
                new Vector2(
                    246f,
                    60f),
                new Rect(
                    4f,
                    2f,
                    246f,
                    60f));

            Button startButton =
                CreateTransparentButton(
                    "StartButton",
                    root,
                    new Vector2(
                        596f,
                        675f),
                    new Vector2(
                        246f,
                        60f));

            Button createButton =
                CreateLegacyRedButton(
                    "CreateButton",
                    root,
                    "Create Character",
                    new Vector2(
                        92f,
                        666f),
                    new Vector2(
                        114f,
                        36f));

            Button deleteButton =
                CreateLegacyRedButton(
                    "DeleteButton",
                    root,
                    "Delete Character",
                    new Vector2(
                        212f,
                        666f),
                    new Vector2(
                        114f,
                        36f));

            Button optionButton =
                CreateLegacyRedButton(
                    "OptionButton",
                    root,
                    "Option Setting",
                    new Vector2(
                        212f,
                        708f),
                    new Vector2(
                        114f,
                        36f));

            optionButton.interactable =
                true;

            Text status =
                CreateText(
                    "Status",
                    root,
                    string.Empty,
                    1,
                    TextAnchor.MiddleCenter);

            status.color =
                Color.clear;

            status.raycastTarget =
                false;

            controller.Bind(
                buttons,
                names,
                metas,
                startButton,
                createButton,
                deleteButton,
                status);
        }

        private static void BuildCharacterMakeUi(
            RectTransform root,
            LegacyCharacterMakeScreenController controller)
        {
            Texture2D infoFrame =
                RequireCharacterMakeTextureByKey(
                    "character.make.infoFrame");

            Texture2D basicInfo =
                RequireCharacterMakeTexture(
                    "basicinfo_bg.tga");

            Texture2D classInfoBackground =
                RequireCharacterMakeTextureByKey(
                    "character.make.classInfo.background");

            Texture2D classFigure =
                RequireCharacterMakeTextureByKey(
                    "character.make.classInfo.fighterBars");

            CreateLegacyTextureLayer(
                "ExplanationFrame",
                root,
                infoFrame,
                new Vector2(5f, 23f),
                new Vector2(334f, 223f),
                new Rect(0f, 0f, 334f, 223f));

            CreateLegacyTextureLayer(
                "BasicInfoFrame",
                root,
                basicInfo,
                new Vector2(5f, 302f),
                new Vector2(334f, 466f),
                new Rect(0f, 0f, 334f, 466f));

            CreateLegacyTextureLayer(
                "ClassInfoFrame",
                root,
                classInfoBackground,
                new Vector2(738f, 33f),
                new Vector2(288f, 440f),
                new Rect(0f, 0f, 288f, 440f));

            CreateLegacyTopLeftText(
                "ExplanationTitle",
                root,
                "Explanation",
                new Vector2(34f, 34f),
                new Vector2(120f, 18f),
                11,
                Color.white,
                TextAnchor.MiddleLeft);

            CreateLegacyTopLeftText(
                "ExplanationBody",
                root,
                "The Fighter is your standard melee combatant. Up close\n" +
                "and personal is how the Fighter prefers confrontation.\n" +
                "Physical attack power is the focus of the Fighter, but don't\n" +
                "be fooled. A certain amount of Magical Points (MP) is\n" +
                "needed to power the Fighter's devastating Special Skills.\n\n" +
                "Characteristics:\n" +
                "· Wide range of available weapons\n" +
                "· Powerful physical attacks",
                new Vector2(33f, 68f),
                new Vector2(288f, 164f),
                10,
                Color.white,
                TextAnchor.UpperLeft);

            Texture2D tabAtlas =
                RequireCharacterMakeTexture(
                    "create_tab_button.tga");

            CreateLegacyAtlasLayer(
                "BasicTab",
                root,
                tabAtlas,
                new Vector2(35f, 278f),
                new Vector2(92f, 32f),
                new Rect(0f, 96f, 92f, 32f));

            CreateLegacyAtlasLayer(
                "AppearanceTab",
                root,
                tabAtlas,
                new Vector2(128f, 286f),
                new Vector2(92f, 32f),
                new Rect(0f, 0f, 92f, 32f));

            CreateLegacyAtlasLayer(
                "ModeTab",
                root,
                tabAtlas,
                new Vector2(221f, 286f),
                new Vector2(92f, 32f),
                new Rect(0f, 0f, 92f, 32f));

            CreateLegacyTopLeftText(
                "BasicTabLabel",
                root,
                "Basic Info",
                new Vector2(43f, 286f),
                new Vector2(76f, 20f),
                11,
                Color.white,
                TextAnchor.MiddleCenter);

            CreateLegacyTopLeftText(
                "AppearanceTabLabel",
                root,
                "Appearance",
                new Vector2(136f, 294f),
                new Vector2(76f, 20f),
                11,
                Color.white,
                TextAnchor.MiddleCenter);

            CreateLegacyTopLeftText(
                "ModeTabLabel",
                root,
                "Mode",
                new Vector2(229f, 294f),
                new Vector2(76f, 20f),
                11,
                Color.white,
                TextAnchor.MiddleCenter);

            CreateLegacyTopLeftText(
                "NameLabel",
                root,
                "Name",
                new Vector2(55f, 337f),
                new Vector2(56f, 18f),
                10,
                new Color(1f, 0.86f, 0.12f, 1f),
                TextAnchor.MiddleCenter);

            InputField name =
                CreateLegacyInput(
                    "CharacterName",
                    root,
                    new Vector2(28f, 356f),
                    new Vector2(188f, 24f));

            Button nameCheck =
                CreateLegacyRedButton(
                    "NameCheck",
                    root,
                    "Name Check",
                    new Vector2(228f, 355f),
                    new Vector2(94f, 27f));

            CreateLegacyTopLeftText(
                "ClassLabel",
                root,
                "Class",
                new Vector2(54f, 405f),
                new Vector2(58f, 18f),
                10,
                new Color(1f, 0.86f, 0.12f, 1f),
                TextAnchor.MiddleCenter);

            string[] classTextureKeys =
            {
                "character.make.class.fighter",
                "character.make.class.defender",
                "character.make.class.priest",
                "character.make.class.ranger",
                "character.make.class.archer",
                "character.make.class.mage"
            };

            string[] classLabels =
            {
                "Fighter",
                "Defender",
                "Priest",
                "Ranger",
                "Archer",
                "Mage"
            };

            Vector2[] classPositions =
            {
                new Vector2(27f, 428f),
                new Vector2(127f, 428f),
                new Vector2(227f, 428f),
                new Vector2(27f, 514f),
                new Vector2(127f, 514f),
                new Vector2(227f, 514f)
            };

            for (int job = 0;
                 job < classTextureKeys.Length;
                 job++)
            {
                Texture2D atlas =
                    RequireCharacterMakeTextureByKey(
                        classTextureKeys[job]);

                int state =
                    job == 0
                        ? 3
                        : 0;

                CreateLegacyAtlasLayer(
                    "ClassVisual_" + job,
                    root,
                    atlas,
                    classPositions[job],
                    new Vector2(96f, 78f),
                    new Rect(
                        0f,
                        state * 128f,
                        96f,
                        78f));

                CreateLegacyTopLeftText(
                    "ClassLabel_" + job,
                    root,
                    classLabels[job],
                    classPositions[job] +
                    new Vector2(8f, 58f),
                    new Vector2(80f, 18f),
                    11,
                    Color.white,
                    TextAnchor.MiddleCenter);

                Button jobButton =
                    CreateTransparentButton(
                        "ClassHit_" + job,
                        root,
                        classPositions[job],
                        new Vector2(96f, 78f));

                UnityEventTools
                    .AddIntPersistentListener(
                        jobButton.onClick,
                        controller.SelectJob,
                        job);
            }

            CreateLegacyTopLeftText(
                "GenderLabel",
                root,
                "Gender",
                new Vector2(52f, 615f),
                new Vector2(70f, 18f),
                10,
                new Color(1f, 0.86f, 0.12f, 1f),
                TextAnchor.MiddleCenter);

            Texture2D maleAtlas =
                RequireCharacterMakeTextureByKey(
                    "character.make.sex.maleAtlas");

            Texture2D femaleAtlas =
                RequireCharacterMakeTextureByKey(
                    "character.make.sex.femaleAtlas");

            CreateLegacyAtlasLayer(
                "MaleVisual",
                root,
                maleAtlas,
                new Vector2(114f, 640f),
                new Vector2(58f, 57f),
                new Rect(0f, 192f, 58f, 57f));

            CreateLegacyAtlasLayer(
                "FemaleVisual",
                root,
                femaleAtlas,
                new Vector2(176f, 640f),
                new Vector2(58f, 57f),
                new Rect(0f, 0f, 58f, 57f));

            Button male =
                CreateTransparentButton(
                    "Male",
                    root,
                    new Vector2(114f, 640f),
                    new Vector2(58f, 57f));

            Button female =
                CreateTransparentButton(
                    "Female",
                    root,
                    new Vector2(176f, 640f),
                    new Vector2(58f, 57f));

            UnityEventTools.AddIntPersistentListener(
                male.onClick,
                controller.SelectSex,
                0);

            UnityEventTools.AddIntPersistentListener(
                female.onClick,
                controller.SelectSex,
                1);

            BuildNativeFighterClassInfo(
                root,
                classFigure);

            Button cancel =
                CreateLegacyRedButton(
                    "CancelButton",
                    root,
                    "Back",
                    new Vector2(779f, 716f),
                    new Vector2(113f, 38f));

            Button create =
                CreateLegacyRedButton(
                    "CreateButton",
                    root,
                    "Create",
                    new Vector2(905f, 716f),
                    new Vector2(113f, 38f));

            Text selection =
                CreateText(
                    "Selection",
                    root,
                    string.Empty,
                    1,
                    TextAnchor.MiddleCenter);

            selection.color =
                Color.clear;

            selection.raycastTarget =
                false;

            Text status =
                CreateText(
                    "Status",
                    root,
                    string.Empty,
                    1,
                    TextAnchor.MiddleCenter);

            status.color =
                Color.clear;

            status.raycastTarget =
                false;

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

        private static void BuildNativeFighterClassInfo(
            RectTransform root,
            Texture2D fighterBars)
        {
            CreateLegacyTopLeftText(
                "WeaponHeader",
                root,
                "Weapon",
                new Vector2(941f, 83f),
                new Vector2(70f, 18f),
                10,
                new Color(1f, 0.86f, 0.12f, 1f),
                TextAnchor.MiddleCenter);

            string[] iconKeys =
            {
                "character.make.weapon.oneHandSword.icon",
                "character.make.weapon.twoHandSword.icon",
                "character.make.weapon.dualSword.icon",
                "character.make.weapon.spear.icon",
                "character.make.weapon.oneHandBlunt.icon",
                "character.make.weapon.twoHandBlunt.icon",
                "character.make.weapon.shield.icon"
            };

            string[] labelKeys =
            {
                "character.make.weapon.oneHandSword.text",
                "character.make.weapon.twoHandSword.text",
                "character.make.weapon.dualSword.text",
                "character.make.weapon.spear.text",
                "character.make.weapon.oneHandBlunt.text",
                "character.make.weapon.twoHandBlunt.text",
                "character.make.weapon.shield.text"
            };

            Vector2[] iconPositions =
            {
                new Vector2(758f, 99f),
                new Vector2(823f, 99f),
                new Vector2(888f, 99f),
                new Vector2(953f, 99f),
                new Vector2(823f, 177f),
                new Vector2(888f, 177f),
                new Vector2(953f, 177f)
            };

            for (int i = 0;
                 i < iconKeys.Length;
                 i++)
            {
                Texture2D icon =
                    RequireCharacterMakeTextureByKey(
                        iconKeys[i]);

                Texture2D label =
                    RequireCharacterMakeTextureByKey(
                        labelKeys[i]);

                CreateLegacyTextureLayer(
                    "WeaponIcon_" + i,
                    root,
                    icon,
                    iconPositions[i],
                    new Vector2(64f, 64f),
                    new Rect(
                        0f,
                        0f,
                        icon.width,
                        icon.height));

                CreateLegacyTextureLayer(
                    "WeaponText_" + i,
                    root,
                    label,
                    iconPositions[i] +
                    new Vector2(-32f, 49f),
                    new Vector2(128f, 32f),
                    new Rect(
                        0f,
                        0f,
                        label.width,
                        label.height));
            }

            CreateLegacyTopLeftText(
                "ClassFigureHeader",
                root,
                "Class figure",
                new Vector2(929f, 293f),
                new Vector2(82f, 18f),
                10,
                new Color(1f, 0.86f, 0.12f, 1f),
                TextAnchor.MiddleCenter);

            CreateLegacyTextureLayer(
                "FighterBars",
                root,
                fighterBars,
                new Vector2(762f, 323f),
                new Vector2(256f, 128f),
                new Rect(
                    0f,
                    0f,
                    fighterBars.width,
                    fighterBars.height));

            Texture2D soloParty =
                RequireCharacterMakeTextureByKey(
                    "character.make.classInfo.soloPartyText");

            Texture2D atkDef =
                RequireCharacterMakeTextureByKey(
                    "character.make.classInfo.atkDefText");

            CreateLegacyTextureLayer(
                "SoloPartyText",
                root,
                soloParty,
                new Vector2(945f, 331f),
                new Vector2(64f, 64f),
                new Rect(
                    0f,
                    0f,
                    soloParty.width,
                    soloParty.height));

            CreateLegacyTextureLayer(
                "AtkDefText",
                root,
                atkDef,
                new Vector2(945f, 405f),
                new Vector2(64f, 64f),
                new Rect(
                    0f,
                    0f,
                    atkDef.width,
                    atkDef.height));
        }

        private static Texture2D RequireCharacterMakeTextureByKey(
            string key)
        {
            Texture2D texture =
                LegacyUiAssetImporter
                    .LoadCharacterMakeTextureByKey(
                        key);

            if (texture == null)
            {
                throw new InvalidOperationException(
                    "Canonical CharacterMake texture is missing for key: " +
                    key);
            }

            return texture;
        }

        private static Texture2D RequireCharacterMakeTexture(
            string fileName)
        {
            Texture2D texture =
                LegacyUiAssetImporter
                    .LoadCharacterMakeTexture(
                        fileName);

            if (texture == null)
            {
                throw new InvalidOperationException(
                    "Canonical CharacterMake texture is missing: " +
                    fileName);
            }

            return texture;
        }

        private static RawImage CreateLegacyTextureLayer(
            string name,
            RectTransform parent,
            Texture2D texture,
            Vector2 topLeft,
            Vector2 size,
            Rect sourceTopLeftPixels)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    size,
                    new Vector2(
                        topLeft.x,
                        -topLeft.y),
                    new Vector2(0f, 1f));

            RawImage image =
                rect.gameObject
                    .AddComponent<RawImage>();

            image.texture =
                texture;

            image.raycastTarget =
                false;

            float u =
                sourceTopLeftPixels.x /
                texture.width;

            float v =
                1f -
                (sourceTopLeftPixels.y +
                 sourceTopLeftPixels.height) /
                texture.height;

            image.uvRect =
                new Rect(
                    u,
                    v,
                    sourceTopLeftPixels.width /
                    texture.width,
                    sourceTopLeftPixels.height /
                    texture.height);

            return image;
        }

        private static RawImage CreateLegacyAtlasLayer(
            string name,
            RectTransform parent,
            Texture2D atlas,
            Vector2 topLeft,
            Vector2 size,
            Rect sourceTopLeftPixels)
        {
            return CreateLegacyTextureLayer(
                name,
                parent,
                atlas,
                topLeft,
                size,
                sourceTopLeftPixels);
        }

        private static Text CreateLegacyTopLeftText(
            string name,
            RectTransform parent,
            string value,
            Vector2 topLeft,
            Vector2 size,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    size,
                    new Vector2(
                        topLeft.x,
                        -topLeft.y),
                    new Vector2(0f, 1f));

            Text text =
                rect.gameObject
                    .AddComponent<Text>();

            text.text =
                value;

            text.font =
                Resources
                    .GetBuiltinResource<Font>(
                        "LegacyRuntime.ttf");

            text.fontSize =
                fontSize;

            text.alignment =
                alignment;

            text.color =
                color;

            text.raycastTarget =
                false;

            text.horizontalOverflow =
                HorizontalWrapMode.Wrap;

            text.verticalOverflow =
                VerticalWrapMode.Overflow;

            return text;
        }

        private static InputField CreateLegacyInput(
            string name,
            RectTransform parent,
            Vector2 topLeft,
            Vector2 size)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    size,
                    new Vector2(
                        topLeft.x,
                        -topLeft.y),
                    new Vector2(0f, 1f));

            Image hitGraphic =
                rect.gameObject
                    .AddComponent<Image>();

            hitGraphic.color =
                new Color(
                    0f,
                    0f,
                    0f,
                    0.01f);

            Text value =
                CreateText(
                    "Text",
                    rect,
                    string.Empty,
                    12,
                    TextAnchor.MiddleLeft);

            value.color =
                Color.white;

            SetOffsets(
                value.rectTransform,
                5f,
                0f,
                -5f,
                0f);

            InputField input =
                rect.gameObject
                    .AddComponent<InputField>();

            input.textComponent =
                value;

            input.characterLimit =
                19;

            return input;
        }

        private static Button CreateTransparentButton(
            string name,
            RectTransform parent,
            Vector2 topLeft,
            Vector2 size)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    size,
                    new Vector2(
                        topLeft.x,
                        -topLeft.y),
                    new Vector2(0f, 1f));

            Image image =
                rect.gameObject
                    .AddComponent<Image>();

            image.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.001f);

            Button button =
                rect.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                image;

            return button;
        }

        private static Button CreateLegacyRedButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 topLeft,
            Vector2 size)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    size,
                    new Vector2(
                        topLeft.x,
                        -topLeft.y),
                    new Vector2(0f, 1f));

            Image image =
                rect.gameObject
                    .AddComponent<Image>();

            image.color =
                new Color(
                    0.34f,
                    0.04f,
                    0.04f,
                    0.88f);

            Button button =
                rect.gameObject
                    .AddComponent<Button>();

            button.targetGraphic =
                image;

            Text text =
                CreateText(
                    "Label",
                    rect,
                    label,
                    12,
                    TextAnchor.MiddleCenter);

            text.color =
                Color.white;

            return button;
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
