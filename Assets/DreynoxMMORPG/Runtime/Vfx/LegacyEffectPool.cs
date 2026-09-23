using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    public sealed class LegacyPooledEffectInstance : MonoBehaviour
    {
        [System.NonSerialized] internal GameObject sourcePrefab;
        [System.NonSerialized] internal bool returning;
        [System.NonSerialized] internal bool pooled;

        private void OnDisable()
        {
            if (!returning &&
                !pooled &&
                sourcePrefab != null &&
                Application.isPlaying)
            {
                LegacyEffectPool.Return(this);
            }
        }
    }

    public static class LegacyEffectPool
    {
        private static readonly Dictionary<GameObject, Stack<LegacyPooledEffectInstance>>
            Pool =
                new Dictionary<GameObject, Stack<LegacyPooledEffectInstance>>();

        public static GameObject Play(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            return PlaySequence(
                prefab,
                0,
                forceOneShot: true,
                position,
                rotation,
                parent);
        }

        public static GameObject PlaySequence(
            GameObject prefab,
            int sequenceIndex,
            bool forceOneShot,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            return PlayInternal(
                prefab,
                position,
                rotation,
                parent,
                player =>
                    player.PlaySequence(
                        sequenceIndex,
                        forceOneShot));
        }

        public static GameObject PlayRawEffect(
            GameObject prefab,
            int effectIndex,
            bool forceOneShot,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            return PlayInternal(
                prefab,
                position,
                rotation,
                parent,
                player =>
                    player.PlayRawEffect(
                        effectIndex,
                        forceOneShot));
        }

        private static GameObject PlayInternal(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            System.Func<LegacyEftSequencePlayer, bool> invoke)
        {
            if (prefab == null)
                return null;

            LegacyPooledEffectInstance item =
                Rent(prefab);

            Transform transform =
                item.transform;

            transform.SetParent(
                parent,
                worldPositionStays: false);

            if (parent == null)
            {
                transform.position = position;
                transform.rotation = rotation;
            }
            else
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
            }

            item.gameObject.SetActive(true);

            LegacyEftSequencePlayer player =
                item.GetComponent<
                    LegacyEftSequencePlayer>();

            if (player == null ||
                invoke == null ||
                !invoke(player))
            {
                Return(item);
                return null;
            }

            return item.gameObject;
        }

        public static void Release(
            GameObject instance)
        {
            if (instance == null)
                return;

            LegacyPooledEffectInstance item =
                instance.GetComponent<
                    LegacyPooledEffectInstance>();

            if (item == null ||
                item.pooled)
                return;

            Return(item);
        }

        public static void Clear()
        {
            foreach (KeyValuePair<GameObject, Stack<LegacyPooledEffectInstance>> pair in Pool)
            {
                while (pair.Value.Count > 0)
                {
                    LegacyPooledEffectInstance item =
                        pair.Value.Pop();

                    if (item != null)
                        Object.Destroy(item.gameObject);
                }
            }

            Pool.Clear();
        }

        internal static void Return(
            LegacyPooledEffectInstance item)
        {
            if (item == null ||
                item.sourcePrefab == null ||
                item.pooled)
                return;

            item.returning = true;
            item.pooled = true;

            LegacyEftSequencePlayer sequence =
                item.GetComponent<
                    LegacyEftSequencePlayer>();

            if (sequence != null)
                sequence.Stop();

            item.transform.SetParent(
                null,
                worldPositionStays: false);

            if (item.gameObject.activeSelf)
                item.gameObject.SetActive(false);

            Stack<LegacyPooledEffectInstance> stack;
            if (!Pool.TryGetValue(
                    item.sourcePrefab,
                    out stack))
            {
                stack =
                    new Stack<
                        LegacyPooledEffectInstance>();

                Pool.Add(
                    item.sourcePrefab,
                    stack);
            }

            stack.Push(item);
            item.returning = false;
        }

        private static LegacyPooledEffectInstance Rent(
            GameObject prefab)
        {
            Stack<LegacyPooledEffectInstance> stack;

            if (Pool.TryGetValue(
                    prefab,
                    out stack))
            {
                while (stack.Count > 0)
                {
                    LegacyPooledEffectInstance pooled =
                        stack.Pop();

                    if (pooled == null)
                        continue;

                    pooled.sourcePrefab =
                        prefab;
                    pooled.pooled = false;

                    return pooled;
                }
            }

            GameObject instance =
                Object.Instantiate(prefab);

            instance.name =
                prefab.name + "_Pooled";

            LegacyPooledEffectInstance item =
                instance.GetComponent<
                    LegacyPooledEffectInstance>();

            if (item == null)
            {
                item =
                    instance.AddComponent<
                        LegacyPooledEffectInstance>();
            }

            item.sourcePrefab = prefab;
            item.pooled = false;

            item.returning = true;
            instance.SetActive(false);
            item.returning = false;

            return item;
        }
    }
}
