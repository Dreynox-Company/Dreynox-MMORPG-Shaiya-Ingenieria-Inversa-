using System;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class LegacyCharacterMakeVisualController : MonoBehaviour
    {
        [SerializeField] private LegacyCharacterMakeScreenController screen;
        [SerializeField] private RawImage[] classVisuals = new RawImage[6];
        [SerializeField] private Text[] classLabels = new Text[6];
        [SerializeField] private RawImage maleVisual;
        [SerializeField] private RawImage femaleVisual;
        [SerializeField] private RawImage classBars;
        [SerializeField] private Texture[] classBarTextures = new Texture[6];
        [SerializeField] private RawImage[] weaponIcons = new RawImage[7];
        [SerializeField] private RawImage[] weaponTexts = new RawImage[7];
        [SerializeField] private Texture[] weaponIconTextures = new Texture[17];
        [SerializeField] private Texture[] weaponTextTextures = new Texture[17];
        [SerializeField] private Text explanationBody;
        [SerializeField] private RawImage[] faceThumbnails = new RawImage[5];
        [SerializeField] private RawImage[] hairThumbnails = new RawImage[5];
        [SerializeField] private Texture[] faceThumbnailTextures = new Texture[40];
        [SerializeField] private Texture[] hairThumbnailTextures = new Texture[40];
        [SerializeField] private Image[] faceHighlights = new Image[5];
        [SerializeField] private Image[] hairHighlights = new Image[5];

        private bool _hooked;

        public void Bind(
            LegacyCharacterMakeScreenController controller,
            RawImage[] classImages,
            Text[] classLabelTexts,
            RawImage male,
            RawImage female,
            RawImage bars,
            Texture[] barsByVisualSlot,
            RawImage[] weaponIconSlots,
            RawImage[] weaponTextSlots,
            Texture[] allWeaponIcons,
            Texture[] allWeaponTexts,
            Text explanation,
            RawImage[] faceImages,
            RawImage[] hairImages,
            Texture[] allFaceTextures,
            Texture[] allHairTextures,
            Image[] faces,
            Image[] hairs)
        {
            Unhook();

            screen =
                controller ??
                throw new ArgumentNullException(nameof(controller));

            classVisuals = ValidateArray(classImages, 6, nameof(classImages));
            classLabels = ValidateArray(classLabelTexts, 6, nameof(classLabelTexts));
            maleVisual = male ?? throw new ArgumentNullException(nameof(male));
            femaleVisual = female ?? throw new ArgumentNullException(nameof(female));
            classBars = bars ?? throw new ArgumentNullException(nameof(bars));
            classBarTextures = ValidateArray(barsByVisualSlot, 6, nameof(barsByVisualSlot));
            weaponIcons = ValidateArray(weaponIconSlots, 7, nameof(weaponIconSlots));
            weaponTexts = ValidateArray(weaponTextSlots, 7, nameof(weaponTextSlots));
            weaponIconTextures = ValidateArray(allWeaponIcons, 17, nameof(allWeaponIcons));
            weaponTextTextures = ValidateArray(allWeaponTexts, 17, nameof(allWeaponTexts));
            explanationBody = explanation ?? throw new ArgumentNullException(nameof(explanation));
            faceThumbnails = ValidateArray(faceImages, 5, nameof(faceImages));
            hairThumbnails = ValidateArray(hairImages, 5, nameof(hairImages));
            faceThumbnailTextures = ValidateArray(allFaceTextures, 40, nameof(allFaceTextures));
            hairThumbnailTextures = ValidateArray(allHairTextures, 40, nameof(allHairTextures));
            faceHighlights = ValidateArray(faces, 5, nameof(faces));
            hairHighlights = ValidateArray(hairs, 5, nameof(hairs));

            Hook();
            Refresh();
        }

        private void OnEnable()
        {
            Hook();
            Refresh();
        }

        private void OnDisable()
        {
            Unhook();
        }

        public void Refresh()
        {
            if (screen == null)
                return;

            LegacyCharacterClassVisualProfile profile =
                LegacyCharacterClassVisualCore.Resolve(
                    screen.Family,
                    screen.Job);

            RefreshClassButtons();
            RefreshGender();
            RefreshClassInfo(profile);
            RefreshAppearance();
        }

        private void RefreshClassButtons()
        {
            int selectedSlot =
                LegacyCharacterMakeLayoutCore.VisualSlotForJob(
                    screen.Job);

            bool fury =
                screen.Family >=
                (int)LegacyCharacterFamily.DeathEater;

            int factionFamily =
                fury
                    ? (int)LegacyCharacterFamily.DeathEater
                    : (int)LegacyCharacterFamily.Human;

            for (int slot = 0;
                 slot < classVisuals.Length;
                 slot++)
            {
                int job =
                    LegacyCharacterMakeLayoutCore.JobForVisualSlot(
                        slot);

                SetAtlasState(
                    classVisuals[slot],
                    128,
                    new Vector2(96f, 78f),
                    slot == selectedSlot);

                int family =
                    LegacyCharacterRigCore.ResolveFamilyForJob(
                        factionFamily,
                        job);

                classLabels[slot].text =
                    LegacyCharacterRigCore.ResolveDisplayJob(
                        family,
                        job);
            }
        }

        private void RefreshGender()
        {
            SetAtlasState(
                maleVisual,
                64,
                new Vector2(58f, 57f),
                screen.Sex == 0);

            SetAtlasState(
                femaleVisual,
                64,
                new Vector2(58f, 57f),
                screen.Sex == 1);
        }

        private void RefreshClassInfo(
            LegacyCharacterClassVisualProfile profile)
        {
            classBars.texture =
                classBarTextures[profile.VisualSlot];

            explanationBody.text =
                profile.Explanation;

            for (int slot = 0;
                 slot < weaponIcons.Length;
                 slot++)
            {
                bool active =
                    slot < profile.Weapons.Count;

                weaponIcons[slot].gameObject.SetActive(active);
                weaponTexts[slot].gameObject.SetActive(active);

                if (!active)
                    continue;

                int kind =
                    (int)profile.Weapons[slot];

                weaponIcons[slot].texture =
                    weaponIconTextures[kind];

                weaponTexts[slot].texture =
                    weaponTextTextures[kind];
            }
        }

        private void RefreshAppearance()
        {
            int baseIndex =
                LegacyCharacterAppearanceUiCore
                    .ResolveGroupIndex(
                        screen.Family,
                        screen.Sex) *
                LegacyCharacterAppearanceUiCore
                    .VariantCount;

            for (int i = 0;
                 i <
                    LegacyCharacterAppearanceUiCore
                        .VariantCount;
                 i++)
            {
                faceThumbnails[i].texture =
                    faceThumbnailTextures[
                        baseIndex + i];

                hairThumbnails[i].texture =
                    hairThumbnailTextures[
                        baseIndex + i];

                SetHighlight(
                    faceHighlights[i],
                    i == screen.Face);

                SetHighlight(
                    hairHighlights[i],
                    i == screen.Hair);
            }
        }

        private void Hook()
        {
            if (_hooked ||
                screen == null)
                return;

            screen.AppearanceChanged += Refresh;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked ||
                screen == null)
                return;

            screen.AppearanceChanged -= Refresh;
            _hooked = false;
        }

        private static void SetHighlight(
            Image image,
            bool selected)
        {
            if (image == null)
                return;

            image.color =
                selected
                    ? new Color(1f, 0.82f, 0.16f, 0.24f)
                    : new Color(1f, 1f, 1f, 0.035f);
        }

        private static void SetAtlasState(
            RawImage image,
            int stateHeight,
            Vector2 sourceSize,
            bool selected)
        {
            if (image == null ||
                image.texture == null)
                return;

            float sourceY =
                selected
                    ? stateHeight * 3f
                    : 0f;

            float v =
                1f -
                (sourceY + sourceSize.y) /
                image.texture.height;

            image.uvRect =
                new Rect(
                    0f,
                    v,
                    sourceSize.x / image.texture.width,
                    sourceSize.y / image.texture.height);
        }

        private static T[] ValidateArray<T>(
            T[] values,
            int requiredLength,
            string parameterName)
            where T : UnityEngine.Object
        {
            if (values == null ||
                values.Length != requiredLength)
            {
                throw new ArgumentException(
                    "Expected exactly " +
                    requiredLength +
                    " values.",
                    parameterName);
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    throw new ArgumentException(
                        parameterName +
                        "[" + i + "] is null.",
                        parameterName);
                }
            }

            return (T[])values.Clone();
        }
    }
}
