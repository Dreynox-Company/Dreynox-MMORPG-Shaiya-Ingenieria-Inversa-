using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public sealed class LegacyPortalRuntime : MonoBehaviour
    {
        [SerializeField] private int sourceMapId;
        [SerializeField] private int portalId;
        [SerializeField] private int minimumLevel;
        [SerializeField] private int maximumLevel;
        [SerializeField] private int targetMapId;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private bool isOpen;

        public int SourceMapId => sourceMapId;
        public int PortalId => portalId;
        public int MinimumLevel => minimumLevel;
        public int MaximumLevel => maximumLevel;
        public int TargetMapId => targetMapId;
        public Vector3 TargetPosition => targetPosition;
        public bool IsOpen => isOpen;
        public bool IsBossActivatedPortal => portalId > 2;

        public void Configure(
            int sourceMap,
            int id,
            int minLevel,
            int maxLevel,
            int targetMap,
            Vector3 target)
        {
            var core =
                new PortalTravelCore(
                    sourceMap,
                    id,
                    minLevel,
                    maxLevel,
                    targetMap,
                    target.x,
                    target.y,
                    target.z);

            sourceMapId =
                core.SourceMapId;
            portalId =
                core.PortalId;
            minimumLevel =
                core.MinimumLevel;
            maximumLevel =
                core.MaximumLevel;
            targetMapId =
                core.TargetMapId;
            targetPosition =
                new Vector3(
                    (float)core.TargetX,
                    (float)core.TargetY,
                    (float)core.TargetZ);

            isOpen =
                core.IsOpenByDefault;
        }

        public bool CanUse(
            int playerLevel,
            int playerFaction)
        {
            var core =
                ToCore();

            return core.CanEnter(
                playerLevel,
                playerFaction,
                isOpen);
        }

        public void SetOpen(bool value)
        {
            if (!IsBossActivatedPortal)
            {
                isOpen = true;
                return;
            }

            isOpen = value;
        }

        private PortalTravelCore ToCore()
        {
            return new PortalTravelCore(
                sourceMapId,
                portalId,
                minimumLevel,
                maximumLevel,
                targetMapId,
                targetPosition.x,
                targetPosition.y,
                targetPosition.z);
        }
    }
}
