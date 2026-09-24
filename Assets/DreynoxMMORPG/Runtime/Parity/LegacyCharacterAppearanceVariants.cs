using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class LegacyCharacterAppearanceVariants : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer[] faceVariants =
            new SkinnedMeshRenderer[5];

        [SerializeField] private SkinnedMeshRenderer[] hairVariants =
            new SkinnedMeshRenderer[5];

        [SerializeField] private int initialFaceIndex;
        [SerializeField] private int initialHairIndex;

        public int FaceIndex { get; private set; }
        public int HairIndex { get; private set; }

        public void Configure(
            SkinnedMeshRenderer[] faces,
            SkinnedMeshRenderer[] hairs,
            int faceIndex,
            int hairIndex)
        {
            ValidateVariants(
                faces,
                nameof(faces));

            ValidateVariants(
                hairs,
                nameof(hairs));

            faceVariants =
                (SkinnedMeshRenderer[])
                faces.Clone();

            hairVariants =
                (SkinnedMeshRenderer[])
                hairs.Clone();

            ValidateIndex(
                faceIndex,
                nameof(faceIndex));

            ValidateIndex(
                hairIndex,
                nameof(hairIndex));

            initialFaceIndex =
                faceIndex;

            initialHairIndex =
                hairIndex;

            Apply(
                faceIndex,
                hairIndex);
        }

        private void Awake()
        {
            Apply(
                initialFaceIndex,
                initialHairIndex);
        }

        public void Apply(
            int faceIndex,
            int hairIndex)
        {
            ValidateIndex(
                faceIndex,
                nameof(faceIndex));

            ValidateIndex(
                hairIndex,
                nameof(hairIndex));

            if (faceVariants == null ||
                faceVariants.Length != 5 ||
                hairVariants == null ||
                hairVariants.Length != 5)
            {
                throw new InvalidOperationException(
                    "Character appearance variants are not configured.");
            }

            for (int i = 0;
                 i < 5;
                 i++)
            {
                if (faceVariants[i] == null ||
                    hairVariants[i] == null)
                {
                    throw new InvalidOperationException(
                        "Character appearance variant " +
                        i +
                        " is missing.");
                }

                faceVariants[i].enabled =
                    i == faceIndex;

                hairVariants[i].enabled =
                    i == hairIndex;
            }

            FaceIndex =
                faceIndex;

            HairIndex =
                hairIndex;
        }

        private static void ValidateVariants(
            SkinnedMeshRenderer[] values,
            string parameterName)
        {
            if (values == null ||
                values.Length != 5)
            {
                throw new ArgumentException(
                    "Exactly five appearance variants are required.",
                    parameterName);
            }

            for (int i = 0;
                 i < values.Length;
                 i++)
            {
                if (values[i] == null)
                {
                    throw new ArgumentException(
                        "Appearance variant " +
                        i +
                        " is null.",
                        parameterName);
                }
            }
        }

        private static void ValidateIndex(
            int value,
            string parameterName)
        {
            if (value < 0 ||
                value > 4)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName);
            }
        }
    }
}
