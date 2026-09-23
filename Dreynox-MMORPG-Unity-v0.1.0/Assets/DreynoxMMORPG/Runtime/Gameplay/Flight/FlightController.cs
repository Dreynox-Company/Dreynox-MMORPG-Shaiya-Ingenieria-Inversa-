using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.Gameplay.Locomotion;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Flight
{
    public enum FlightState
    {
        Grounded,
        TakingOff,
        Hovering,
        Flying,
        CombatDescending,
        Landing
    }

    public sealed class FlightController : MonoBehaviour
    {
        [SerializeField] private EquipmentAttachmentController equipment;
        [SerializeField] private CharacterLocomotionMotor locomotion;

        [Header("Recovered behavior")]
        [SerializeField] private float hoverHeight = 0.38f;
        [SerializeField] private float combatDescentSeconds = 0.165f;

        [Header("Transitions")]
        [SerializeField] private float takeoffSeconds = 0.28f;
        [SerializeField] private float normalLandingSeconds = 0.34f;

        private float _transitionStartY;
        private float _transitionTargetY;
        private float _transitionElapsed;
        private float _transitionDuration;
        private bool _resumeAfterCombat;
        private bool _movingIntent;

        public FlightState State { get; private set; } = FlightState.Grounded;
        public bool IsMounted { get; set; }
        public bool IsDead { get; set; }
        public bool InCombatGuard { get; set; }
        public bool ManualFlightRequested { get; private set; }
        public bool IsAirborne => State == FlightState.TakingOff ||
                                  State == FlightState.Hovering ||
                                  State == FlightState.Flying ||
                                  State == FlightState.CombatDescending ||
                                  State == FlightState.Landing;

        public bool CanFly =>
            equipment != null &&
            equipment.Has(EquipmentSlot.Wings) &&
            !IsMounted &&
            !IsDead;

        public string SemanticAnimation =>
            State == FlightState.Flying ? "PLAYER_FLY" :
            State == FlightState.Hovering || State == FlightState.TakingOff ? "PLAYER_STOP_FLY" :
            string.Empty;

        public void Configure(
            EquipmentAttachmentController attachmentController,
            CharacterLocomotionMotor locomotionMotor)
        {
            equipment = attachmentController;
            locomotion = locomotionMotor;
        }

        public bool TryToggleFlight()
        {
            if (!ManualFlightRequested)
            {
                if (!CanFly || InCombatGuard) return false;
                ManualFlightRequested = true;
                BeginTakeoff();
                return true;
            }

            ManualFlightRequested = false;
            _resumeAfterCombat = false;
            if (IsAirborne) BeginLanding(ResolveGroundY(), normalLandingSeconds, FlightState.Landing);
            return true;
        }

        public void SetMoveIntent(bool moving)
        {
            _movingIntent = moving;
            if (State == FlightState.Hovering && moving) State = FlightState.Flying;
            else if (State == FlightState.Flying && !moving) State = FlightState.Hovering;
        }

        public void BeginCombatDescent(float groundY)
        {
            if (!IsAirborne || State == FlightState.CombatDescending) return;

            _resumeAfterCombat = ManualFlightRequested && CanFly;
            BeginLanding(groundY, combatDescentSeconds, FlightState.CombatDescending);
        }

        public void EndCombatGuard()
        {
            InCombatGuard = false;
            if (_resumeAfterCombat && ManualFlightRequested && CanFly && State == FlightState.Grounded)
            {
                _resumeAfterCombat = false;
                BeginTakeoff();
            }
        }

        private void Update()
        {
            if (!CanFly && IsAirborne && State != FlightState.CombatDescending && State != FlightState.Landing)
            {
                ManualFlightRequested = false;
                _resumeAfterCombat = false;
                BeginLanding(ResolveGroundY(), normalLandingSeconds, FlightState.Landing);
            }

            if (State != FlightState.TakingOff &&
                State != FlightState.Landing &&
                State != FlightState.CombatDescending)
            {
                if (locomotion != null)
                    locomotion.SetFlying(State == FlightState.Hovering || State == FlightState.Flying);
                return;
            }

            _transitionElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
            float eased = t * t * (3f - 2f * t);
            float targetY = Mathf.Lerp(_transitionStartY, _transitionTargetY, eased);
            ApplyAbsoluteY(targetY);

            if (t < 1f) return;

            if (State == FlightState.TakingOff)
            {
                State = _movingIntent ? FlightState.Flying : FlightState.Hovering;
                if (locomotion != null) locomotion.SetFlying(true);
            }
            else
            {
                State = FlightState.Grounded;
                if (locomotion != null) locomotion.SetFlying(false);
            }
        }

        private void BeginTakeoff()
        {
            if (!CanFly || InCombatGuard) return;
            if (locomotion != null) locomotion.SetFlying(true);
            BeginTransition(
                FlightState.TakingOff,
                transform.position.y + hoverHeight,
                takeoffSeconds);
        }

        private void BeginLanding(float targetGroundY, float seconds, FlightState transitionState)
        {
            if (locomotion != null) locomotion.SetFlying(true);
            BeginTransition(transitionState, targetGroundY, seconds);
        }

        private void BeginTransition(FlightState state, float targetY, float duration)
        {
            State = state;
            _transitionStartY = transform.position.y;
            _transitionTargetY = targetY;
            _transitionElapsed = 0f;
            _transitionDuration = Mathf.Max(0.01f, duration);
        }

        private void ApplyAbsoluteY(float targetY)
        {
            float delta = targetY - transform.position.y;
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null && controller.enabled)
                controller.Move(Vector3.up * delta);
            else
            {
                Vector3 p = transform.position;
                p.y = targetY;
                transform.position = p;
            }
        }

        private float ResolveGroundY()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            if (Physics.Raycast(origin, Vector3.down, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return transform.position.y - hoverHeight;
        }
    }
}
