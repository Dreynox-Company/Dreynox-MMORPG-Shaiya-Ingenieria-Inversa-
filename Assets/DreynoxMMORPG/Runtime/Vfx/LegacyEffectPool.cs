using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Vfx
{
    public sealed class LegacyPooledEffectInstance : MonoBehaviour
    {
        [System.NonSerialized] internal GameObject sourcePrefab;
        [System.NonSerialized] internal bool returning;

        private void OnDisable()
        {
            if (!returning &&
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

            LegacyEftSequencePlayer sequence =
                item.GetComponent<
                    LegacyEftSequencePlayer>();

            if (sequence != null)
                sequence.PlayDefault();

            return item.gameObject;
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
                item.sourcePrefab == null)
                return;

            item.returning = true;

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

            item.returning = true;
            instance.SetActive(false);
            item.returning = false;

            return item;
        }
    }
}
