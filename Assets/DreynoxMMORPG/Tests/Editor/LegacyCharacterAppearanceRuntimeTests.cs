using Dreynox.Mmorpg.Parity;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyCharacterAppearanceRuntimeTests
    {
        [Test]
        public void AppearanceVariantsEnableExactlyOneFaceAndHair()
        {
            GameObject root =
                new GameObject(
                    "AppearanceFixture");

            try
            {
                LegacyCharacterAppearanceVariants controller =
                    root.AddComponent<
                        LegacyCharacterAppearanceVariants>();

                var faces =
                    new SkinnedMeshRenderer[5];

                var hairs =
                    new SkinnedMeshRenderer[5];

                for (int i = 0;
                     i < 5;
                     i++)
                {
                    faces[i] =
                        new GameObject(
                            "Face_" + i)
                            .AddComponent<
                                SkinnedMeshRenderer>();

                    faces[i]
                        .transform
                        .SetParent(
                            root.transform,
                            false);

                    hairs[i] =
                        new GameObject(
                            "Hair_" + i)
                            .AddComponent<
                                SkinnedMeshRenderer>();

                    hairs[i]
                        .transform
                        .SetParent(
                            root.transform,
                            false);
                }

                controller.Configure(
                    faces,
                    hairs,
                    2,
                    4);

                Assert.AreEqual(
                    2,
                    controller.FaceIndex);

                Assert.AreEqual(
                    4,
                    controller.HairIndex);

                for (int i = 0;
                     i < 5;
                     i++)
                {
                    Assert.AreEqual(
                        i == 2,
                        faces[i].enabled);

                    Assert.AreEqual(
                        i == 4,
                        hairs[i].enabled);
                }

                controller.Apply(
                    1,
                    3);

                Assert.AreEqual(
                    1,
                    controller.FaceIndex);

                Assert.AreEqual(
                    3,
                    controller.HairIndex);

                for (int i = 0;
                     i < 5;
                     i++)
                {
                    Assert.AreEqual(
                        i == 1,
                        faces[i].enabled);

                    Assert.AreEqual(
                        i == 3,
                        hairs[i].enabled);
                }
            }
            finally
            {
                Object.DestroyImmediate(
                    root);
            }
        }
    }
}
