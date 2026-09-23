using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.CameraSystem
{
    public sealed class ShaiyaThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private float distance = 6.5f;
        [SerializeField] private float minDistance = 2.0f;
        [SerializeField] private float maxDistance = 12.0f;
        [SerializeField] private float yaw = 180f;
        [SerializeField] private float pitch = 18f;
        [SerializeField] private float minPitch = -8f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float lookSensitivity = 3f;
        [SerializeField] private float zoomSensitivity = 1.2f;
        [SerializeField] private float sphereRadius = 0.25f;
        [SerializeField] private float collisionPadding = 0.12f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float positionSmoothing = 22f;

        private Vector2 _externalLook;
        private float _externalZoom;

        public Transform Target => target;
        public float Distance => distance;
        public float Yaw => yaw;
        public float Pitch => pitch;

        public void SetTarget(Transform value) => target = value;
        public void AddLookInput(Vector2 delta) => _externalLook += delta;
        public void AddZoomInput(float delta) => _externalZoom += delta;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector2 look = _externalLook;
            float zoom = _externalZoom;
            _externalLook = Vector2.zero;
            _externalZoom = 0f;

            if (Input.GetMouseButton(1))
            {
                look.x += Input.GetAxisRaw("Mouse X");
                look.y += Input.GetAxisRaw("Mouse Y");
            }
            zoom += Input.mouseScrollDelta.y;

            yaw += look.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - zoom * zoomSensitivity, minDistance, maxDistance);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 backward = rotation * Vector3.back;
            float resolvedDistance = distance;
            if (Physics.SphereCast(pivot, sphereRadius, backward, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                resolvedDistance = Mathf.Max(minDistance * 0.35f, hit.distance - collisionPadding);

            Vector3 wanted = pivot + backward * resolvedDistance;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime));
            transform.rotation = rotation;
        }
    }
}
