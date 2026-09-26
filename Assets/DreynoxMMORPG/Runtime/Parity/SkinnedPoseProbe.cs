using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Parity
{
    /// <summary>Explicit qualification snapshots; never runs in normal per-frame gameplay.</summary>
    public static class SkinnedPoseProbe
    {
        public sealed class Snapshot
        {
            internal readonly Dictionary<int, Part> Parts=new Dictionary<int, Part>();
            public int VertexCount { get; internal set; }
        }
        internal sealed class Part
        {
            public int MeshId;
            public Vector3[] Vertices;
        }
        [Serializable] public sealed class Difference
        {
            public int parts,vertices,changedVertices;
            public float maximumDisplacement,rootMeanSquareDisplacement;
            public bool Deformed => changedVertices>0;
        }
        public static Snapshot Capture(GameObject actor)
        {
            if(actor==null)throw new ArgumentNullException(nameof(actor));
            var result=new Snapshot();
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled||renderer.sharedMesh==null)continue;
                int count=renderer.sharedMesh.vertexCount;
                if(count<=0||count>500000-result.VertexCount)
                    throw new InvalidDataException("Pose capture exceeds its vertex budget or contains empty geometry.");
                var baked=new Mesh();
                try
                {
                    // Renderer-local output: walking/teleporting the actor cannot
                    // masquerade as skeletal vertex deformation.
                    renderer.BakeMesh(baked,false);
                    Vector3[] vertices=baked.vertices;
                    if(vertices.Length!=count)throw new InvalidDataException("Baked skin topology changed.");
                    foreach(var v in vertices)RequireFinite(v);
                    result.Parts.Add(renderer.GetInstanceID(),new Part{MeshId=renderer.sharedMesh.GetInstanceID(),Vertices=vertices});
                    result.VertexCount+=count;
                }
                finally{if(Application.isPlaying)Object.Destroy(baked);else Object.DestroyImmediate(baked);}
            }
            if(result.Parts.Count==0)throw new InvalidDataException("No active skinned meshes for actual pose qualification.");
            return result;
        }
        public static Difference Compare(Snapshot before,Snapshot after,float threshold=0.0001f)
        {
            if(before==null||after==null)throw new ArgumentNullException("Snapshots are required.");
            if(float.IsNaN(threshold)||float.IsInfinity(threshold)||threshold<0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if(before.Parts.Count!=after.Parts.Count||before.VertexCount!=after.VertexCount)
                throw new InvalidDataException("A different actor/appearance cannot be compared as animation.");
            var result=new Difference{parts=before.Parts.Count,vertices=before.VertexCount};
            double squares=0,maximum=0,epsilon=(double)threshold*threshold;
            foreach(var pair in before.Parts)
            {
                if(!after.Parts.TryGetValue(pair.Key,out var part)||pair.Value.MeshId!=part.MeshId||
                    part.Vertices.Length!=pair.Value.Vertices.Length)
                    throw new InvalidDataException("Renderer or mesh identity changed during pose qualification.");
                for(int i=0;i<part.Vertices.Length;i++)
                {
                    Vector3 a=pair.Value.Vertices[i],b=part.Vertices[i];
                    RequireFinite(a);RequireFinite(b);
                    double x=(double)b.x-a.x,y=(double)b.y-a.y,z=(double)b.z-a.z;
                    double squared=x*x+y*y+z*z;
                    squares+=squared;maximum=Math.Max(maximum,squared);
                    if(squared>epsilon)result.changedVertices++;
                }
            }
            result.maximumDisplacement=(float)Math.Sqrt(maximum);
            result.rootMeanSquareDisplacement=(float)Math.Sqrt(squares/Math.Max(1,result.vertices));
            return result;
        }
        private static void RequireFinite(Vector3 v)
        {
            if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))
                throw new InvalidDataException("Non-finite baked skin vertex.");
        }
    }
}
