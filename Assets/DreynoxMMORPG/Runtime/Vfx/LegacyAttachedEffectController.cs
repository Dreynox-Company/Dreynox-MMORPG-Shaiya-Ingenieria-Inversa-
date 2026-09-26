using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    public enum LegacyEftInvocationKind
    {
        Sequence,
        RawEffect
    }

    [Serializable]
    public sealed class LegacyAttachedEffectBinding
    {
        public Transform bone;
        public GameObject effectPrefab;
        public int effectIndex;
        public LegacyEftInvocationKind invocationKind;
    }

    public sealed class LegacyAttachedEffectController : MonoBehaviour
    {
        [SerializeField] private LegacyAttachedEffectBinding[] bindings =
            Array.Empty<LegacyAttachedEffectBinding>();

        private readonly List<GameObject> _active =
            new List<GameObject>();

        public IReadOnlyList<LegacyAttachedEffectBinding> Bindings =>
            bindings;

        public void Configure(
            LegacyAttachedEffectBinding[] source)
        {
            StopAll();
            bindings =
                source ??
                Array.Empty<LegacyAttachedEffectBinding>();
        }

        private void OnEnable()
        {
            StartAll();
        }

        private void OnDisable()
        {
            StopAll();
        }

        private void OnDestroy()
        {
            StopAll();
        }

        public void StartAll()
        {
            StopAll();

            for (int i = 0;
                 i < bindings.Length;
                 i++)
            {
                LegacyAttachedEffectBinding binding =
                    bindings[i];

                if (binding == null ||
                    binding.bone == null ||
                    binding.effectPrefab == null ||
                    binding.effectIndex < 0)
                    continue;

                GameObject instance;

                if (binding.invocationKind ==
                    LegacyEftInvocationKind.RawEffect)
                {
                    instance =
                        LegacyEffectPool.PlayRawEffect(
                            binding.effectPrefab,
                            binding.effectIndex,
                            forceOneShot: false,
                            Vector3.zero,
                            Quaternion.identity,
                            binding.bone);
                }
                else
                {
                    instance =
                        LegacyEffectPool.PlaySequence(
                            binding.effectPrefab,
                            binding.effectIndex,
                            forceOneShot: false,
                            Vector3.zero,
                            Quaternion.identity,
                            binding.bone);
                }

                if (instance != null)
                    _active.Add(instance);
            }
        }

        public void StopAll()
        {
            for (int i = 0;
                 i < _active.Count;
                 i++)
            {
                LegacyEffectPool.Release(
                    _active[i]);
            }

            _active.Clear();
        }
    }
}
