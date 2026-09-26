using Dreynox.Mmorpg.World;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Editor.Importing;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class WorldGroundPlacementTests
    {
        [Test]
        public void ChoosesNearestHeightInsteadOfRoofAndNeverIntersectsWall()
        {
            var root=new GameObject("Placement fixture");root.transform.position=new Vector3(31000,14000,31000);
            try
            {
                var floor=new GameObject("walkable floor");floor.transform.SetParent(root.transform,false);
                floor.AddComponent<BoxCollider>().size=new Vector3(10,0.2f,10);
                var roof=new GameObject("higher roof");roof.transform.SetParent(root.transform,false);roof.transform.localPosition=Vector3.up*5;
                roof.AddComponent<BoxCollider>().size=new Vector3(10,0.2f,10);
                var player=new GameObject("player");player.transform.SetParent(root.transform,false);
                var body=player.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*0.9f;body.radius=0.3f;
                player.transform.localPosition=Vector3.right*30;
                Physics.SyncTransforms();
                Assert.IsTrue(WorldGroundPlacement.TryFind(body,root.transform.position,out Vector3 position,out string reason,0,8),reason);
                Assert.Less(position.y-root.transform.position.y,0.5f,"Do not jump to the highest roof.");
                var wall=new GameObject("wall");wall.transform.SetParent(root.transform,false);wall.transform.localPosition=Vector3.up;
                wall.AddComponent<BoxCollider>().size=new Vector3(0.8f,2,0.8f);Physics.SyncTransforms();
                Assert.IsFalse(WorldGroundPlacement.IsClear(body,position));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test]
        public void MissingSupportDoesNotMutateActor()
        {
            var player=new GameObject("Unsupported actor");player.transform.position=new Vector3(32000,18000,32000);
            var body=player.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*0.9f;
            var actor=player.AddComponent<ShaiyaClientActor>();
            try
            {
                Vector3 before=player.transform.position;
                Assert.IsFalse(WorldGroundPlacement.TryPlace(actor,before,out string reason,0,2));
                Assert.AreEqual(before,player.transform.position);
            }
            finally{Object.DestroyImmediate(player);}
        }
        [Test]
        public void BatchingCreatesNativeAssetsAndSubAssetsBeforePrefabReferences()
        {
            const string folder="Assets/DreynoxMMORPG/LocalLegacyGenerated/BatchFixture";
            System.IO.Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string path=folder+"/mesh.asset";
            var mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
            try
            {
                using(LegacyAssetWriteBatch.Begin())
                {
                    LegacyAssetWriteBatch.CreateAsset(mesh,path);
                    Assert.AreSame(mesh,LegacyAssetWriteBatch.LoadAssetAtPath<Mesh>(path));
                    LegacyAssetWriteBatch.Flush();
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(path));
                }
            }
            finally{AssetDatabase.DeleteAsset(folder);}
        }
    }
}
