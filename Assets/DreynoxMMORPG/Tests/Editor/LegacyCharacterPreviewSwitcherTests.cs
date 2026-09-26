using Dreynox.Mmorpg.Parity;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyCharacterPreviewSwitcherTests
    {
        [Test]
        public void SwitcherChangesNativeRigAndReusesRigForAppearanceOnly()
        {
            GameObject root =
                new GameObject("PreviewSwitcherFixture");

            GameObject[] prefabs =
                new GameObject[16];

            try
            {
                for (int rig = 0;
                     rig < prefabs.Length;
                     rig++)
                {
                    prefabs[rig] =
                        CreatePreviewPrefab(
                            "Rig_" + rig);
                }

                LegacyCharacterPreviewSwitcher switcher =
                    root.AddComponent<
                        LegacyCharacterPreviewSwitcher>();

                switcher.Configure(
                    prefabs,
                    Vector3.zero,
                    Vector3.zero,
                    family: 0,
                    job: 0,
                    sex: 0,
                    faceIndex: 0,
                    hairIndex: 0);

                GameObject fighter =
                    switcher.Apply(
                        0,
                        0,
                        0,
                        0,
                        0);

                Assert.IsNotNull(fighter);
                Assert.AreEqual(0, switcher.CurrentRigIndex);

                GameObject sameRig =
                    switcher.Apply(
                        0,
                        1,
                        0,
                        4,
                        3);

                Assert.AreSame(fighter, sameRig);
                Assert.AreEqual(0, switcher.CurrentRigIndex);
                Assert.AreEqual(4, switcher.CurrentFaceIndex);
                Assert.AreEqual(3, switcher.CurrentHairIndex);

                GameObject priest =
                    switcher.Apply(
                        0,
                        5,
                        0,
                        2,
                        1);

                Assert.IsNotNull(priest);
                Assert.AreNotSame(fighter, priest);
                Assert.AreEqual(1, switcher.CurrentRigIndex);
                Assert.AreEqual(2, switcher.CurrentFaceIndex);
                Assert.AreEqual(1, switcher.CurrentHairIndex);

                GameObject femalePriest =
                    switcher.Apply(
                        0,
                        5,
                        1,
                        1,
                        4);

                Assert.AreEqual(3, switcher.CurrentRigIndex);
                Assert.AreEqual(1, switcher.CurrentFaceIndex);
                Assert.AreEqual(4, switcher.CurrentHairIndex);
                Assert.AreEqual("Preview_huwm", femalePriest.name);
            }
            finally
            {
                Object.DestroyImmediate(root);

                for (int i = 0;
                     i < prefabs.Length;
                     i++)
                {
                    if (prefabs[i] != null)
                        Object.DestroyImmediate(prefabs[i]);
                }
            }
        }

        [Test]
        public void FailedReplacementPreservesCurrentModelAndSelection()
        {
            var root = new GameObject("TransactionalPreview");
            var prefabs = new GameObject[16];
            try
            {
                for (int i = 0; i < 16; i++) prefabs[i] = CreatePreviewPrefab("Rig_" + i);
                Object.DestroyImmediate(prefabs[1].GetComponent<LegacyCharacterAppearanceVariants>());
                var switcher = root.AddComponent<LegacyCharacterPreviewSwitcher>();
                switcher.Configure(prefabs, Vector3.zero, Vector3.zero, 0, 0, 0);
                GameObject valid = switcher.Apply(0, 0, 0, 0, 0);
                Assert.Throws<System.InvalidOperationException>(() => switcher.Apply(0, 5, 0, 1, 1));
                Assert.AreSame(valid, switcher.CurrentInstance);
                Assert.AreEqual(0, switcher.CurrentRigIndex);
                Assert.AreEqual(0, switcher.CurrentFaceIndex);
                Assert.IsTrue(valid.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var prefab in prefabs) if (prefab != null) Object.DestroyImmediate(prefab);
            }
        }

        private static GameObject CreatePreviewPrefab(
            string name)
        {
            GameObject prefab =
                new GameObject(name);

            LegacyCharacterAppearanceVariants appearance =
                prefab.AddComponent<
                    LegacyCharacterAppearanceVariants>();

            var faces =
                new SkinnedMeshRenderer[5];

            var hairs =
                new SkinnedMeshRenderer[5];

            for (int i = 0;
                 i < 5;
                 i++)
            {
                GameObject face =
                    new GameObject(
                        "Face_" + i);

                face.transform.SetParent(
                    prefab.transform,
                    false);

                faces[i] =
                    face.AddComponent<
                        SkinnedMeshRenderer>();

                GameObject hair =
                    new GameObject(
                        "Hair_" + i);

                hair.transform.SetParent(
                    prefab.transform,
                    false);

                hairs[i] =
                    hair.AddComponent<
                        SkinnedMeshRenderer>();
            }

            appearance.Configure(
                faces,
                hairs,
                0,
                0);

            return prefab;
        }
    }
}
