using System.Collections.Generic;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyCharacterMakeVisualRuntimeTests
    {
        [Test]
        public void VisualControllerTracksClassFactionSexAndAppearance()
        {
            GameObject root =
                new GameObject(
                    "CharacterMakeVisualFixture");

            Texture2D[] classBars =
                CreateTextures(
                    6,
                    "ClassBars");

            Texture2D[] weaponIcons =
                CreateTextures(
                    17,
                    "WeaponIcon");

            Texture2D[] weaponTexts =
                CreateTextures(
                    17,
                    "WeaponText");

            try
            {
                LegacyCharacterMakeScreenController screen =
                    root.AddComponent<
                        LegacyCharacterMakeScreenController>();

                RawImage[] classVisuals =
                    CreateRawImages(
                        root,
                        6,
                        "ClassVisual",
                        96,
                        512);

                Text[] classLabels =
                    CreateTexts(
                        root,
                        6,
                        "ClassLabel");

                RawImage male =
                    CreateRawImage(
                        root,
                        "Male",
                        58,
                        256);

                RawImage female =
                    CreateRawImage(
                        root,
                        "Female",
                        58,
                        256);

                RawImage bars =
                    CreateRawImage(
                        root,
                        "Bars",
                        256,
                        128);

                RawImage[] weaponIconSlots =
                    CreateRawImages(
                        root,
                        7,
                        "WeaponIconSlot",
                        64,
                        64);

                RawImage[] weaponTextSlots =
                    CreateRawImages(
                        root,
                        7,
                        "WeaponTextSlot",
                        128,
                        32);

                Text explanation =
                    CreateText(
                        root,
                        "Explanation");

                Image[] faceHighlights =
                    CreateImages(
                        root,
                        5,
                        "FaceHighlight");

                Image[] hairHighlights =
                    CreateImages(
                        root,
                        5,
                        "HairHighlight");

                LegacyCharacterMakeVisualController visual =
                    root.AddComponent<
                        LegacyCharacterMakeVisualController>();

                screen.Configure(
                    1,
                    0,
                    0,
                    0,
                    0,
                    0);

                visual.Bind(
                    screen,
                    classVisuals,
                    classLabels,
                    male,
                    female,
                    bars,
                    classBars,
                    weaponIconSlots,
                    weaponTextSlots,
                    weaponIcons,
                    weaponTexts,
                    explanation,
                    faceHighlights,
                    hairHighlights);

                Assert.AreSame(
                    classBars[0],
                    bars.texture);

                Assert.AreSame(
                    weaponIcons[
                        (int)LegacyCharacterWeaponKind.OneHandSword],
                    weaponIconSlots[0].texture);

                Assert.AreSame(
                    weaponIcons[
                        (int)LegacyCharacterWeaponKind.Shield],
                    weaponIconSlots[6].texture);

                Assert.AreEqual(
                    "Fighter",
                    classLabels[0].text);

                Assert.AreEqual(
                    "Priest",
                    classLabels[2].text);

                Assert.Greater(
                    faceHighlights[0].color.a,
                    faceHighlights[1].color.a);

                screen.SelectJob(
                    (int)LegacyCharacterJob.Priest);

                Assert.AreEqual(
                    (int)LegacyCharacterFamily.Human,
                    screen.Family);

                Assert.AreSame(
                    classBars[2],
                    bars.texture);

                Assert.AreSame(
                    weaponIcons[
                        (int)LegacyCharacterWeaponKind.Dagger],
                    weaponIconSlots[0].texture);

                Assert.AreSame(
                    weaponIcons[
                        (int)LegacyCharacterWeaponKind.Staff],
                    weaponIconSlots[1].texture);

                Assert.IsFalse(
                    weaponIconSlots[2]
                        .gameObject
                        .activeSelf);

                screen.SelectJob(
                    (int)LegacyCharacterJob.Ranger);

                Assert.AreEqual(
                    (int)LegacyCharacterFamily.Elf,
                    screen.Family);

                Assert.AreEqual(
                    "Ranger",
                    classLabels[3].text);

                Assert.AreSame(
                    classBars[3],
                    bars.texture);

                Assert.AreSame(
                    weaponIcons[
                        (int)LegacyCharacterWeaponKind.ReversedSword],
                    weaponIconSlots[0].texture);

                screen.SelectSex(1);
                screen.SelectFace(4);
                screen.SelectHair(3);

                Assert.Less(
                    female.uvRect.y,
                    male.uvRect.y);

                Assert.Greater(
                    faceHighlights[4].color.a,
                    faceHighlights[0].color.a);

                Assert.Greater(
                    hairHighlights[3].color.a,
                    hairHighlights[0].color.a);

                screen.SelectFamily(
                    (int)LegacyCharacterFamily.Vile);

                screen.SelectJob(
                    (int)LegacyCharacterJob.Priest);

                Assert.AreEqual(
                    (int)LegacyCharacterFamily.Vile,
                    screen.Family);

                Assert.AreEqual(
                    "Oracle",
                    classLabels[2].text);
            }
            finally
            {
                var ownedTextures =
                    new HashSet<Texture2D>();

                if (root != null)
                {
                    RawImage[] images =
                        root.GetComponentsInChildren<
                            RawImage>(
                                true);

                    for (int i = 0;
                         i < images.Length;
                         i++)
                    {
                        Texture2D texture =
                            images[i].texture
                                as Texture2D;

                        if (texture != null)
                            ownedTextures.Add(
                                texture);
                    }
                }

                AddTextures(
                    ownedTextures,
                    classBars);

                AddTextures(
                    ownedTextures,
                    weaponIcons);

                AddTextures(
                    ownedTextures,
                    weaponTexts);

                Object.DestroyImmediate(root);

                foreach (Texture2D texture in
                         ownedTextures)
                {
                    if (texture != null)
                        Object.DestroyImmediate(
                            texture);
                }
            }
        }

        private static RawImage[] CreateRawImages(
            GameObject parent,
            int count,
            string prefix,
            int width,
            int height)
        {
            RawImage[] result =
                new RawImage[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                result[i] =
                    CreateRawImage(
                        parent,
                        prefix + "_" + i,
                        width,
                        height);
            }

            return result;
        }

        private static RawImage CreateRawImage(
            GameObject parent,
            string name,
            int width,
            int height)
        {
            GameObject go =
                new GameObject(
                    name);

            go.transform.SetParent(
                parent.transform,
                false);

            RawImage image =
                go.AddComponent<RawImage>();

            image.texture =
                new Texture2D(
                    width,
                    height);

            return image;
        }

        private static Text[] CreateTexts(
            GameObject parent,
            int count,
            string prefix)
        {
            Text[] result =
                new Text[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                result[i] =
                    CreateText(
                        parent,
                        prefix + "_" + i);
            }

            return result;
        }

        private static Text CreateText(
            GameObject parent,
            string name)
        {
            GameObject go =
                new GameObject(
                    name);

            go.transform.SetParent(
                parent.transform,
                false);

            return go.AddComponent<Text>();
        }

        private static Image[] CreateImages(
            GameObject parent,
            int count,
            string prefix)
        {
            Image[] result =
                new Image[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                GameObject go =
                    new GameObject(
                        prefix + "_" + i);

                go.transform.SetParent(
                    parent.transform,
                    false);

                result[i] =
                    go.AddComponent<Image>();
            }

            return result;
        }

        private static Texture2D[] CreateTextures(
            int count,
            string prefix)
        {
            Texture2D[] result =
                new Texture2D[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                result[i] =
                    new Texture2D(
                        4,
                        4)
                    {
                        name =
                            prefix +
                            "_" +
                            i
                    };
            }

            return result;
        }

        private static void AddTextures(
            ISet<Texture2D> target,
            Texture2D[] textures)
        {
            if (target == null ||
                textures == null)
                return;

            for (int i = 0;
                 i < textures.Length;
                 i++)
            {
                if (textures[i] != null)
                    target.Add(
                        textures[i]);
            }
        }
    }
}
