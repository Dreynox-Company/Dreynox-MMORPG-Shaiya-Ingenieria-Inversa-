using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Equipment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class CanonicalStarterSwordTests
    {
        [Test]
        public void OriginalSwordPersistsAndFollowsItsAuthoredHandWithoutChangingBodyOrOriginalData()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            Assert.IsFalse(Directory.Exists(CanonicalStarterSwordImporter.Root),"Do not replace pre-existing user-generated equipment from a test.");
            var root=new GameObject("StarterEquipmentFixture");
            try
            {
                var equipment=root.AddComponent<EquipmentAttachmentController>();
                var actor=root.AddComponent<ShaiyaClientActor>();actor.ConfigureLegacyIdentity(0,0,0);
                var hand=new GameObject("Bone_021");hand.transform.SetParent(root.transform,false);
                var definition=CanonicalStarterSwordImporter.Configure(corpus,actor);
                Assert.IsTrue(AssetDatabase.Contains(definition));Assert.IsNotNull(definition.prefab);
                Assert.IsTrue(equipment.Equip(definition));
                Assert.IsTrue(equipment.TryGetInstance(EquipmentSlot.MainHand,out var instance));
                Assert.AreSame(hand.transform,instance.transform.parent);
                var filter=instance.GetComponentInChildren<MeshFilter>();Assert.IsNotNull(filter);
                Assert.AreEqual(169,filter.sharedMesh.vertexCount);Assert.AreEqual(152*3,filter.sharedMesh.triangles.Length);
                Assert.IsTrue(AssetDatabase.Contains(filter.sharedMesh));
                Assert.AreEqual(new Vector3(.099f,.004f,-.006f),filter.transform.localPosition);
                Quaternion expected=new Quaternion(.024678f,.706676f,-.024678f,.706676f).normalized;
                Assert.Greater(Mathf.Abs(Quaternion.Dot(expected,filter.transform.localRotation)),.999999f);
                hand.transform.localPosition=new Vector3(1,2,3);hand.transform.localRotation=Quaternion.Euler(12,42,17);
                Assert.Less(Vector3.Distance(hand.transform.TransformPoint(new Vector3(.099f,.004f,-.006f)),filter.transform.position),.00001f);
                var material=instance.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.IsTrue(AssetDatabase.Contains(material));Assert.IsTrue(AssetDatabase.GetAssetPath(material.mainTexture).EndsWith(".png"));
                Assert.IsNotNull(root.GetComponent<LocalStarterEquipment>());
            }
            finally {Object.DestroyImmediate(root);AssetDatabase.DeleteAsset(CanonicalStarterSwordImporter.Root);}
        }
    }
}
