using System;
using UnityEngine;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;

namespace Dreynox.Mmorpg.World
{
    /// <summary>Find actual walkable support near an authored position, not just terrain height.</summary>
    public static class WorldGroundPlacement
    {
        public static bool TryFind(CharacterController body, Vector3 hint, out Vector3 position,
            out string reason, float horizontalRadius = 3f, float verticalTolerance = 8f)
        {
            position = hint; reason = "No walkable, unobstructed surface near authored position.";
            if (body == null || !Finite(hint) || horizontalRadius < 0 || verticalTolerance <= 0)
                return false;
            Vector3 scale = body.transform.lossyScale;
            if (Mathf.Abs(scale.x - 1) > 0.001f || Mathf.Abs(scale.y - 1) > 0.001f || Mathf.Abs(scale.z - 1) > 0.001f)
            { reason = "Ground placement requires an unscaled upright actor controller."; return false; }
            int rings = Mathf.CeilToInt(horizontalRadius / 0.5f);
            for (int ring = 0; ring <= rings; ring++)
            {
                int samples = ring == 0 ? 1 : 16;
                float radius = Mathf.Min(horizontalRadius, ring * 0.5f);
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * 2f * Mathf.PI / samples;
                    Vector3 anchor = hint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    RaycastHit[] hits = Physics.RaycastAll(anchor + Vector3.up * verticalTolerance,
                        Vector3.down, 2 * verticalTolerance, ~0, QueryTriggerInteraction.Ignore);
                    Array.Sort(hits, (a, b) => Mathf.Abs(a.point.y - hint.y).CompareTo(Mathf.Abs(b.point.y - hint.y)));
                    foreach (var hit in hits)
                    {
                        if (!WorldCollider(hit.collider, body.transform)) continue;
                        if (Vector3.Dot(hit.normal, Vector3.up) < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                        float margin = Mathf.Max(0.04f, body.skinWidth + 0.02f);
                        float bottomOffset = body.center.y - body.height * 0.5f;
                        Vector3 candidate = new Vector3(anchor.x, hit.point.y + margin - bottomOffset, anchor.z);
                        if (!IsClear(body, candidate, margin * 0.5f)) continue;
                        position = candidate; reason = hit.collider.name;
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool TryPlace(ShaiyaClientActor actor, Vector3 hint, out string reason,
            float horizontalRadius = 3f, float verticalTolerance = 8f)
        {
            if (actor == null) { reason = "Actor is missing."; return false; }
            Physics.SyncTransforms();
            var body = actor.GetComponent<CharacterController>();
            if (!TryFind(body, hint, out Vector3 position, out reason, horizontalRadius, verticalTolerance)) return false;
            actor.TeleportGrounded(position);
            Physics.SyncTransforms();
            return true;
        }
        public static bool IsClear(CharacterController body, Vector3 position, float inset = 0.01f)
        {
            float r = Mathf.Max(0.02f, body.radius - inset);
            float halfSegment = Mathf.Max(0, body.height * 0.5f - body.radius);
            Vector3 center = position + body.center;
            foreach (Collider other in Physics.OverlapCapsule(center + Vector3.up * halfSegment,
                center - Vector3.up * halfSegment, r, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == null || other.transform.IsChildOf(body.transform)) continue;
                // Triggers excluded, but NPC/mob bodies remain obstacles for player placement.
                return false;
            }
            return true;
        }
        private static bool WorldCollider(Collider other, Transform actor)
        {
            return other != null && !other.transform.IsChildOf(actor) &&
                other.GetComponentInParent<ShaiyaCombatTarget>() == null &&
                other.GetComponentInParent<LegacyNpcRuntimeDescriptor>() == null &&
                other.GetComponentInParent<CharacterController>() == null;
        }
        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
