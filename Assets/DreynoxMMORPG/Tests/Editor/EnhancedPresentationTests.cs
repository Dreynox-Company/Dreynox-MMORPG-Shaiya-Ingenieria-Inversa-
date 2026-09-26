using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.LocalData;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class EnhancedPresentationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private static readonly Vector3 Far = new Vector3(30000, 10000, 30000);
        private GameObject Make(string name, Vector3 offset)
        {
            var go = new GameObject(name); go.transform.position = Far + offset; objects.Add(go); return go;
        }
        private ShaiyaThirdPersonCamera CameraRig()
        {
            var player = Make("actor", Vector3.zero);
            var body = player.AddComponent<SphereCollider>(); body.center = Vector3.up * 1.55f; body.radius = 0.8f;
            var go = Make("camera", Vector3.zero);
            var camera = go.AddComponent<Camera>(); camera.nearClipPlane = 0.08f;
            var rig = go.AddComponent<ShaiyaThirdPersonCamera>(); rig.SetTarget(player.transform); rig.ConfigureView(0, 0, 6.5f);
            return rig;
        }
        [TearDown] public void Clear()
        {
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }
        [TestCase(false)] [TestCase(true)]
        public void AnotherCharacterNeverPushesCameraIntoPlayersHead(bool npc)
        {
            var rig = CameraRig();
            var other = Make("nearby character", new Vector3(0, 1.55f, -0.7f));
            other.AddComponent<SphereCollider>().radius = 0.6f;
            if (npc) other.AddComponent<LegacyNpcRuntimeDescriptor>();
            else other.AddComponent<ShaiyaCombatTarget>();
            Physics.SyncTransforms(); rig.Step(1f / 60, Vector2.zero, 0);
            Assert.AreEqual(6.5f, rig.ResolvedDistance, 0.001f);
            Assert.IsFalse(rig.AvatarOccluded);
            Assert.IsFalse(rig.PivotObstructed);
            Assert.IsTrue(other.GetComponent<Collider>().enabled, "Gameplay collider must remain enabled.");
        }
        [Test]
        public void WallStillRetractsImmediatelyAndRecoveryIsSmooth()
        {
            var rig = CameraRig(); Physics.SyncTransforms(); rig.Step(1f / 60, Vector2.zero, 0);
            var wall = Make("solid wall", new Vector3(0, 1.55f, -3));
            wall.AddComponent<BoxCollider>().size = new Vector3(6, 6, 0.5f);
            Physics.SyncTransforms(); rig.Step(1f / 60, Vector2.zero, 0);
            Assert.That(rig.ResolvedDistance, Is.InRange(1f, 2.6f));
            float retracted = rig.ResolvedDistance;
            Object.DestroyImmediate(wall); Physics.SyncTransforms(); rig.Step(1f / 60, Vector2.zero, 0);
            Assert.Greater(rig.ResolvedDistance, retracted);
            Assert.Less(rig.ResolvedDistance, 6.5f);
        }
        [Test]
        public void NearPlaneSafetyIncludesWideAspectAndFov()
        {
            var camera = Make("wide camera", Vector3.zero).AddComponent<Camera>();
            camera.nearClipPlane = 0.3f; camera.fieldOfView = 60f; camera.aspect = 4f / 3;
            float normal = ShaiyaThirdPersonCamera.NearPlaneRadius(camera);
            camera.aspect = 32f / 9;
            Assert.Greater(ShaiyaThirdPersonCamera.NearPlaneRadius(camera), normal);
            camera.fieldOfView = 100;
            Assert.Greater(ShaiyaThirdPersonCamera.NearPlaneRadius(camera), camera.nearClipPlane);
        }
        [Test]
        public void SolidPivotOverlapIsReportedAndDoesNotRemoveAvatarParts()
        {
            var rig = CameraRig();
            var body = Make("mesh part", Vector3.zero); body.transform.SetParent(rig.Target, false);
            var renderer = body.AddComponent<MeshRenderer>();
            var wall = Make("overlapping wall", Vector3.up * 1.55f);
            wall.AddComponent<BoxCollider>().size = Vector3.one;
            Physics.SyncTransforms(); rig.Step(1f / 60, Vector2.zero, 0);
            Assert.IsTrue(rig.PivotObstructed);
            Assert.IsTrue(rig.AvatarOccluded);
            Assert.IsTrue(renderer.enabled);
            Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode,
                "Visibility changes must be camera-scoped, not permanent mesh edits.");
        }
        [Test]
        public void CameraRejectsNonFiniteInputWithoutCorruptingItsTransform()
        {
            var rig = CameraRig();
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Step(float.NaN, Vector2.zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.ConfigureView(0, 0, float.PositiveInfinity));
        }
        [Test]
        public void DdsPngRoundTripRetainsPixelsAlphaAndVerticalOrientation()
        {
            byte[] dds = DdsFixture();
            DecodedDds decoded = LegacyDdsDecoder.Decode(dds);
            byte[] png = LegacyColorTextureImporter.DecodeDdsToPng(dds);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.IsTrue(texture.LoadImage(png));
                Assert.AreEqual(4, texture.width); Assert.AreEqual(4, texture.height);
                Color32[] actual = texture.GetPixels32();
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.AreEqual(decoded.Pixels[i * 4], actual[i].r);
                    Assert.AreEqual(decoded.Pixels[i * 4 + 1], actual[i].g);
                    Assert.AreEqual(decoded.Pixels[i * 4 + 2], actual[i].b);
                    Assert.AreEqual(decoded.Pixels[i * 4 + 3], actual[i].a);
                }
                Assert.AreEqual(255, actual[12].r, "DDS top row must stay at the top after import.");
            }
            finally { Object.DestroyImmediate(texture); }
        }
        [Test]
        public void ColorImportUsesSrgbMipsAndFilteringWithoutModifyingSource()
        {
            string folder = "Assets/DreynoxMMORPG/LocalLegacyGenerated/EnhancedTests_" + Guid.NewGuid().ToString("N");
            string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dds");
            byte[] bytes = DdsFixture(); File.WriteAllBytes(source, bytes);
            try
            {
                var texture = LegacyColorTextureImporter.Import(source, folder + "/sample.dds");
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                Assert.IsNotNull(importer); Assert.IsTrue(importer.sRGBTexture); Assert.IsTrue(importer.mipmapEnabled);
                Assert.AreEqual(FilterMode.Trilinear, importer.filterMode); Assert.AreEqual(8, importer.anisoLevel);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
            }
            finally { AssetDatabase.DeleteAsset(folder); File.Delete(source); }
        }
        [Test]
        public void SurfacePolicyDoesNotTreatColorAlphaAsGlossAndSkyShaderCompiles()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                EnhancedWorldPresentation.ConfigureOpaque(material, true);
                Assert.IsFalse(material.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"));
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHATEST_ON"));
                Assert.AreEqual((int)RenderQueue.AlphaTest, material.renderQueue);
                Assert.Less(material.GetFloat("_Smoothness"), 0.3f);
                Assert.AreEqual(0, material.GetFloat("_Metallic"));
                EnhancedWorldPresentation.ConfigureOpaque(material, false);
                Assert.AreEqual((int)RenderQueue.Geometry, material.renderQueue);
            }
            finally { Object.DestroyImmediate(material); }
            var sky = Shader.Find(EnhancedWorldPresentation.SkyShader);
            Assert.IsNotNull(sky);
            Assert.IsFalse(ShaderUtil.ShaderHasError(sky));
        }
        private static byte[] DdsFixture()
        {
            byte[] data = new byte[136];
            void U32(int offset, uint value) { Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4); }
            U32(0, 0x20534444); U32(4, 124); U32(8, 0x81007); U32(12, 4); U32(16, 4);
            U32(20, 8); U32(76, 32); U32(80, 4); U32(84, 0x31545844); U32(108, 0x1000);
            data[128] = 0; data[129] = 0xf8; data[130] = 0xe0; data[131] = 7;
            U32(132, 0xffaa5500); // Different selector on each source row.
            return data;
        }
    }
}
