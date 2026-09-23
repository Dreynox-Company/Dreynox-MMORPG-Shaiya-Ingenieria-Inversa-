using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.CameraSystem
{
    public sealed class ShaiyaThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField, Range(-80f, 80f)] private float pitch = 18f;
        [SerializeField] private float yaw;
        [SerializeField, Min(1f)] private float distance = 6f;
        [SerializeField, Min(1f)] private float minDistance = 2f;
        [SerializeField, Min(1f)] private float maxDistance = 11f;
        [SerializeField] private float lookSensitivity = 0.16f;
        [SerializeField] private float zoomSensitivity = 1.4f;
        [SerializeField] private float collisionRadius = 0.24f;
        [SerializeField] private float collisionPadding = 0.12f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float positionSharpness = 24f;

        public float MovementYawDegrees => yaw;
        public Transform Target => target;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            if (target != null) yaw = target.eulerAngles.y;
        }

        public void AddLookDelta(Vector2 delta)
        {
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -20f, 68f);
        }

        public void AddZoom(float delta)
        {
            distance = Mathf.Clamp(distance - delta * zoomSensitivity, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 backward = rotation * Vector3.back;
            float resolvedDistance = distance;

            RaycastHit hit;
            if (Physics.SphereCast(
                    pivot,
                    collisionRadius,
                    backward,
                    out hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                resolvedDistance = Mathf.Max(minDistance * 0.35f, hit.distance - collisionPadding);
            }

            Vector3 wanted = pivot + backward * resolvedDistance;
            float t = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, wanted, t);
            transform.rotation = rotation;
        }
    }
}
