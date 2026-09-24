using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Vfx;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyWorldEffectPlacement
    {
        public int sequenceIndex;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public GameObject effectPrefab;

        [NonSerialized] public GameObject activeInstance;
        [NonSerialized] public uint activeLeaseId;
        [NonSerialized] public bool triggeredWhileResident;
    }

    public sealed class LegacyWorldEffectStreamer : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField, Min(16f)] private float activationRadius = 260f;
        [SerializeField, Min(16f)] private float deactivationRadius = 340f;
        [SerializeField, Range(0.05f, 2f)] private float evaluationInterval = 0.30f;
        [SerializeField, Min(1)] private int maxResidentPlacements = 96;
        [SerializeField] private List<LegacyWorldEffectPlacement> placements =
            new List<LegacyWorldEffectPlacement>();

        private float _nextEvaluation;

        public IReadOnlyList<LegacyWorldEffectPlacement> Placements =>
            placements;

        public int LogicalPlacementCount =>
            placements.Count;

        public int ActiveCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < placements.Count; i++)
                {
                    if (OwnsActiveLease(placements[i]))
                        count++;
                }

                return count;
            }
        }

        public void Configure(
            Transform playerObserver,
            IEnumerable<LegacyWorldEffectPlacement> source)
        {
            observer = playerObserver;
            ReplacePlacements(source);
        }

        public void ReplacePlacements(
            IEnumerable<LegacyWorldEffectPlacement> source)
        {
            ReleaseAll();
            placements.Clear();

            if (source == null)
                return;

            foreach (LegacyWorldEffectPlacement placement in source)
            {
                if (placement == null ||
                    placement.effectPrefab == null ||
                    placement.sequenceIndex < 0)
                    continue;

                placements.Add(placement);
            }
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        private void Update()
        {
            if (observer == null ||
                Time.unscaledTime < _nextEvaluation)
                return;

            _nextEvaluation =
                Time.unscaledTime +
                evaluationInterval;

            Evaluate();
        }

        private void Evaluate()
        {
            float loadSqr =
                activationRadius *
                activationRadius;

            float unloadSqr =
                deactivationRadius *
                deactivationRadius;

            int active =
                ActiveCount;

            for (int i = 0; i < placements.Count; i++)
            {
                LegacyWorldEffectPlacement placement =
                    placements[i];

                if (placement == null)
                    continue;

                if (placement.activeInstance != null &&
                    !OwnsActiveLease(placement))
                {
                    placement.activeInstance = null;
                    placement.activeLeaseId = 0;
                }

                float sqr =
                    (observer.position -
                     placement.position)
                    .sqrMagnitude;

                if (sqr > unloadSqr)
                {
                    if (OwnsActiveLease(placement))
                    {
                        Release(placement);
                        active--;
                    }

                    // A one-shot placement may be triggered again only after
                    // the player actually leaves its resident ring.
                    placement.triggeredWhileResident =
                        false;
                }
            }

            if (active >= maxResidentPlacements)
                return;

            var candidates =
                new List<Candidate>();

            for (int i = 0; i < placements.Count; i++)
            {
                LegacyWorldEffectPlacement placement =
                    placements[i];

                if (placement == null ||
                    placement.effectPrefab == null ||
                    placement.triggeredWhileResident)
                    continue;

                float sqr =
                    (observer.position -
                     placement.position)
                    .sqrMagnitude;

                if (sqr <= loadSqr)
                {
                    candidates.Add(
                        new Candidate
                        {
                            placement = placement,
                            sqrDistance = sqr
                        });
                }
            }

            candidates.Sort(
                (left, right) =>
                    left.sqrDistance.CompareTo(
                        right.sqrDistance));

            for (int i = 0;
                 i < candidates.Count &&
                 active < maxResidentPlacements;
                 i++)
            {
                LegacyWorldEffectPlacement placement =
                    candidates[i].placement;

                placement.triggeredWhileResident =
                    true;

                GameObject instance =
                    LegacyEffectPool.PlaySequence(
                        placement.effectPrefab,
                        placement.sequenceIndex,
                        forceOneShot: false,
                        placement.position,
                        placement.rotation);

                if (instance == null)
                    continue;

                LegacyPooledEffectInstance lease =
                    instance.GetComponent<
                        LegacyPooledEffectInstance>();

                placement.activeInstance =
                    instance;

                placement.activeLeaseId =
                    lease != null
                        ? lease.LeaseId
                        : 0;

                active++;
            }
        }

        private static bool OwnsActiveLease(
            LegacyWorldEffectPlacement placement)
        {
            if (placement == null ||
                placement.activeInstance == null)
                return false;

            LegacyPooledEffectInstance lease =
                placement.activeInstance
                    .GetComponent<
                        LegacyPooledEffectInstance>();

            return lease != null &&
                   !lease.IsPooled &&
                   lease.LeaseId != 0 &&
                   lease.LeaseId ==
                   placement.activeLeaseId;
        }

        private static void Release(
            LegacyWorldEffectPlacement placement)
        {
            if (!OwnsActiveLease(placement))
            {
                placement.activeInstance = null;
                placement.activeLeaseId = 0;
                return;
            }

            LegacyEffectPool.Release(
                placement.activeInstance);

            placement.activeInstance = null;
            placement.activeLeaseId = 0;
        }

        private void ReleaseAll()
        {
            for (int i = 0; i < placements.Count; i++)
            {
                LegacyWorldEffectPlacement placement =
                    placements[i];

                if (placement == null)
                    continue;

                Release(placement);
                placement.triggeredWhileResident =
                    false;
            }
        }

        private struct Candidate
        {
            public LegacyWorldEffectPlacement placement;
            public float sqrDistance;
        }
    }
}
