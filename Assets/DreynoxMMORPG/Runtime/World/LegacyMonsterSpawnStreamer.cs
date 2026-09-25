using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    [Serializable]
    public sealed class LegacyMonsterSpawnDefinition
    {
        public uint mobId;
        public int modelIndex;
        public int targetId;
        public string mobName = string.Empty;
        public int level;
        public int maxHealth = 1;
        public byte ai;
        public byte element;
        public byte rawSize;
        public float scale = 1f;
        public Vector3 position;
        public float yawDegrees;
        public GameObject prefab;

        [NonSerialized] public GameObject activeInstance;
        // Streaming is not respawning: damage and death persist until an explicit
        // server/qualified local respawn policy replaces this spawn definition.
        [NonSerialized] public int remainingHealth = -1;
    }

    public sealed class LegacyMonsterSpawnStreamer : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField, Min(10f)] private float activationRadius = 130f;
        [SerializeField, Min(10f)] private float deactivationRadius = 165f;
        [SerializeField, Range(0.05f, 2f)] private float evaluationInterval = 0.25f;
        [SerializeField, Min(1)] private int maxActive = 220;
        [SerializeField] private List<LegacyMonsterSpawnDefinition> spawns =
            new List<LegacyMonsterSpawnDefinition>();

        private readonly Dictionary<GameObject, Stack<GameObject>> _pool =
            new Dictionary<GameObject, Stack<GameObject>>();

        private float _nextEvaluation;

        public IReadOnlyList<LegacyMonsterSpawnDefinition> Spawns => spawns;

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
            IEnumerable<LegacyMonsterSpawnDefinition> definitions)
        {
            observer = playerObserver;
            ReplaceSpawns(definitions);
        }

        public void ReplaceSpawns(
            IEnumerable<LegacyMonsterSpawnDefinition> definitions)
        {
            DespawnAll();
            spawns.Clear();

            if (definitions == null)
                return;

            foreach (LegacyMonsterSpawnDefinition definition in definitions)
            {
                if (definition == null ||
                    definition.prefab == null ||
                    definition.maxHealth <= 0)
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

        public void EvaluateNow() { if (observer != null) Evaluate(); }

        private void Evaluate()
        {
            float loadSqr =
                activationRadius * activationRadius;

            float unloadSqr =
                deactivationRadius * deactivationRadius;

            int active = ActiveCount;

            // Despawn first so capacity is immediately reusable.
            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyMonsterSpawnDefinition spawn = spawns[i];
                if (spawn == null ||
                    spawn.activeInstance == null)
                    continue;

                float sqr =
                    (observer.position - spawn.position)
                    .sqrMagnitude;

                if (sqr > unloadSqr)
                {
                    Despawn(spawn);
                    active--;
                }
            }

            if (active >= maxActive)
                return;

            // Activate nearest candidates first. The map may contain more than a
            // thousand logical spawns, while mobile should animate only nearby
            // entities.
            var candidates =
                new List<SpawnCandidate>();

            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyMonsterSpawnDefinition spawn = spawns[i];

                if (spawn == null ||
                    spawn.activeInstance != null ||
                    spawn.prefab == null || spawn.remainingHealth == 0)
                    continue;

                float sqr =
                    (observer.position - spawn.position)
                    .sqrMagnitude;

                if (sqr <= loadSqr)
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
                (a, b) =>
                    a.sqrDistance.CompareTo(b.sqrDistance));

            for (int i = 0;
                 i < candidates.Count && active < maxActive;
                 i++)
            {
                Spawn(candidates[i].definition);
                active++;
            }
        }

        private void Spawn(
            LegacyMonsterSpawnDefinition definition)
        {
            GameObject instance = Rent(definition.prefab);

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

            float resolvedScale =
                Mathf.Max(0.05f, definition.scale);

            instance.transform.localScale =
                definition.prefab.transform.localScale *
                resolvedScale;

            instance.name =
                "Mob_" +
                definition.mobId +
                "_" +
                SanitizeName(definition.mobName) +
                "_Model_" +
                definition.modelIndex +
                "_Lv_" +
                definition.level +
                "_Target_" +
                definition.targetId;

            ShaiyaCombatTarget target =
                instance.GetComponent<ShaiyaCombatTarget>();

            if (target != null)
            {
                target.Configure(
                    definition.targetId,
                    definition.maxHealth,
                    definition.remainingHealth);
            }

            instance.SetActive(true);
            SemanticAnimationPlayer animation =
                instance.GetComponent<SemanticAnimationPlayer>();

            if (animation != null)
            {
                if (!animation.PlaySemantic("idle"))
                    animation.PlaySemantic("breath");
            }

            definition.activeInstance = instance;
        }

        private void Despawn(
            LegacyMonsterSpawnDefinition definition)
        {
            GameObject instance =
                definition.activeInstance;

            definition.activeInstance = null;

            if (instance == null)
                return;

            ShaiyaCombatTarget health = instance.GetComponent<ShaiyaCombatTarget>();
            if (health != null) definition.remainingHealth = health.Health;
            instance.SetActive(false);
            instance.transform.SetParent(
                transform,
                false);

            Stack<GameObject> stack;
            if (!_pool.TryGetValue(definition.prefab, out stack))
            {
                stack = new Stack<GameObject>();
                _pool.Add(definition.prefab, stack);
            }

            stack.Push(instance);
        }

        private GameObject Rent(GameObject prefab)
        {
            Stack<GameObject> stack;
            if (_pool.TryGetValue(prefab, out stack))
            {
                while (stack.Count > 0)
                {
                    GameObject pooled = stack.Pop();
                    if (pooled == null)
                        continue;

                    return pooled;
                }
            }

            GameObject instance = Instantiate(prefab);
            instance.SetActive(false);
            return instance;
        }

        private void DespawnAll()
        {
            for (int i = 0; i < spawns.Count; i++)
            {
                LegacyMonsterSpawnDefinition spawn = spawns[i];
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
            public LegacyMonsterSpawnDefinition definition;
            public float sqrDistance;
        }
    }
}
