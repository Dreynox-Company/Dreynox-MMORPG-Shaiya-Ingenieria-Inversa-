using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Gameplay.CameraSystem
{
    /// <summary>Gameplay-relative orbit with immediate wall retraction, not world-position lerp.</summary>
    [RequireComponent(typeof(Camera))]
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
        [SerializeField, Min(0.1f)] private float avatarClearance = 0.8f;
        private Vector2 externalLook;
        private float externalZoom, currentDistance = -1f;
        private Camera view;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private readonly List<Renderer> avatarRenderers = new List<Renderer>();
        private readonly List<ShadowCastingMode> originalModes = new List<ShadowCastingMode>();
        private bool hidingForCamera;
        public Transform Target => target;
        public float Distance => distance;
        public float ResolvedDistance => currentDistance;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool AvatarOccluded { get; private set; }
        public bool PivotObstructed { get; private set; }
        public float CollisionRadius => Mathf.Max(sphereRadius, NearPlaneRadius(view));

        private void Awake() { view = GetComponent<Camera>(); }
        private void OnEnable()
        {
            view = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginRender;
            RenderPipelineManager.endCameraRendering += EndRender;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginRender;
            RenderPipelineManager.endCameraRendering -= EndRender;
            RestoreAvatar();
        }
        public void SetTarget(Transform value)
        {
            RestoreAvatar(); target = value; currentDistance = -1f; AvatarOccluded = false;
        }
        public void AddLookInput(Vector2 delta) { externalLook += delta; }
        public void AddZoomInput(float delta) { externalZoom += delta; }
        public void ConfigureView(float newYaw, float newPitch, float newDistance)
        {
            if (!Finite(newYaw) || !Finite(newPitch) || !Finite(newDistance))
                throw new ArgumentOutOfRangeException(nameof(newYaw));
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
            distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
            currentDistance = -1f;
        }
        private void LateUpdate()
        {
            Vector2 look = externalLook; float zoom = externalZoom;
            externalLook = Vector2.zero; externalZoom = 0f;
            if (target == null) return;
            if (Application.isFocused && Input.GetMouseButton(1))
                look += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            if (Application.isFocused) zoom += Input.mouseScrollDelta.y;
            Step(Time.deltaTime, look, zoom);
        }
        public void Step(float deltaTime, Vector2 look, float zoom)
        {
            if (!Finite(deltaTime) || deltaTime < 0 || !Finite(look.x) || !Finite(look.y) || !Finite(zoom))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (target == null) return;
            yaw = Mathf.Repeat(yaw + look.x * lookSensitivity, 360f);
            pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - zoom * zoomSensitivity, minDistance, maxDistance);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 backward = rotation * Vector3.back;
            float allowed = FindDistance(pivot, backward, distance);
            if (currentDistance < 0 || allowed < currentDistance) currentDistance = allowed;
            else currentDistance = Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-positionSmoothing * deltaTime));
            transform.SetPositionAndRotation(pivot + backward * currentDistance, rotation);
            // If a wall leaves no room for both camera and body, hide the whole avatar
            // for this camera only. Never delete a head mesh or push through the wall.
            AvatarOccluded = currentDistance < avatarClearance + CollisionRadius;
        }
        public static float NearPlaneRadius(Camera camera)
        {
            if (camera == null) return 0f;
            float halfHeight = camera.orthographic ? camera.orthographicSize :
                camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float halfWidth = halfHeight * camera.aspect;
            return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight +
                camera.nearClipPlane * camera.nearClipPlane) + 0.02f;
        }
        private bool IsWorldBlocker(Collider collider)
        {
            if (collider == null || collider.transform.IsChildOf(target) || collider.transform.IsChildOf(transform)) return false;
            // Bodies retain gameplay collisions and targeting; only camera queries ignore them.
            return collider.GetComponentInParent<CharacterController>() == null &&
                   collider.GetComponentInParent<ShaiyaCombatTarget>() == null &&
                   collider.GetComponentInParent<LegacyNpcRuntimeDescriptor>() == null;
        }
        private float FindDistance(Vector3 pivot, Vector3 direction, float desired)
        {
            float radius = CollisionRadius;
            int overlapCount = Physics.OverlapSphereNonAlloc(pivot, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            Collider[] overlapValues = overlaps;
            if (overlapCount == overlaps.Length)
            {
                overlapValues = Physics.OverlapSphere(pivot, radius, collisionMask, QueryTriggerInteraction.Ignore);
                overlapCount = overlapValues.Length;
            }
            PivotObstructed = false;
            for (int i = 0; i < overlapCount; i++)
                if (IsWorldBlocker(overlapValues[i])) { PivotObstructed = true; break; }
            // A bad pivot is reported instead of trusting a SphereCast starting inside a wall.
            if (PivotObstructed) return 0.02f;
            int count = Physics.SphereCastNonAlloc(pivot, radius, direction, hits, desired, collisionMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] values = hits;
            if (count == hits.Length)
            {
                values = Physics.SphereCastAll(pivot, radius, direction, desired, collisionMask, QueryTriggerInteraction.Ignore);
                count = values.Length;
            }
            float nearest = desired;
            for (int i = 0; i < count; i++)
                if (IsWorldBlocker(values[i].collider))
                    nearest = Mathf.Min(nearest, Mathf.Max(0.02f, values[i].distance - collisionPadding));
            return nearest;
        }
        private void BeginRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view || !AvatarOccluded || target == null) return;
            RestoreAvatar();
            avatarRenderers.Clear(); originalModes.Clear();
            target.GetComponentsInChildren(true, avatarRenderers);
            foreach (Renderer renderer in avatarRenderers)
            {
                originalModes.Add(renderer.shadowCastingMode);
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            hidingForCamera = true;
        }
        private void EndRender(ScriptableRenderContext context, Camera camera)
        { if (camera == view) RestoreAvatar(); }
        private void RestoreAvatar()
        {
            if (!hidingForCamera) return;
            for (int i = 0; i < avatarRenderers.Count; i++)
                if (avatarRenderers[i] != null) avatarRenderers[i].shadowCastingMode = originalModes[i];
            hidingForCamera = false;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
