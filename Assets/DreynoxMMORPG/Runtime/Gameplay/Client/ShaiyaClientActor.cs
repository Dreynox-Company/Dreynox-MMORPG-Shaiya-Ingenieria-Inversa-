using Dreynox.Mmorpg.Gameplay.AnimationSystem;
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
        [SerializeField] private SemanticAnimationPlayer semanticAnimationPlayer;
        [SerializeField] private EquipmentAttachmentController equipmentAttachments;
        [Header("Legacy character identity")]
        [SerializeField, Range(0, 3)] private int family;
        [SerializeField, Range(0, 5)] private int job;
        [SerializeField, Range(0, 1)] private int sex;
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

        private CharacterController _controller;
        private readonly ClientMotionCore _motion = new ClientMotionCore();
        private readonly EquipmentRuleCore _equipment = new EquipmentRuleCore();
        private readonly CombatCore _combat = new CombatCore();
        private readonly FlightTransitionCore _flight = new FlightTransitionCore();
        private Vector3 _planarVelocity;
        private float _verticalVelocity;
        private int _lastAnimationGeneration;
        private int _lastAttackAnimationSerial;
        private float _flightGroundY;
        private float _flightAnchorY;
        // Air attacks retain the target captured before combat descent.
        private int? _deferredAirAttackTargetId;
        private int _deferredAirAttackDamage;
        private bool externalMovement;
        private Vector2 externalMove;
        private bool externalSprint;

        public ClientMotionCore Motion => _motion;
        public EquipmentRuleCore Equipment => _equipment;
        public CombatCore Combat => _combat;
        public FlightTransitionCore Flight => _flight;
        public string CurrentSemanticAnimation => _motion.ClipKey;
        public int AttackAnimationCount { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public int Family => family;
        public int Job => job;
        public int Sex => sex;

        public void SetExternalMovement(Vector2 value, bool sprint)
        {
            externalMovement = true;
            externalMove = Vector2.ClampMagnitude(value, 1f);
            externalSprint = sprint;
            _motion.SetFocus(true);
        }
        public void ReleaseExternalMovement() { externalMovement = false; externalMove = Vector2.zero; }

        public void TeleportGrounded(Vector3 position)
        {
            if (_flight.Airborne) throw new System.InvalidOperationException("Land before ground placement.");
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
                throw new System.ArgumentOutOfRangeException(nameof(position));
            if (_controller == null) _controller = GetComponent<CharacterController>();
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _planarVelocity = Vector3.zero; _verticalVelocity = -2f;
            _controller.enabled = wasEnabled;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraReference == null && Camera.main != null) cameraReference = Camera.main.transform;
            if (equipmentAttachments == null) equipmentAttachments = GetComponent<EquipmentAttachmentController>();
            if (semanticAnimationPlayer == null) semanticAnimationPlayer = GetComponent<SemanticAnimationPlayer>();
        }
        private void Update()
        {
            float dt = Time.deltaTime;
            ReadDesktopInput();
            _combat.Tick(dt);
            bool combatGuard = _combat.InCombatGuard || _deferredAirAttackTargetId.HasValue;
            _flight.SetMoving(_motion.HasMovement);
            _flight.SetCombatGuard(combatGuard);
            _flight.Tick(dt);
            if (_deferredAirAttackTargetId.HasValue && _flight.Phase == ClientFlightPhase.Grounded)
                ExecuteDeferredAirAttack();
            combatGuard = _combat.InCombatGuard || _deferredAirAttackTargetId.HasValue;
            _flight.SetCombatGuard(combatGuard);
            _motion.SetCombatGuard(combatGuard);
            _motion.SetFlightActive(_flight.Airborne);
            _motion.Tick(dt);
            ApplyMovement(dt);
            ApplySemanticAnimation();
        }
        private void ReadDesktopInput()
        {
            if (externalMovement) { _motion.SetMove(externalMove.x, externalMove.y, externalSprint); return; }
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _motion.SetMove(x, y, sprint);
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            if (sprint)
            {
                bool wasRequested = _flight.ManualRequested;
                if (!_flight.ToggleManualFlight()) return;
                _flightGroundY = ResolveGroundY();
                if (!wasRequested) _flightAnchorY = _flightGroundY + hoverHeight;
                return;
            }
            if (_flight.Airborne) return;
            if (IsGrounded && _motion.BeginJump())
                _verticalVelocity = Mathf.Sqrt(Mathf.Max(0.01f, jumpHeight) * -2f * gravity);
        }
        private void ApplyMovement(float dt)
        {
            Transform basis = cameraReference != null ? cameraReference : transform;
            // Unity camera yaw and recovered legacy yaw differ in sign.
            ClientCoordinateCore.ResolveUnityCameraRelative(_motion.MoveX, _motion.MoveY,
                basis.eulerAngles.y, out double worldX, out double worldZ);
            Vector3 direction = new Vector3((float)worldX, 0f, (float)worldZ);
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, direction * ResolveSpeed(), acceleration * dt);
            if (_flight.Airborne)
            {
                if (_flight.Phase == ClientFlightPhase.Hover || _flight.Phase == ClientFlightPhase.Flight)
                {
                    float verticalInput = 0f;
                    if (Input.GetKey(KeyCode.E)) verticalInput += 1f;
                    if (Input.GetKey(KeyCode.Q)) verticalInput -= 1f;
                    if (Mathf.Abs(verticalInput) > 0.01f)
                        _flightAnchorY += verticalInput * flightVerticalSpeed * dt;
                }
                float desiredY = Mathf.Lerp(_flightGroundY, _flightAnchorY, (float)_flight.FlightBlend);
                _verticalVelocity = (desiredY - transform.position.y) / Mathf.Max(0.0001f, dt);
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
        }
        private float ResolveSpeed()
        {
            if (!_motion.HasMovement) return 0f;
            if (_motion.Mounted) return _motion.Sprint ? mountRunSpeed : mountWalkSpeed;
            if (_flight.Airborne) return _motion.Sprint ? flightSprintSpeed : flightSpeed;
            return _motion.Sprint ? runSpeed : walkSpeed;
        }
        private void ApplySemanticAnimation()
        {
            if (_combat.Phase != AttackPhase.Idle)
            {
                if (_lastAttackAnimationSerial != _combat.AttackSerial)
                {
                    _lastAttackAnimationSerial = _combat.AttackSerial;
                    // The actual catalog contains attack_1, not "attack".
                    // One accepted strike restarts the clip once, never once per frame.
                    if (semanticAnimationPlayer != null && semanticAnimationPlayer.ReplaySemantic("attack_1", 0.06f))
                        AttackAnimationCount++;
                }
                _lastAnimationGeneration = -1;
                return;
            }
            if (_lastAnimationGeneration == _motion.ClipGeneration) return;
            _lastAnimationGeneration = _motion.ClipGeneration;
            if (semanticAnimationPlayer != null && semanticAnimationPlayer.PlaySemantic(_motion.ClipKey)) return;
            // Compatibility path for controller-backed imported actors.
            if (animator == null || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(_motion.ClipKey);
            if (animator.HasState(0, hash)) animator.CrossFadeInFixedTime(hash, 0.12f, 0);
        }
        public void SetCameraReference(Transform reference) { cameraReference = reference; }
        public void ConfigureLegacyIdentity(int characterFamily, int characterJob, int characterSex)
        {
            if (characterFamily < 0 || characterFamily > 3) throw new System.ArgumentOutOfRangeException(nameof(characterFamily));
            if (characterJob < 0 || characterJob > 5) throw new System.ArgumentOutOfRangeException(nameof(characterJob));
            if (characterSex < 0 || characterSex > 1) throw new System.ArgumentOutOfRangeException(nameof(characterSex));
            family = characterFamily; job = characterJob; sex = characterSex;
            ApplyEquippedWingPose();
        }
        public void SetWeaponFamily(ClientWeaponFamily family)
        {
            _equipment.EquipMainHand(family); _motion.SetWeaponFamily(family);
        }
        public bool EquipShield() { return _equipment.EquipShield(); }
        public void SetWings(bool equipped, double localYawDegrees = 0, double localHeight = 0)
        {
            if (equipped) _equipment.EquipWings(localYawDegrees, localHeight);
            else _equipment.UnequipWings();
            _motion.SetWingsEquipped(_equipment.HasWings);
            if (!equipped && _flight.Airborne) _flightGroundY = ResolveGroundY();
            _flight.SetWingsEquipped(_equipment.HasWings);
        }
        public void SetMounted(bool mounted, double seatHeight = 0)
        {
            _equipment.SetMount(mounted, seatHeight); _motion.SetMounted(mounted);
            if (mounted && _flight.Airborne) _flightGroundY = ResolveGroundY();
            _flight.SetMounted(mounted);
        }
        public bool EquipAttachment(AttachmentDefinition definition)
        {
            if (definition == null || equipmentAttachments == null || !equipmentAttachments.Equip(definition)) return false;
            switch (definition.slot)
            {
                case EquipmentSlot.MainHand: SetWeaponFamily(definition.weaponFamily); break;
                case EquipmentSlot.OffHand:
                    if (!definition.occupiesBothHands) _equipment.EquipShield();
                    break;
                case EquipmentSlot.Wings:
                    SetWings(true, definition.wingLocalYawCorrection, definition.wingLocalHeightCorrection);
                    ApplyEquippedWingPose(); break;
                case EquipmentSlot.Mount: SetMounted(true, definition.mountSeatHeight); break;
            }
            return true;
        }
        public void UnequipAttachment(EquipmentSlot slot)
        {
            if (equipmentAttachments != null) equipmentAttachments.Unequip(slot);
            switch (slot)
            {
                case EquipmentSlot.MainHand: SetWeaponFamily(ClientWeaponFamily.None); break;
                case EquipmentSlot.OffHand: _equipment.UnequipShield(); break;
                case EquipmentSlot.Wings: SetWings(false); break;
                case EquipmentSlot.Mount: SetMounted(false); break;
            }
        }
        private void ApplyEquippedWingPose()
        {
            if (equipmentAttachments == null || !equipmentAttachments.TryGetInstance(EquipmentSlot.Wings, out GameObject wing)) return;
            var applier = wing.GetComponent<WingAttachmentPoseApplier>();
            if (applier == null) applier = wing.AddComponent<WingAttachmentPoseApplier>();
            applier.Configure(wing.transform, family, job, sex);
        }
        public void SelectCombatTarget(int id, int maxHealth)
        {
            if (!_combat.Targets.ContainsKey(id)) _combat.RegisterTarget(id, maxHealth);
            _combat.SelectTarget(id);
        }
        public bool RequestAttack(int damage)
        {
            if (damage <= 0 || !_combat.SelectedTargetId.HasValue) return false;
            if (_flight.Airborne)
            {
                if (_deferredAirAttackTargetId.HasValue) return true;
                _deferredAirAttackTargetId = _combat.SelectedTargetId.Value;
                _deferredAirAttackDamage = damage;
                _flightGroundY = ResolveGroundY();
                if (_flight.BeginCombatDescent())
                {
                    _flight.SetCombatGuard(true); _motion.SetCombatGuard(true); return true;
                }
                ExecuteDeferredAirAttack();
                return _combat.InCombatGuard;
            }
            return StartLocalAttack(_combat.SelectedTargetId.Value, damage);
        }
        public void RegisterIncomingHit() { _combat.RegisterIncomingHit(); }
        private void ExecuteDeferredAirAttack()
        {
            if (!_deferredAirAttackTargetId.HasValue) return;
            int targetId = _deferredAirAttackTargetId.Value, damage = _deferredAirAttackDamage;
            _deferredAirAttackTargetId = null; _deferredAirAttackDamage = 0;
            StartLocalAttack(targetId, damage);
        }
        private bool StartLocalAttack(int targetId, int damage)
        {
            const double hitTime = 0.18; // Existing local qualification timing, not decoded server rules.
            double recovery = 0.28;
            if (semanticAnimationPlayer != null && semanticAnimationPlayer.Catalog != null &&
                semanticAnimationPlayer.Catalog.TryResolve("attack_1", out AnimationClip clip, out float speed, out string resolved))
            {
                // Preserve the authored cycle instead of truncating its last half.
                // Server attack speed and impact frame need independent native calibration.
                recovery = System.Math.Max(recovery, clip.length / System.Math.Max(0.01f, speed) - hitTime);
            }
            return _combat.RequestAttackAt(targetId, damage, 0.12, hitTime, recovery);
        }
        private float ResolveGroundY()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return transform.position.y - Mathf.Max(hoverHeight, 0.01f);
        }
        private void OnApplicationFocus(bool hasFocus) { _motion.SetFocus(hasFocus); }
    }
}
