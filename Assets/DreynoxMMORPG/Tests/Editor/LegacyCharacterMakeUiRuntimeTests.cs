using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyCharacterMakeUiRuntimeTests
    {
        [Test]
        public void AppearanceTabStartsHiddenAndCanToggle()
        {
            GameObject root =
                new GameObject("TabFixture");

            GameObject panel =
                new GameObject("AppearancePanel");

            panel.transform.SetParent(
                root.transform,
                false);

            try
            {
                LegacyCharacterMakeTabController tabs =
                    root.AddComponent<
                        LegacyCharacterMakeTabController>();

                tabs.Bind(panel);

                Assert.IsFalse(
                    tabs.AppearanceVisible);

                tabs.ShowAppearance();

                Assert.IsTrue(
                    tabs.AppearanceVisible);

                tabs.ShowBasic();

                Assert.IsFalse(
                    tabs.AppearanceVisible);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DirectFaceAndHairSelectionUpdatesControllerState()
        {
            GameObject root =
                new GameObject("MakeFixture");

            try
            {
                LegacyCharacterMakeScreenController controller =
                    root.AddComponent<
                        LegacyCharacterMakeScreenController>();

                controller.Configure(
                    1,
                    0,
                    0,
                    0,
                    0,
                    0);

                int changes = 0;
                controller.AppearanceChanged +=
                    () => changes++;

                controller.SelectFace(4);
                controller.SelectHair(3);

                Assert.AreEqual(
                    4,
                    controller.Face);

                Assert.AreEqual(
                    3,
                    controller.Hair);

                Assert.AreEqual(
                    2,
                    changes);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
