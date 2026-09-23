using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.CameraSystem
{
    public sealed class ThirdPersonCameraCollision : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private float desiredDistance = 6f;
        [SerializeField] private float sphereRadius = 0.24f;
        [SerializeField] private float collisionPadding = 0.12f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float smoothing = 18f;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 pivot = target.position + pivotOffset;
            Vector3 backward = -transform.forward;
            float distance = desiredDistance;
            if (Physics.SphereCast(pivot, sphereRadius, backward, out RaycastHit hit, desiredDistance, collisionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0.4f, hit.distance - collisionPadding);
            Vector3 wanted = pivot + backward * distance;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        }
    }
}
