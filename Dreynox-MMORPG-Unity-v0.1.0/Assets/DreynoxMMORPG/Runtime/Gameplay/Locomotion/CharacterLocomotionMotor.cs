using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Locomotion
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterLocomotionMotor : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVelocity = -2f;
        [SerializeField] private Transform cameraReference;
        private CharacterController _controller;
        private Vector2 _input;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private bool _run;

        public Vector3 Velocity => _planarVelocity + Vector3.up * _verticalVelocity;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        private void Awake() => _controller = GetComponent<CharacterController>();
        public void SetMoveInput(Vector2 input, bool run) { _input = Vector2.ClampMagnitude(input, 1f); _run = run; }

        private void Update()
        {
            Transform basis = cameraReference != null ? cameraReference : transform;
            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
            Vector3 desiredDir = (forward * _input.y + right * _input.x).normalized;
            float targetSpeed = (_run ? runSpeed : walkSpeed) * Mathf.Clamp01(_input.magnitude);
            Vector3 target = desiredDir * targetSpeed;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, target, acceleration * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = groundedVelocity;
            else _verticalVelocity += gravity * Time.deltaTime;

            if (_planarVelocity.sqrMagnitude > 0.04f)
            {
                Quaternion facing = Quaternion.LookRotation(_planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing, 14f * Time.deltaTime);
            }
            _controller.Move(Velocity * Time.deltaTime);
        }
    }
}
