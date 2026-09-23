using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    public enum LegacyWorldMarkerKind
    {
        Portal,
        Npc,
        MonsterArea,
        Spawn
    }

    public sealed class LegacyWorldMarker : MonoBehaviour
    {
        [SerializeField] private LegacyWorldMarkerKind kind;
        [SerializeField] private int mapId;
        [SerializeField] private int primaryId;
        [SerializeField] private int secondaryId;
        [SerializeField] private int count;
        [SerializeField] private float rawYawRadians;
        [SerializeField] private Vector3 boundsSize;
        [SerializeField] private Vector3 boundsCenterOffset;

        public LegacyWorldMarkerKind Kind => kind;
        public int MapId => mapId;
        public int PrimaryId => primaryId;
        public int SecondaryId => secondaryId;
        public int Count => count;
        public float RawYawRadians => rawYawRadians;
        public Vector3 BoundsSize => boundsSize;

        public void Configure(
            LegacyWorldMarkerKind markerKind,
            int legacyMapId,
            int id1,
            int id2,
            int quantity,
            float yawRadians,
            Vector3 size,
            Vector3 centerOffset)
        {
            kind = markerKind;
            mapId = legacyMapId;
            primaryId = id1;
            secondaryId = id2;
            count = quantity;
            rawYawRadians = yawRadians;
            boundsSize = size;
            boundsCenterOffset = centerOffset;
        }

        private void OnDrawGizmosSelected()
        {
            Color previous = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                Vector3.one);

            switch (kind)
            {
                case LegacyWorldMarkerKind.Portal:
                    Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
                    Gizmos.DrawWireSphere(Vector3.zero, 1.5f);
                    break;

                case LegacyWorldMarkerKind.Npc:
                    Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                    Gizmos.DrawWireSphere(Vector3.up, 0.45f);
                    Gizmos.DrawLine(Vector3.zero, Vector3.up * 2f);
                    break;

                case LegacyWorldMarkerKind.MonsterArea:
                    Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.65f);
                    Gizmos.DrawWireCube(
                        boundsCenterOffset,
                        boundsSize);
                    break;

                case LegacyWorldMarkerKind.Spawn:
                    Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.9f);
                    Gizmos.DrawWireCube(
                        boundsCenterOffset,
                        boundsSize);
                    break;
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previous;
        }
    }
}
