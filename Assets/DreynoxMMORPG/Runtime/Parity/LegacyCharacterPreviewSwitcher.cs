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

        private GameObject _instance;
        private int _currentRigIndex = -1;

        public GameObject CurrentInstance => _instance;
        public int CurrentRigIndex => _currentRigIndex;
        public SemanticAnimationPlayer CurrentAnimation { get; private set; }

        public void Configure(
            GameObject[] prefabs,
            Vector3 previewLocalPosition,
            Vector3 previewLocalEulerAngles,
            int family,
            int job,
            int sex)
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

            initialFamily = family;
            initialJob = job;
            initialSex = sex;

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
            Apply(
                initialFamily,
                initialJob,
                initialSex);
        }

        public GameObject Apply(
            int family,
            int job,
            int sex)
        {
            LegacyCharacterRigSelection selection =
                LegacyCharacterRigCore.Resolve(
                    family,
                    job,
                    sex);

            if (_instance != null &&
                _currentRigIndex ==
                    selection.NativeRigIndex)
            {
                PlaySelect();
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

            if (_instance != null)
                Destroy(_instance);

            _instance =
                Instantiate(
                    rigPrefabs[
                        selection.NativeRigIndex],
                    transform,
                    false);

            _instance.name =
                "Preview_" +
                selection.Prefix;

            _instance.transform.localPosition =
                localPosition;

            _instance.transform.localRotation =
                Quaternion.Euler(
                    localEulerAngles);

            _instance.transform.localScale =
                Vector3.one;

            _currentRigIndex =
                selection.NativeRigIndex;

            CurrentAnimation =
                _instance.GetComponent<
                    SemanticAnimationPlayer>();

            PlaySelect();
            return _instance;
        }

        private void PlaySelect()
        {
            if (CurrentAnimation != null)
                CurrentAnimation.PlaySemantic("select");
        }
    }
}
