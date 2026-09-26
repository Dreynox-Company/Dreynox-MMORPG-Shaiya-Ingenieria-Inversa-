using System;
using System.Linq;
using System.Reflection;
using Dreynox.Mmorpg.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    /// <summary>
    /// Exercises actual pool checkout/return, not runtime-only OnEnable events
    /// assumed to fire in EditMode. The production Spawn calls Configure.
    /// </summary>
    internal sealed class NpcPoolTestLease : IDisposable
    {
        private readonly GameObject root;
        private readonly LegacyNpcSpawnStreamer streamer;
        private readonly LegacyNpcSpawnDefinition definition;
        private static readonly MethodInfo Spawn = typeof(LegacyNpcSpawnStreamer).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Despawn = typeof(LegacyNpcSpawnStreamer).GetMethod("Despawn", BindingFlags.Instance | BindingFlags.NonPublic);
        public LegacyNpcRuntimeDescriptor Current => definition.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>();

        public NpcPoolTestLease(Transform parent, Transform observer, LegacyNpcRuntimeDescriptor source)
        {
            if (Spawn == null || Despawn == null)
                throw new InvalidOperationException("NPC pool entrypoints changed; update the real pool regression fixture.");
            root = new GameObject("Production NPC pool test lease");
            root.transform.SetParent(parent, false);
            try
            {
                var prefab = new GameObject("Synthetic pool prefab");
                prefab.transform.SetParent(root.transform, false);
                streamer = root.AddComponent<LegacyNpcSpawnStreamer>();
                definition = new LegacyNpcSpawnDefinition
                {
                    npcType = source.NpcType, typeId = source.TypeId, modelIndex = source.ModelIndex,
                    faction = source.Faction, moveDistance = source.MoveDistance, moveSpeed = source.MoveSpeed,
                    merchantType = source.MerchantType, displayName = source.DisplayName, welcomeMessage = source.WelcomeMessage,
                    services = source.Services, saleItems = source.SaleItems.ToArray(), inQuestIds = source.InQuestIds.ToArray(),
                    outQuestIds = source.OutQuestIds.ToArray(), gateTargets = source.GateTargets.ToArray(),
                    position = source.transform.position, yawDegrees = source.transform.eulerAngles.y, prefab = prefab
                };
                streamer.Configure(observer, new[] { definition });
                Spawn.Invoke(streamer, new object[] { definition });
            }
            catch { Object.DestroyImmediate(root); throw; }
        }
        public LegacyNpcRuntimeDescriptor Recycle()
        {
            Despawn.Invoke(streamer, new object[] { definition });
            Spawn.Invoke(streamer, new object[] { definition });
            Physics.SyncTransforms();
            return Current;
        }
        public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
    }
}
