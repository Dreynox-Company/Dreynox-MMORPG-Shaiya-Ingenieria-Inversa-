using System;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class WorldTerrainCollisionTests
    {
        private GameObject ground, player;
        private TerrainData data;
        private Terrain terrain;
        private static readonly Vector3 Origin=new Vector3(37000,12000,37000);
        [SetUp] public void CreateTerrain()
        {
            data=new TerrainData {heightmapResolution=33,size=new Vector3(32,16,32)};
            var heights=new float[33,33];
            for(int z=0;z<33;z++)for(int x=0;x<33;x++)heights[z,x]=0.25f;
            data.SetHeights(0,0,heights);
            // This is the exact factory used by the original-map importer.
            // Missing TerrainPhysics must fail here, not days later in combat.
            ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=Origin;
            terrain=ground.GetComponent<Terrain>();
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            if(player!=null)Object.DestroyImmediate(player);
            if(ground!=null)Object.DestroyImmediate(ground);
            if(data!=null)Object.DestroyImmediate(data);
            Physics.SyncTransforms();
        }
        [Test] public void RealTerrainFactoryIncludesEnabledMatchingPhysics()
        {
            var collider=WorldTerrainCollision.Require(terrain);
            Assert.AreSame(data,collider.terrainData);
            Assert.IsTrue(WorldTerrainCollision.TryProbe(terrain,Origin+new Vector3(16,4,16),out var hit));
            Assert.AreSame(collider,hit.collider);
            Assert.AreEqual(Origin.y+4,hit.point.y,0.02f);
        }
        [Test] public void FieldWithoutBuildingsSupportsCharacterControllerPlacement()
        {
            player=new GameObject("field character");player.transform.position=Origin+new Vector3(-5,10,-5);
            var body=player.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*0.9f;body.radius=0.35f;
            Physics.SyncTransforms();
            Assert.IsTrue(WorldGroundPlacement.TryFind(body,Origin+new Vector3(16,4,16),out var pose,out var reason,0,8),reason);
            Assert.That(pose.y-Origin.y,Is.InRange(4.04f,4.2f));
            Assert.IsTrue(WorldGroundPlacement.IsClear(body,pose));
        }
        [Test] public void RenderOnlyTerrainIsRejectedInsteadOfBeingCalledWalkable()
        {
            Object.DestroyImmediate(ground.GetComponent<TerrainCollider>());
            Assert.IsNotNull(terrain.terrainData);
            Assert.Throws<InvalidOperationException>(()=>WorldTerrainCollision.Require(terrain));
        }
        [Test] public void DisabledPhysicalSupportIsRejected()
        {
            ground.GetComponent<TerrainCollider>().enabled=false;
            Assert.Throws<InvalidOperationException>(()=>WorldTerrainCollision.Require(terrain));
        }
        [Test] public void WrongHeightfieldCannotPassTheSupportGate()
        {
            var other=new TerrainData {heightmapResolution=33,size=new Vector3(32,16,32)};
            try
            {
                ground.GetComponent<TerrainCollider>().terrainData=other;
                Assert.Throws<InvalidOperationException>(()=>WorldTerrainCollision.Require(terrain));
            }
            finally {ground.GetComponent<TerrainCollider>().terrainData=data;Object.DestroyImmediate(other);}
        }
        [Test] public void OutsideMapAndNonFiniteProbesNeverInventGround()
        {
            Assert.IsFalse(WorldTerrainCollision.TryProbe(terrain,Origin+new Vector3(-1,4,16),out _));
            Assert.IsFalse(WorldTerrainCollision.TryProbe(terrain,new Vector3(float.NaN,0,0),out _));
        }
        [Test] public void AuthoredHoleRemainsARealHole()
        {
            int size=data.holesResolution;var holes=new bool[size,size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)holes[z,x]=!(x>=14&&x<=18&&z>=14&&z<=18);
            data.SetHoles(0,0,holes);terrain.Flush();Physics.SyncTransforms();
            Assert.IsFalse(WorldTerrainCollision.TryProbe(terrain,Origin+new Vector3(16.5f,4,16.5f),out _));
            Assert.IsTrue(WorldTerrainCollision.TryProbe(terrain,Origin+new Vector3(8.5f,4,8.5f),out _));
        }
    }
}
