using Dreynox.Mmorpg.Gameplay.Equipment;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Flight
{
    public enum FlightState { Grounded, TakingOff, Flying, CombatDescending, Landing }

    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private EquipmentAttachmentController equipment;
        [SerializeField] private float hoverHeight = 0.38f;
        [SerializeField] private float transitionSpeed = 5f;
        [SerializeField] private float combatDescentSeconds = 0.165f;
        public FlightState State { get; private set; } = FlightState.Grounded;
        public bool IsMounted { get; set; }
        public bool IsDead { get; set; }
        public bool InCombatGuard { get; set; }
        private float _transitionStartY;
        private float _transitionTargetY;
        private float _transitionElapsed;
        private float _transitionDuration;
        private bool _resumeAfterCombat;

        public bool CanFly => equipment != null && equipment.Has(EquipmentSlot.Wings) && !IsMounted && !IsDead;

        public bool TryToggleFlight()
        {
            if (State == FlightState.Grounded || State == FlightState.Landing)
            {
                if (!CanFly) return false;
                BeginVerticalTransition(FlightState.TakingOff, transform.position.y + hoverHeight, 1f / Mathf.Max(0.1f, transitionSpeed));
                return true;
            }
            if (State == FlightState.Flying || State == FlightState.TakingOff)
            {
                BeginVerticalTransition(FlightState.Landing, transform.position.y - hoverHeight, 1f / Mathf.Max(0.1f, transitionSpeed));
                return true;
            }
            return false;
        }

        public void BeginCombatDescent(float groundY)
        {
            if (State != FlightState.Flying && State != FlightState.TakingOff) return;
            _resumeAfterCombat = CanFly;
            BeginVerticalTransition(FlightState.CombatDescending, groundY, combatDescentSeconds);
        }

        public void EndCombatGuard()
        {
            InCombatGuard = false;
            if (_resumeAfterCombat && CanFly && State == FlightState.Grounded)
            {
                _resumeAfterCombat = false;
                TryToggleFlight();
            }
        }

        private void BeginVerticalTransition(FlightState state, float targetY, float duration)
        {
            State = state;
            _transitionStartY = transform.position.y;
            _transitionTargetY = targetY;
            _transitionElapsed = 0f;
            _transitionDuration = Mathf.Max(0.01f, duration);
        }

        private void Update()
        {
            if (State != FlightState.TakingOff && State != FlightState.Landing && State != FlightState.CombatDescending) return;
            _transitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
            float eased = t * t * (3f - 2f * t);
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(_transitionStartY, _transitionTargetY, eased);
            transform.position = p;
            if (t < 1f) return;
            if (State == FlightState.TakingOff) State = FlightState.Flying;
            else State = FlightState.Grounded;
        }
    }
}
