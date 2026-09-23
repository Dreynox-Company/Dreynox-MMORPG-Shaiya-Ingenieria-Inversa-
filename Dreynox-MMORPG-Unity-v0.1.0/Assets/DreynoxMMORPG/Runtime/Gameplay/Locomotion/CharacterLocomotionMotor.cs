using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Locomotion
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterLocomotionMotor : MonoBehaviour
    {
        [Header("Ground movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float mountedWalkSpeed = 6f;
        [SerializeField] private float mountedRunSpeed = 10f;
        [SerializeField] private float flightSpeed = 8f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float rotationSharpness = 14f;

        [Header("Vertical")]
        [SerializeField] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVelocity = -2f;

        private CharacterController _controller;
        private Vector2 _input;
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private bool _runHeld;
        private bool _jumpRequested;
        private bool _jumping;
        private bool _flying;
        private bool _mounted;
        private float _cameraYawDegrees;

        public Vector3 Velocity => _planarVelocity + Vector3.up * _verticalVelocity;
        public float PlanarSpeed => _planarVelocity.magnitude;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsMounted => _mounted;
        public bool IsFlying => _flying;
        public ShaiyaLocomotionState State { get; private set; } = ShaiyaLocomotionState.Idle;
        public string SemanticAnimation => ShaiyaLocomotionModel.SemanticAnimation(State);

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void SetMoveInput(Vector2 input, bool run)
        {
            _input = Vector2.ClampMagnitude(input, 1f);
            _runHeld = run;
        }

        public void SetCameraYaw(float yawDegrees)
        {
            _cameraYawDegrees = yawDegrees;
        }

        public void RequestJump()
        {
            _jumpRequested = true;
        }

        public void SetFlying(bool flying)
        {
            _flying = flying;
            if (flying)
            {
                _verticalVelocity = 0f;
                _jumping = false;
                _jumpRequested = false;
            }
        }

        public void SetMounted(bool mounted)
        {
            _mounted = mounted;
            if (mounted)
            {
                _jumping = false;
                _jumpRequested = false;
            }
        }

        public void ClearInput()
        {
            _input = Vector2.zero;
            _runHeld = false;
            _jumpRequested = false;
        }

        private void Update()
        {
            if (_controller == null || !_controller.enabled) return;

            bool groundedAtStart = _controller.isGrounded;
            Vector3 desiredDirection = ShaiyaLocomotionModel.ResolveCameraRelativeDirection(
                _input,
                _cameraYawDegrees);

            float speed;
            if (_flying) speed = flightSpeed;
            else if (_mounted) speed = _runHeld ? mountedRunSpeed : mountedWalkSpeed;
            else speed = _runHeld ? runSpeed : walkSpeed;

            float magnitude = Mathf.Clamp01(_input.magnitude);
            Vector3 targetVelocity = desiredDirection * (speed * magnitude);
            _planarVelocity = Vector3.MoveTowards(
                _planarVelocity,
                targetVelocity,
                acceleration * Time.deltaTime);

            if (_flying)
            {
                _verticalVelocity = 0f;
            }
            else
            {
                if (groundedAtStart && _verticalVelocity < 0f)
                {
                    _verticalVelocity = groundedVelocity;
                    _jumping = false;
                }

                if (_jumpRequested && groundedAtStart && !_mounted)
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    _jumping = true;
                }

                _verticalVelocity += gravity * Time.deltaTime;
            }

            _jumpRequested = false;

            if (_planarVelocity.sqrMagnitude > 0.04f)
            {
                Quaternion facing = Quaternion.LookRotation(_planarVelocity.normalized, Vector3.up);
                float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing, rotationT);
            }

            _controller.Move(Velocity * Time.deltaTime);

            State = ShaiyaLocomotionModel.ResolveState(
                _input,
                _runHeld,
                _controller.isGrounded,
                _jumping,
                _mounted,
                _flying);
        }
    }
}
