using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public readonly struct LegacyCharacterPreviewAssetPaths
    {
        public readonly LegacyCharacterRigSelection Rig;
        public readonly int SetId;
        public readonly int FaceIndex;
        public readonly int HairIndex;
        public readonly string Root;
        public readonly string UpperMesh;
        public readonly string LowerMesh;
        public readonly string HandMesh;
        public readonly string FootMesh;
        public readonly string FaceMesh;
        public readonly string HairMesh;
        public readonly string UpperTexture;
        public readonly string LowerTexture;
        public readonly string HandTexture;
        public readonly string FootTexture;
        public readonly string FaceTexture;
        public readonly string HairTexture;
        public readonly string SelectAnimation;

        public LegacyCharacterPreviewAssetPaths(
            LegacyCharacterRigSelection rig,
            int setId,
            int faceIndex,
            int hairIndex,
            string root,
            string upperMesh,
            string lowerMesh,
            string handMesh,
            string footMesh,
            string faceMesh,
            string hairMesh,
            string upperTexture,
            string lowerTexture,
            string handTexture,
            string footTexture,
            string faceTexture,
            string hairTexture,
            string selectAnimation)
        {
            Rig = rig;
            SetId = setId;
            FaceIndex = faceIndex;
            HairIndex = hairIndex;
            Root = root ?? string.Empty;
            UpperMesh = upperMesh ?? string.Empty;
            LowerMesh = lowerMesh ?? string.Empty;
            HandMesh = handMesh ?? string.Empty;
            FootMesh = footMesh ?? string.Empty;
            FaceMesh = faceMesh ?? string.Empty;
            HairMesh = hairMesh ?? string.Empty;
            UpperTexture = upperTexture ?? string.Empty;
            LowerTexture = lowerTexture ?? string.Empty;
            HandTexture = handTexture ?? string.Empty;
            FootTexture = footTexture ?? string.Empty;
            FaceTexture = faceTexture ?? string.Empty;
            HairTexture = hairTexture ?? string.Empty;
            SelectAnimation = selectAnimation ?? string.Empty;
        }
    }

    public static class LegacyCharacterAssetCore
    {
        public static LegacyCharacterPreviewAssetPaths ResolvePreview(
            int family,
            int job,
            int sex,
            int faceIndex = 0,
            int hairIndex = 0,
            int setId = 3)
        {
            if (faceIndex < 0 || faceIndex > 4)
                throw new ArgumentOutOfRangeException(nameof(faceIndex));

            if (hairIndex < 0 || hairIndex > 4)
                throw new ArgumentOutOfRangeException(nameof(hairIndex));

            if (setId < 0 || setId > 999)
                throw new ArgumentOutOfRangeException(nameof(setId));

            LegacyCharacterRigSelection rig =
                LegacyCharacterRigCore.Resolve(
                    family,
                    job,
                    sex);

            string root =
                "DATA_Español/character/" +
                rig.FamilyFolder;

            string set =
                setId.ToString("D3");

            string face =
                (faceIndex + 1).ToString("D3");

            string hair =
                (hairIndex + 1).ToString("D3");

            string texturePrefix =
                ResolveTexturePrefix(
                    rig.Prefix);

            return new LegacyCharacterPreviewAssetPaths(
                rig,
                setId,
                faceIndex,
                hairIndex,
                root,
                root + "/3dc/co_" + rig.Prefix + "_upper" + set + ".3dc",
                root + "/3dc/co_" + rig.Prefix + "_lower" + set + ".3dc",
                root + "/3dc/co_" + rig.Prefix + "_hand" + set + ".3dc",
                root + "/3dc/co_" + rig.Prefix + "_foot" + set + ".3dc",
                root + "/3dc/" + rig.Prefix + "_face" + face + ".3dc",
                root + "/3dc/" + rig.Prefix + "_hair" + hair + ".3dc",
                root + "/dds/co_" + rig.Prefix + "_upper" + set + ".dds",
                root + "/dds/co_" + rig.Prefix + "_lower" + set + ".dds",
                root + "/dds/co_" + rig.Prefix + "_hand" + set + ".dds",
                root + "/dds/co_" + rig.Prefix + "_foot" + set + ".dds",
                root + "/dds/" + texturePrefix + "_face" + face + ".dds",
                root + "/dds/" + texturePrefix + "_hair" + hair + ".dds",
                root + "/ani6/" + rig.Prefix + "_019_select.ani");
        }

        public static string ResolveTexturePrefix(
            string rigPrefix)
        {
            if (string.IsNullOrWhiteSpace(rigPrefix) ||
                rigPrefix.Length < 3)
            {
                throw new ArgumentException(
                    "Legacy rig prefix must contain at least three characters.",
                    nameof(rigPrefix));
            }

            return rigPrefix.Substring(0, 3);
        }
    }
}
