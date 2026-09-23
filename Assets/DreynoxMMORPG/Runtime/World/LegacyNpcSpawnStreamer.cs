using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyNpcSpawnDefinition
    {
        public int npcType;
        public int typeId;
        public int modelIndex;
        public int faction;
        public int moveDistance;
        public int moveSpeed;
        public int merchantType = -1;
        public string displayName = string.Empty;
        public string welcomeMessage = string.Empty;
        public NpcServiceKind services;
        public LegacyNpcSaleItemRuntime[] saleItems =
            Array.Empty<LegacyNpcSaleItemRuntime>();
        public int[] inQuestIds =
            Array.Empty<int>();
        public int[] outQuestIds =
            Array.Empty<int>();
        public Vector3 position;
        public float yawDegrees;
        public LegacyNpcGateTargetRuntime[] gateTargets =
            Array.Empty<LegacyNpcGateTargetRuntime>();
        public GameObject prefab;

        [NonSerialized] public GameObject activeInstance;
    }

    public sealed class LegacyNpcSpawnStreamer : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField, Min(10f)] private float activationRadius = 180f;
        [SerializeField, Min(10f)] private float deactivationRadius = 220f;
        [SerializeField, Range(0.05f, 2f)] private float evaluationInterval = 0.35f;
        [SerializeField, Min(1)] private int maxActive = 120;
        [SerializeField] private List<LegacyNpcSpawnDefinition> spawns =
            new List<LegacyNpcSpawnDefinition>();

        private readonly Dictionary<GameObject, Stack<GameObject>> _pool =
            new Dictionary<GameObject, Stack<GameObject>>();

        private float _nextEvaluation;

        public IReadOnlyList<LegacyNpcSpawnDefinition> Spawns => spawns;
        public int LogicalSpawnCount => spawns.Count;

        public int ActiveCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < spawns.Count; i++)
                {
                    if (spawns[i] != null &&
                        spawns[i].activeInstance != null)
                        count++;
                }

                return count;
            }
        }

        public void Configure(
            Transform playerObserver,
            IEnumerable<LegacyNpcSpawnDefinition> definitions)
        {
            observer = playerObserver;
            ReplaceSpawns(definitions);
        }

        public void ReplaceSpawns(
            IEnumerable<LegacyNpcSpawnDefinition> definitions)
        {
            DespawnAll();
            spawns.Clear();

            if (definitions == null)
                return;

            foreach (LegacyNpcSpawnDefinition definition in definitions)
            {
                if (definition == null ||
                    definition.prefab == null)
                    continue;

                spawns.Add(definition);
            }
        }

        private void OnDisable()
        {
            DespawnAll();
        }

        private void Update()
        {
            if (observer == null ||
                Time.unscaledTime < _nextEvaluation)
                return;

            _nextEvaluation =
                Time.unscaledTime + evaluationInterval;

            Evaluate();
        }

        private void Evaluate()
        {
            float activationSqr =
                activationRadius * activationRadius;

            float deactivationSqr =
                deactivationRadius * deactivationRadius;

            int active = ActiveCount;

            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyNpcSpawnDefinition spawn = spawns[i];

                if (spawn == null ||
                    spawn.activeInstance == null)
                    continue;

                if ((observer.position - spawn.position).sqrMagnitude >
                    deactivationSqr)
                {
                    Despawn(spawn);
                    active--;
                }
            }

            if (active >= maxActive)
                return;

            var candidates =
                new List<SpawnCandidate>();

            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyNpcSpawnDefinition spawn = spawns[i];

                if (spawn == null ||
                    spawn.prefab == null ||
                    spawn.activeInstance != null)
                    continue;

                float sqr =
                    (observer.position - spawn.position)
                    .sqrMagnitude;

                if (sqr <= activationSqr)
                {
                    candidates.Add(
                        new SpawnCandidate
                        {
                            definition = spawn,
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
                 active < maxActive;
                 i++)
            {
                Spawn(candidates[i].definition);
                active++;
            }
        }

        private void Spawn(
            LegacyNpcSpawnDefinition definition)
        {
            GameObject instance =
                Rent(definition.prefab);

            instance.transform.SetParent(
                transform,
                false);

            instance.transform.position =
                definition.position;

            instance.transform.rotation =
                Quaternion.Euler(
                    0f,
                    definition.yawDegrees,
                    0f);

            instance.name =
                "NPC_" +
                SanitizeName(definition.displayName) +
                "_Type_" +
                definition.npcType +
                "_Id_" +
                definition.typeId +
                "_Model_" +
                definition.modelIndex;

            LegacyNpcRuntimeDescriptor descriptor =
                instance.GetComponent<
                    LegacyNpcRuntimeDescriptor>();

            if (descriptor == null)
            {
                descriptor =
                    instance.AddComponent<
                        LegacyNpcRuntimeDescriptor>();
            }

            descriptor.Configure(
                definition.npcType,
                definition.typeId,
                definition.modelIndex,
                definition.faction,
                definition.moveDistance,
                definition.moveSpeed,
                definition.merchantType,
                definition.displayName,
                definition.welcomeMessage,
                definition.services,
                definition.saleItems,
                definition.inQuestIds,
                definition.outQuestIds,
                definition.gateTargets);

            SemanticAnimationPlayer animation =
                instance.GetComponent<
                    SemanticAnimationPlayer>();

            if (animation != null)
            {
                if (!animation.PlaySemantic("idle"))
                    animation.PlaySemantic("breath");
            }

            definition.activeInstance = instance;
        }

        private void Despawn(
            LegacyNpcSpawnDefinition definition)
        {
            GameObject instance =
                definition.activeInstance;

            definition.activeInstance = null;

            if (instance == null)
                return;

            instance.SetActive(false);
            instance.transform.SetParent(
                transform,
                false);

            Stack<GameObject> stack;
            if (!_pool.TryGetValue(
                    definition.prefab,
                    out stack))
            {
                stack =
                    new Stack<GameObject>();

                _pool.Add(
                    definition.prefab,
                    stack);
            }

            stack.Push(instance);
        }

        private GameObject Rent(
            GameObject prefab)
        {
            Stack<GameObject> stack;
            if (_pool.TryGetValue(prefab, out stack))
            {
                while (stack.Count > 0)
                {
                    GameObject instance =
                        stack.Pop();

                    if (instance == null)
                        continue;

                    instance.SetActive(true);
                    return instance;
                }
            }

            return Instantiate(prefab);
        }

        private void DespawnAll()
        {
            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyNpcSpawnDefinition spawn =
                    spawns[i];

                if (spawn != null &&
                    spawn.activeInstance != null)
                {
                    Despawn(spawn);
                }
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            return value
                .Trim()
                .Replace(' ', '_')
                .Replace('/', '_')
                .Replace('\\', '_');
        }

        private struct SpawnCandidate
        {
            public LegacyNpcSpawnDefinition definition;
            public float sqrDistance;
        }
    }
}
