using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Gameplay.Flight;
using Dreynox.Mmorpg.Gameplay.Locomotion;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.AnimationSystem
{
    [RequireComponent(typeof(Animator))]
    public sealed class ShaiyaAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private CharacterLocomotionMotor locomotion;
        [SerializeField] private FlightController flight;
        [SerializeField] private ShaiyaCombatController combat;

        private Animator _animator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int FlyingHash = Animator.StringToHash("Flying");
        private static readonly int MountedHash = Animator.StringToHash("Mounted");
        private static readonly int CombatHash = Animator.StringToHash("Combat");
        private static readonly int StateHash = Animator.StringToHash("LocomotionState");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        public void Configure(
            CharacterLocomotionMotor locomotionMotor,
            FlightController flightController,
            ShaiyaCombatController combatController)
        {
            locomotion = locomotionMotor;
            flight = flightController;
            combat = combatController;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (combat != null) combat.AttackStarted += OnAttackStarted;
        }

        private void OnDestroy()
        {
            if (combat != null) combat.AttackStarted -= OnAttackStarted;
        }

        private void Update()
        {
            if (_animator == null || locomotion == null) return;
            _animator.SetFloat(SpeedHash, locomotion.PlanarSpeed, 0.08f, Time.deltaTime);
            _animator.SetBool(GroundedHash, locomotion.IsGrounded);
            _animator.SetBool(FlyingHash, flight != null && flight.IsAirborne);
            _animator.SetBool(MountedHash, locomotion.IsMounted);
            _animator.SetBool(CombatHash, combat != null && combat.InCombatGuard);
            _animator.SetInteger(StateHash, (int)locomotion.State);
        }

        private void OnAttackStarted(string semantic, CombatTarget target)
        {
            if (_animator != null) _animator.SetTrigger(AttackHash);
        }
    }
}
