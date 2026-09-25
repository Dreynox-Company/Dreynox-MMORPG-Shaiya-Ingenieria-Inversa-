using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.ParityCore;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>
    /// Local Map1 fighter loadout, matching the supplied offline reference's type 1/id 1.
    /// Mesh, texture, image index and hand transform come from original DATA.
    /// This grants no network inventory and changes no damage/timing formulas.
    /// </summary>
    public static class CanonicalStarterSwordImporter
    {
        public const string Root = "Assets/DreynoxMMORPG/LocalLegacyGenerated/Equipment/Map1StarterSword";
        public const string SocketName = "Original_IT2_HUMF_MainHand";
        public static AttachmentDefinition Configure(CanonicalClientCorpus corpus, ShaiyaClientActor actor)
        {
            if (corpus == null || actor == null) throw new ArgumentNullException("Original corpus and actor are required.");
            if (actor.Family != 0 || actor.Sex != 0 || actor.Job != 0)
                throw new InvalidOperationException("This explicit starter loadout is qualified only for the human male fighter.");
            int image = LegacyItemDefinitionParser.ResolveVisualIndex(corpus.Resolve("DATA_Español/binarysdata/dbitemdata.sdata"), 1, 1);
            var table = LegacyItemVisualParser.ParseItm(corpus.Resolve("DATA_Español/item/01.itm"));
            if (image < 0 || image >= table.Records.Count) throw new InvalidDataException("Starter item visual index is outside IT2.");
            var row = table.Records[image];
            // Keep the importer narrow instead of inventing effects for other item records.
            if (table.ArchetypeCount != 16 || row.RecordFormat != 1 || row.BlendMode != -1 ||
                row.Rgba != uint.MaxValue || row.Rotation != 0 || row.Scale != 1)
                throw new NotSupportedException("Starter item material policy changed; qualify its authored properties explicitly.");
            LegacyItemBoneTransform placement = row.Primary[0];
            string boneName = "Bone_" + placement.Bone.ToString("D3");
            var matches = actor.GetComponentsInChildren<Transform>(true).Where(t => t.name == boneName).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Original starter item bone is absent or ambiguous: " + boneName);
            var existing = matches[0].GetComponents<AttachmentSocket>();
            var socket = existing.FirstOrDefault(s => s.SocketName == SocketName);
            if (socket == null) socket = matches[0].gameObject.AddComponent<AttachmentSocket>();
            socket.Configure(SocketName);
            var equipment = actor.GetComponent<EquipmentAttachmentController>();
            if (equipment == null) throw new InvalidOperationException("Actor equipment controller is absent.");
            equipment.RebuildSocketCache();
            EnsureFolder(Root);
            var original = LegacyItemVisualParser.Parse3do(corpus.Resolve("DATA_Español/item/3do/" + table.MeshNames[row.MeshIndex]));
            var mesh = new Mesh { name = "Original_01001_StarterSword" };
            mesh.vertices = original.Vertices.Select(v => LegacyCoordinateBridge.Position(v.Position)).ToArray();
            mesh.normals = original.Vertices.Select(v => LegacyCoordinateBridge.Direction(v.Normal).normalized).ToArray();
            mesh.uv = original.Vertices.Select(v => v.UV).ToArray();
            mesh.triangles = original.Faces.SelectMany(f => new[] { (int)f.A, (int)f.C, (int)f.B }).ToArray();
            mesh.RecalculateBounds();
            Write(mesh, Root + "/Sword.asset");
            string textureName = table.TextureNames[row.TextureIndex];
            Texture2D texture = LegacyColorTextureImporter.Import(corpus.Resolve("DATA_Español/item/dds/" + textureName),
                Root + "/" + textureName, TextureWrapMode.Repeat);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || texture == null) throw new InvalidOperationException("Qualified item shader or original texture is absent.");
            var material = new Material(shader) { name = "Original_01001_Albedo" };
            material.SetTexture("_BaseMap", texture); material.SetFloat("_Smoothness", 0.15f);
            Write(material, Root + "/Sword.mat");
            var root = new GameObject("OriginalStarterSword_1_1");
            try
            {
                // The attachment system resets its instance root. A visual child
                // retains the authored quaternion without an Euler round trip.
                var visual = new GameObject("IT2_AuthoredVisual"); visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = LegacyCoordinateBridge.Position(placement.Position);
                visual.transform.localRotation = LegacyCoordinateBridge.Rotation(placement.Rotation).normalized;
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;
                var prefab = LegacyAssetWriteBatch.SaveAsPrefabAsset(root, Root + "/Sword.prefab");
                if (prefab == null) throw new InvalidDataException("Starter item prefab did not persist.");
                var definition = ScriptableObject.CreateInstance<AttachmentDefinition>();
                definition.legacyResourceId = "item/01.itm:image=" + image + ";itemtype=1;itemtypeid=1;archetype=HUMF";
                definition.slot = EquipmentSlot.MainHand; definition.socketName = SocketName;
                definition.prefab = prefab; definition.localScale = Vector3.one;
                definition.weaponFamily = ClientWeaponFamily.OneHand; definition.occupiesBothHands = false;
                Write(definition, Root + "/StarterSword.asset");
                LegacyAssetWriteBatch.Flush(); LegacyAssetWriteBatch.SaveAssets();
                var loadout = actor.GetComponent<LocalStarterEquipment>();
                if (loadout == null) loadout = actor.gameObject.AddComponent<LocalStarterEquipment>();
                loadout.Configure(actor, definition);
                return definition;
            }
            catch { LegacyAssetWriteBatch.Abort(); throw; }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Write(Object value, string path)
        { LegacyAssetWriteBatch.DeleteAsset(path); LegacyAssetWriteBatch.CreateAsset(value, path); }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
