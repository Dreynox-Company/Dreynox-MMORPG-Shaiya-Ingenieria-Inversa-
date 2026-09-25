using System;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class LegacyCharacterPreviewSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] rigPrefabs =
            new GameObject[16];

        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles =
            new Vector3(0f, 180f, 0f);

        [SerializeField] private int initialFamily;
        [SerializeField] private int initialJob;
        [SerializeField] private int initialSex;
        [SerializeField] private int initialFaceIndex;
        [SerializeField] private int initialHairIndex;

        private GameObject _instance;
        private int _currentRigIndex = -1;

        public GameObject CurrentInstance => _instance;
        public int CurrentRigIndex => _currentRigIndex;
        public int CurrentFaceIndex { get; private set; }
        public int CurrentHairIndex { get; private set; }
        public SemanticAnimationPlayer CurrentAnimation { get; private set; }

        public void Configure(
            GameObject[] prefabs,
            Vector3 previewLocalPosition,
            Vector3 previewLocalEulerAngles,
            int family,
            int job,
            int sex,
            int faceIndex = 0,
            int hairIndex = 0)
        {
            if (prefabs == null ||
                prefabs.Length != 16)
            {
                throw new ArgumentException(
                    "Character preview requires exactly 16 native rig prefabs.",
                    nameof(prefabs));
            }

            rigPrefabs =
                new GameObject[16];

            for (int i = 0;
                 i < prefabs.Length;
                 i++)
            {
                if (prefabs[i] == null)
                {
                    throw new ArgumentException(
                        "Character preview rig prefab " +
                        i +
                        " is null.",
                        nameof(prefabs));
                }

                rigPrefabs[i] =
                    prefabs[i];
            }

            localPosition =
                previewLocalPosition;

            localEulerAngles =
                previewLocalEulerAngles;

            LegacyCharacterRigSelection selection =
                LegacyCharacterRigCore.Resolve(
                    family,
                    job,
                    sex);

            ValidateAppearanceIndex(
                faceIndex,
                nameof(faceIndex));

            ValidateAppearanceIndex(
                hairIndex,
                nameof(hairIndex));

            initialFamily = family;
            initialJob = job;
            initialSex = sex;
            initialFaceIndex = faceIndex;
            initialHairIndex = hairIndex;

            if (selection.NativeRigIndex < 0 ||
                selection.NativeRigIndex >=
                    rigPrefabs.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(family));
            }
        }

        private void Start()
        {
            if (_instance != null) return; // UI may have selected a rig before Start.
            Apply(
                initialFamily,
                initialJob,
                initialSex,
                initialFaceIndex,
                initialHairIndex);
        }

        public GameObject Apply(
            int family,
            int job,
            int sex)
        {
            return Apply(
                family,
                job,
                sex,
                0,
                0);
        }

        public GameObject Apply(
            int family,
            int job,
            int sex,
            int faceIndex,
            int hairIndex)
        {
            ValidateAppearanceIndex(
                faceIndex,
                nameof(faceIndex));

            ValidateAppearanceIndex(
                hairIndex,
                nameof(hairIndex));

            LegacyCharacterRigSelection selection =
                LegacyCharacterRigCore.Resolve(
                    family,
                    job,
                    sex);

            if (_instance != null &&
                _currentRigIndex ==
                    selection.NativeRigIndex)
            {
                if (CurrentFaceIndex == faceIndex && CurrentHairIndex == hairIndex)
                    return _instance;
                ApplyAppearance(
                    _instance,
                    faceIndex,
                    hairIndex);

                CurrentFaceIndex =
                    faceIndex;

                CurrentHairIndex =
                    hairIndex;

                // Appearance-only changes keep the current animation phase.
                return _instance;
            }

            if (selection.NativeRigIndex < 0 ||
                selection.NativeRigIndex >=
                    rigPrefabs.Length ||
                rigPrefabs[
                    selection.NativeRigIndex] ==
                    null)
            {
                throw new InvalidOperationException(
                    "Preview prefab is missing for native rig " +
                    selection.NativeRigIndex +
                    " (" +
                    selection.Prefix +
                    ").");
            }

            GameObject candidate = null;
            GameObject staging = new GameObject("PreviewStaging");
            staging.SetActive(false);
            try
            {
                candidate = Instantiate(rigPrefabs[selection.NativeRigIndex], staging.transform, false);
                candidate.name = "Preview_" + selection.Prefix;
                candidate.transform.localPosition = localPosition;
                candidate.transform.localRotation = Quaternion.Euler(localEulerAngles);
                candidate.transform.localScale = Vector3.one;
                ApplyAppearance(candidate, faceIndex, hairIndex);
                candidate.transform.SetParent(transform, false);
                candidate.SetActive(true);
                var nextAnimation = candidate.GetComponent<SemanticAnimationPlayer>();
                if (nextAnimation != null) nextAnimation.PlaySemantic("select");

                GameObject previous = _instance;
                _instance = candidate;
                _currentRigIndex = selection.NativeRigIndex;
                CurrentAnimation = nextAnimation;
                CurrentFaceIndex = faceIndex;
                CurrentHairIndex = hairIndex;
                candidate = null;
                Release(previous);
                return _instance;
            }
            finally
            {
                Release(candidate);
                Release(staging);
            }
        }

        private static void Release(GameObject value)
        {
            if (value == null) return;
            value.SetActive(false);
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private void DestroyPreviewInstance()
        {
            if (_instance == null)
                return;

            if (Application.isPlaying)
                Destroy(_instance);
            else
                DestroyImmediate(_instance);

            _instance = null;
            _currentRigIndex = -1;
            CurrentAnimation = null;
        }

        private static void ApplyAppearance(
            GameObject instance,
            int faceIndex,
            int hairIndex)
        {
            LegacyCharacterAppearanceVariants appearance =
                instance.GetComponent<
                    LegacyCharacterAppearanceVariants>();

            if (appearance == null)
            {
                throw new InvalidOperationException(
                    "Character preview prefab has no appearance variant controller.");
            }

            appearance.Apply(
                faceIndex,
                hairIndex);
        }

        private static void ValidateAppearanceIndex(
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

        private void PlaySelect()
        {
            if (CurrentAnimation != null)
                CurrentAnimation.PlaySemantic("select");
        }
    }
}
