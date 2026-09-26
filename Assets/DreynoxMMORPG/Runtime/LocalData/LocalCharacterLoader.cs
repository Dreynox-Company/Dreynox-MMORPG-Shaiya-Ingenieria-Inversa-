// Raw DATA loading is an opt-in developer tool, never a shipping-client feature.
#if DREYNOX_DEV_DATA && !DEVELOPMENT_BUILD && !UNITY_EDITOR
#error DREYNOX_DEV_DATA requires a Development Player build.
#endif
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && DREYNOX_DEV_DATA)
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>One cancellable local import. IO, parsing and DDS decoding happen off-thread; Unity objects do not.</summary>
    public sealed class LocalCharacterLoader : MonoBehaviour
    {
        [SerializeField] private Material materialTemplate;
        private CancellationTokenSource cancellation;
        private int generation;
        private bool alive = true;
        public GameObject Current { get; private set; }
        public string Status { get; private set; } = "Seleccione la carpeta de Shaiya.";
        public bool IsLoading { get; private set; }
        public Material MaterialTemplate { get => materialTemplate; set => materialTemplate = value; }

        public async Task<bool> LoadAsync(string folder, int rigIndex, int face, int hair)
        {
            Cancel();
            int ticket = ++generation;
            cancellation = new CancellationTokenSource();
            var ownCancellation = cancellation;
            var token = ownCancellation.Token;
            IsLoading = true;
            Status = "Leyendo 3DC / ANI / DDS locales...";
            GameObject candidate = null;
            try
            {
                var rig = LegacyCharacterRigCore.ResolveNativeRigIndex(rigIndex);
                CharacterData data = await Task.Run(() =>
                {
                    var source = new LocalDataFolder(folder);
                    var paths = LegacyDefaultAppearanceCore.Resolve(rig.Family, rig.Job, rig.Sex, face, hair,
                        path => source.Read(path, token, LegacyModelListCore.MaximumBytes));
                    return Read(folder, paths, token);
                }, token);
                token.ThrowIfCancellationRequested();
                if (!alive || ticket != generation) return false;
                candidate = CreateCharacter(data);
                token.ThrowIfCancellationRequested();
                if (!alive || ticket != generation) return false;
                GameObject previous = Current;
                Current = candidate;
                Current.SetActive(true);
                candidate = null;
                if (previous != null) { previous.SetActive(false); Destroy(previous); }
                Status = data.Name + " · " + data.Animation.Bones.Count + " huesos · 6 piezas reales · DATA solo lectura";
                return true;
            }
            catch (OperationCanceledException) { if (alive && ticket == generation) Status = "Carga cancelada; se conserva el modelo anterior."; return false; }
            catch (Exception ex) { if (alive && ticket == generation) { Status = ex.Message; Debug.LogException(ex); } return false; }
            finally
            {
                if (candidate != null) Destroy(candidate);
                if (alive && ticket == generation) IsLoading = false;
                if (ReferenceEquals(cancellation, ownCancellation)) cancellation = null;
                ownCancellation.Dispose();
            }
        }
        public void Cancel() { if (cancellation != null) cancellation.Cancel(); }
        private void OnDestroy() { alive = false; generation++; Cancel(); }

        private static CharacterData Read(string folder, LegacyCharacterPreviewAssetPaths paths, CancellationToken token)
        {
            var source = new LocalDataFolder(folder);
            var data = new CharacterData { Name = paths.Rig.Prefix };
            data.Animation = LegacyAniParser.Parse(source.Read(paths.SelectAnimation, token));
            string[] meshes = { paths.UpperMesh, paths.LowerMesh, paths.HandMesh, paths.FootMesh, paths.FaceMesh, paths.HairMesh };
            string[] textures = { paths.UpperTexture, paths.LowerTexture, paths.HandTexture, paths.FootTexture, paths.FaceTexture, paths.HairTexture };
            long decodedBudget = 0;
            for (int i = 0; i < 6; i++)
            {
                token.ThrowIfCancellationRequested();
                data.Meshes[i] = Legacy3dcParser.Parse(source.Read(meshes[i], token));
                LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(data.Meshes[i], data.Animation.Bones.Count);
                data.Textures[i] = LegacyDdsDecoder.Decode(source.Read(textures[i], token), token);
                decodedBudget += data.Textures[i].Pixels.Length;
                if (decodedBudget > 128L * 1024 * 1024) throw new InvalidDataException("Character texture budget exceeded.");
            }
            return data;
        }
        private GameObject CreateCharacter(CharacterData data)
        {
            if (materialTemplate == null) throw new InvalidOperationException("El cargador requiere un material URP incluido en el build.");
            var root = new GameObject("Local_" + data.Name);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var resources = root.AddComponent<LocalCharacterResources>();
            try
            {
                Transform[] bones = LegacyRuntimeSkinnedBuilder.BuildSkeleton(root.transform, data.Meshes[0], data.Animation);
                string[] labels = { "Upper", "Lower", "Hands", "Feet", "Face", "Hair" };
                for (int i = 0; i < data.Meshes.Length; i++)
                {
                    Mesh mesh = LegacyRuntimeSkinnedBuilder.BuildMesh(data.Meshes[i], bones, root.transform, labels[i]);
                    resources.Own(mesh);
                    DecodedDds image = data.Textures[i];
                    var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, true, false) { name = labels[i], wrapMode = TextureWrapMode.Repeat };
                    resources.Own(texture);
                    texture.LoadRawTextureData(image.Pixels); texture.Apply(true, true);
                    var material = new Material(materialTemplate) { name = data.Name + "_" + labels[i] };
                    resources.Own(material);
                    material.mainTexture = texture;
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                    if (i == 5)
                    {
                        material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", 0.5f);
                        material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450;
                        material.SetOverrideTag("RenderType", "TransparentCutout");
                    }
                    var part = new GameObject(labels[i]); part.transform.SetParent(root.transform, false);
                    var renderer = part.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.bones = bones; renderer.rootBone = bones[0];
                    renderer.localBounds = mesh.bounds;
                    // Preview correctness first. World distance culling belongs to the existing actor pipeline.
                    renderer.updateWhenOffscreen = true;
                }
                root.AddComponent<LocalAniPlayer>().Configure(data.Animation, bones);
                return root;
            }
            catch { Destroy(root); throw; }
        }
        private sealed class CharacterData
        {
            public string Name;
            public LegacyAniFile Animation;
            public readonly Legacy3dcFile[] Meshes = new Legacy3dcFile[6];
            public readonly DecodedDds[] Textures = new DecodedDds[6];
        }
    }

}
#endif
