using System;
using Dreynox.Mmorpg.Gameplay.Flight;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    public enum ShaiyaCombatState
    {
        OutOfCombat,
        Ready,
        Windup,
        Attack,
        Recover
    }

    public sealed class ShaiyaCombatController : MonoBehaviour
    {
        [Header("Recovered parity")]
        [SerializeField, Min(0.1f)] private float combatGuardSeconds = 8f;

        [Header("Tunable animation timing")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float attackSeconds = 0.35f;
        [SerializeField, Range(0f, 1f)] private float impactNormalizedTime = 0.55f;
        [SerializeField, Min(0f)] private float recoverSeconds = 0.24f;
        [SerializeField, Min(0f)] private float defaultDamage = 10f;

        [SerializeField] private FlightController flight;

        private CombatTarget _lockedTarget;
        private CombatTarget _queuedTarget;
        private string _queuedAttack;
        private float _stateElapsed;
        private float _guardUntil;
        private bool _impactApplied;

        public ShaiyaCombatState State { get; private set; } = ShaiyaCombatState.OutOfCombat;
        public CombatTarget LockedTarget => _lockedTarget;
        public bool InCombatGuard => Time.time < _guardUntil || State == ShaiyaCombatState.Windup ||
                                     State == ShaiyaCombatState.Attack || State == ShaiyaCombatState.Recover;
        public string CurrentAttackSemantic { get; private set; } = string.Empty;

        public event Action<ShaiyaCombatState> StateChanged;
        public event Action<CombatTarget> TargetChanged;
        public event Action<string, CombatTarget> AttackStarted;
        public event Action<string, CombatTarget> AttackImpacted;

        public void Configure(FlightController flightController)
        {
            flight = flightController;
        }

        public bool RequestAttack(CombatTarget target, string semanticAttack = "PLAYER_ATTACK")
        {
            if (target == null || !target.IsAlive) return false;

            _guardUntil = Time.time + combatGuardSeconds;

            // Single-slot input buffer recovered from the previous client:
            // repeated clicks replace the pending attack instead of creating an
            // unbounded queue or cancelling the current hit.
            _queuedTarget = target;
            _queuedAttack = string.IsNullOrWhiteSpace(semanticAttack) ? "PLAYER_ATTACK" : semanticAttack;

            if (flight != null && flight.IsAirborne)
            {
                flight.BeginCombatDescent(ResolveGroundY());
                return true;
            }

            if (State != ShaiyaCombatState.Windup &&
                State != ShaiyaCombatState.Attack &&
                State != ShaiyaCombatState.Recover)
            {
                BeginQueuedAttack();
            }

            return true;
        }

        public void RegisterIncomingHit()
        {
            _guardUntil = Time.time + combatGuardSeconds;
            if (State == ShaiyaCombatState.OutOfCombat) ChangeState(ShaiyaCombatState.Ready);
        }

        private void Update()
        {
            if (flight != null) flight.InCombatGuard = InCombatGuard;

            if (flight != null && flight.State == FlightState.CombatDescending)
                return;

            if (State == ShaiyaCombatState.OutOfCombat || State == ShaiyaCombatState.Ready)
            {
                if (_queuedTarget != null)
                {
                    BeginQueuedAttack();
                    return;
                }

                if (!InCombatGuard && State != ShaiyaCombatState.OutOfCombat)
                {
                    ClearTarget();
                    ChangeState(ShaiyaCombatState.OutOfCombat);
                    if (flight != null) flight.EndCombatGuard();
                }
                else if (InCombatGuard && State == ShaiyaCombatState.OutOfCombat)
                {
                    ChangeState(ShaiyaCombatState.Ready);
                }
                return;
            }

            _stateElapsed += Time.deltaTime;

            if (State == ShaiyaCombatState.Windup && _stateElapsed >= windupSeconds)
            {
                _impactApplied = false;
                ChangeState(ShaiyaCombatState.Attack);
                AttackStarted?.Invoke(CurrentAttackSemantic, _lockedTarget);
                return;
            }

            if (State == ShaiyaCombatState.Attack)
            {
                float impactTime = attackSeconds * impactNormalizedTime;
                if (!_impactApplied && _stateElapsed >= impactTime)
                {
                    _impactApplied = true;
                    if (_lockedTarget != null && _lockedTarget.IsAlive)
                        _lockedTarget.ApplyDamage(defaultDamage);
                    AttackImpacted?.Invoke(CurrentAttackSemantic, _lockedTarget);
                }

                if (_stateElapsed >= attackSeconds)
                {
                    ChangeState(ShaiyaCombatState.Recover);
                    return;
                }
            }

            if (State == ShaiyaCombatState.Recover && _stateElapsed >= recoverSeconds)
            {
                if (_queuedTarget != null) BeginQueuedAttack();
                else ChangeState(InCombatGuard ? ShaiyaCombatState.Ready : ShaiyaCombatState.OutOfCombat);
            }
        }

        private void BeginQueuedAttack()
        {
            if (_queuedTarget == null || !_queuedTarget.IsAlive)
            {
                _queuedTarget = null;
                _queuedAttack = null;
                return;
            }

            SetLockedTarget(_queuedTarget);
            CurrentAttackSemantic = _queuedAttack;
            _queuedTarget = null;
            _queuedAttack = null;
            ChangeState(ShaiyaCombatState.Windup);
        }

        private void SetLockedTarget(CombatTarget target)
        {
            if (_lockedTarget == target) return;
            _lockedTarget = target;
            TargetChanged?.Invoke(_lockedTarget);
        }

        private void ClearTarget()
        {
            if (_lockedTarget == null) return;
            _lockedTarget = null;
            TargetChanged?.Invoke(null);
        }

        private void ChangeState(ShaiyaCombatState next)
        {
            if (State == next)
            {
                _stateElapsed = 0f;
                return;
            }

            State = next;
            _stateElapsed = 0f;
            StateChanged?.Invoke(State);
        }

        private float ResolveGroundY()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return transform.position.y;
        }
    }
}
