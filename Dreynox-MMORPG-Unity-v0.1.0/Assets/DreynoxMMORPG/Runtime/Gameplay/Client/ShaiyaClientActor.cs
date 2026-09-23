using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Client
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ShaiyaClientActor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraReference;
        [SerializeField] private Animator animator;
        [SerializeField] private EquipmentAttachmentController equipmentAttachments;

        [Header("Ground locomotion")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float runSpeed = 7.5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float rotationSharpness = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 1.25f;

        [Header("Mount / flight")]
        [SerializeField] private float mountWalkSpeed = 6.5f;
        [SerializeField] private float mountRunSpeed = 10.5f;
        [SerializeField] private float flightSpeed = 8f;
        [SerializeField] private float flightSprintSpeed = 12f;
        [SerializeField] private float flightVerticalSpeed = 5f;
        [SerializeField] private float hoverHeight = 0.38f;
        [SerializeField] private float flightTransitionSpeed = 4.5f;

        private CharacterController _controller;
        private readonly ClientMotionCore _motion = new ClientMotionCore();
        private readonly EquipmentRuleCore _equipment = new EquipmentRuleCore();
        private readonly CombatCore _combat = new CombatCore();
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private int _lastAnimationGeneration;
        private float _flightAnchorY;
        private bool _wasAirborne;

        public ClientMotionCore Motion => _motion;
        public EquipmentRuleCore Equipment => _equipment;
        public CombatCore Combat => _combat;
        public string CurrentSemanticAnimation => _motion.ClipKey;
        public bool IsGrounded => _controller != null && _controller.isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraReference == null && Camera.main != null) cameraReference = Camera.main.transform;
        }

        private void Update()
        {
            ReadDesktopInput();
            _combat.Tick(Time.deltaTime);
            _motion.SetCombatGuard(_combat.InCombatGuard);
            _motion.Tick(Time.deltaTime);
            ApplyMovement(Time.deltaTime);
            ApplySemanticAnimation();
        }

        private void ReadDesktopInput()
        {
            float x = 0f;
            float y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _motion.SetMove(x, y, sprint);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (sprint)
                {
                    if (_motion.ToggleFlight() && _motion.IsAirborne)
                        _flightAnchorY = transform.position.y + hoverHeight;
                }
                else if (_motion.BeginJump() && IsGrounded)
                {
                    _verticalVelocity = Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * -2f * gravity);
                }
            }
        }

        private void ApplyMovement(float dt)
        {
            bool airborne = _motion.IsAirborne;
            Transform basis = cameraReference != null ? cameraReference : transform;
            ClientCoordinateCore.ResolveCameraRelative(
                _motion.MoveX,
                _motion.MoveY,
                basis.eulerAngles.y,
                out double worldX,
                out double worldZ);
            Vector3 desiredDirection = new Vector3((float)worldX, 0f, (float)worldZ);

            float targetSpeed = ResolveSpeed();
            Vector3 targetPlanar = desiredDirection * targetSpeed;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetPlanar, acceleration * dt);

            if (airborne)
            {
                if (!_wasAirborne) _flightAnchorY = transform.position.y + hoverHeight;
                float verticalInput = 0f;
                if (Input.GetKey(KeyCode.E)) verticalInput += 1f;
                if (Input.GetKey(KeyCode.Q)) verticalInput -= 1f;
                if (Mathf.Abs(verticalInput) > 0.01f)
                    _flightAnchorY += verticalInput * flightVerticalSpeed * dt;
                float verticalDelta = (_flightAnchorY - transform.position.y) * flightTransitionSpeed;
                _verticalVelocity = Mathf.Clamp(verticalDelta, -flightVerticalSpeed, flightVerticalSpeed);
            }
            else
            {
                if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                else _verticalVelocity += gravity * dt;
            }

            if (_planarVelocity.sqrMagnitude > 0.04f)
            {
                Quaternion facing = Quaternion.LookRotation(_planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, facing, 1f - Mathf.Exp(-rotationSharpness * dt));
            }

            _controller.Move((_planarVelocity + Vector3.up * _verticalVelocity) * dt);
            _wasAirborne = airborne;
        }

        private float ResolveSpeed()
        {
            if (!_motion.HasMovement) return 0f;
            if (_motion.Mounted) return _motion.Sprint ? mountRunSpeed : mountWalkSpeed;
            if (_motion.IsAirborne) return _motion.Sprint ? flightSprintSpeed : flightSpeed;
            return _motion.Sprint ? runSpeed : walkSpeed;
        }

        private void ApplySemanticAnimation()
        {
            if (_lastAnimationGeneration == _motion.ClipGeneration) return;
            _lastAnimationGeneration = _motion.ClipGeneration;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(_motion.ClipKey);
            if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, 0.12f, 0);
        }

        public void SetCameraReference(Transform reference) => cameraReference = reference;

        public void SetWeaponFamily(ClientWeaponFamily family)
        {
            _equipment.EquipMainHand(family);
            _motion.SetWeaponFamily(family);
        }

        public bool EquipShield() => _equipment.EquipShield();

        public void SetWings(bool equipped, double localYawDegrees = 0, double localHeight = 0)
        {
            if (equipped) _equipment.EquipWings(localYawDegrees, localHeight); else _equipment.UnequipWings();
            _motion.SetWingsEquipped(_equipment.HasWings);
        }

        public void SetMounted(bool mounted, double seatHeight = 0)
        {
            _equipment.SetMount(mounted, seatHeight);
            _motion.SetMounted(mounted);
        }

        public bool EquipAttachment(AttachmentDefinition definition)
        {
            return equipmentAttachments != null && equipmentAttachments.Equip(definition);
        }

        public void SelectCombatTarget(int id, int maxHealth)
        {
            if (!_combat.Targets.ContainsKey(id)) _combat.RegisterTarget(id, maxHealth);
            _combat.SelectTarget(id);
        }

        public bool RequestAttack(int damage)
        {
            return _combat.RequestAttack(damage);
        }

        public void RegisterIncomingHit() => _combat.RegisterIncomingHit();

        private void OnApplicationFocus(bool hasFocus)
        {
            _motion.SetFocus(hasFocus);
        }
    }
}
