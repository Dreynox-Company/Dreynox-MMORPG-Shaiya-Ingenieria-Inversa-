using System.IO;
using Dreynox.Mmorpg.Parity;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class SkinnedPoseProbeTests
    {
        private GameObject root;
        private Transform bone;
        private Mesh mesh;
        [SetUp] public void Setup()
        {
            root=new GameObject("Pose proof fixture");
            bone=new GameObject("Animated bone").transform;bone.SetParent(root.transform,false);
            mesh=new Mesh();mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};
            mesh.triangles=new[]{0,1,2};mesh.bindposes=new[]{Matrix4x4.identity};
            var weight=new BoneWeight{boneIndex0=0,weight0=1};mesh.boneWeights=new[]{weight,weight,weight};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;
            renderer.bones=new[]{bone};renderer.rootBone=bone;renderer.quality=SkinQuality.Bone1;renderer.updateWhenOffscreen=true;
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        [Test] public void RootMovementIsNotSkeletalAnimation()
        {
            var before=SkinnedPoseProbe.Capture(root);
            root.transform.position=new Vector3(10,20,30);
            var delta=SkinnedPoseProbe.Compare(before,SkinnedPoseProbe.Capture(root));
            Assert.AreEqual(0,delta.changedVertices);Assert.IsFalse(delta.Deformed);
        }
        [Test] public void AnimatedBoneChangesActualVerticesAndIdenticalPoseDoesNot()
        {
            var before=SkinnedPoseProbe.Capture(root);
            Assert.IsFalse(SkinnedPoseProbe.Compare(before,SkinnedPoseProbe.Capture(root)).Deformed);
            bone.localRotation=Quaternion.Euler(0,0,90);
            var delta=SkinnedPoseProbe.Compare(before,SkinnedPoseProbe.Capture(root));
            Assert.AreEqual(2,delta.changedVertices);Assert.Greater(delta.maximumDisplacement,1f);
            Assert.AreEqual(3,delta.vertices);Assert.AreEqual(1,delta.parts);
        }
        [Test] public void MissingOrReplacedSkinCannotPassAsAnimation()
        {
            var before=SkinnedPoseProbe.Capture(root);
            var renderer=root.GetComponent<SkinnedMeshRenderer>();
            var replacement=Object.Instantiate(mesh);
            try
            {
                renderer.sharedMesh=replacement;
                Assert.Throws<InvalidDataException>(()=>SkinnedPoseProbe.Compare(before,SkinnedPoseProbe.Capture(root)));
                renderer.enabled=false;
                Assert.Throws<InvalidDataException>(()=>SkinnedPoseProbe.Capture(root));
            }
            finally{Object.DestroyImmediate(replacement);}
        }
    }
}
