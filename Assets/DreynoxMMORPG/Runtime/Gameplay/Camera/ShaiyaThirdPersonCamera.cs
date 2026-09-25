using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.CameraSystem
{
    public sealed class ShaiyaThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private float distance = 6.5f, minDistance = 2f, maxDistance = 12f;
        [SerializeField] private float yaw = 180f, pitch = 18f, minPitch = -8f, maxPitch = 65f;
        [SerializeField] private float lookSensitivity = 3f, zoomSensitivity = 1.2f;
        [SerializeField] private float sphereRadius = 0.25f, collisionPadding = 0.12f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float positionSmoothing = 22f;
        private Vector2 externalLook;
        private float externalZoom, currentDistance = -1f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        public Transform Target => target;
        public float Distance => distance;
        public float ResolvedDistance => currentDistance;
        public float Yaw => yaw;
        public float Pitch => pitch;

        public void SetTarget(Transform value) { target = value; currentDistance = -1f; }
        public void AddLookInput(Vector2 delta) { externalLook += delta; }
        public void AddZoomInput(float delta) { externalZoom += delta; }
        public void ConfigureView(float newYaw, float newPitch, float newDistance)
        {
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            currentDistance = -1f;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector2 look = externalLook;
            float zoom = externalZoom;
            externalLook = Vector2.zero; externalZoom = 0f;
            if (Application.isFocused && Input.GetMouseButton(1))
                look += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            if (Application.isFocused) zoom += Input.mouseScrollDelta.y;
            Step(Time.deltaTime, look, zoom);
        }

        public void Step(float deltaTime, Vector2 look, float zoom)
        {
            if (target == null) return;
            yaw = Mathf.Repeat(yaw + look.x * lookSensitivity, 360f);
            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - zoom * zoomSensitivity, minDistance, maxDistance);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 backward = rotation * Vector3.back;
            float allowed = FindDistance(pivot, backward, distance);
            // Retract immediately on collision. Smooth only outward recovery; world-position
            // lerp can sweep through a wall during an orbit even when the final cast is safe.
            if (currentDistance < 0f || allowed < currentDistance) currentDistance = allowed;
            else currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-positionSmoothing * Mathf.Max(0f, deltaTime)));
            transform.SetPositionAndRotation(pivot + backward * currentDistance, rotation);
        }

        private float FindDistance(Vector3 pivot, Vector3 direction, float desired)
        {
            int count = Physics.SphereCastNonAlloc(pivot, sphereRadius, direction, hits, desired, collisionMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] values = hits;
            // Never silently miss the nearest wall when the reusable buffer fills.
            if (count == hits.Length)
            {
                values = Physics.SphereCastAll(pivot, sphereRadius, direction, desired, collisionMask, QueryTriggerInteraction.Ignore);
                count = values.Length;
            }
            float nearest = desired;
            for (int i = 0; i < count; i++)
            {
                Collider collider = values[i].collider;
                if (collider == null || collider.transform.IsChildOf(target) || collider.transform.IsChildOf(transform)) continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0.05f, values[i].distance - collisionPadding));
            }
            return nearest;
        }
    }
}
