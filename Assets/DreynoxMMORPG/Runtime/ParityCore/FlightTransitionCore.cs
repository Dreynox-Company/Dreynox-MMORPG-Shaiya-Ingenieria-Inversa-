using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum ClientFlightPhase
    {
        Grounded,
        TakingOff,
        Hover,
        Flight,
        CombatDescending,
        Landing
    }

    public sealed class FlightTransitionCore
    {
        public const double DefaultCombatDescentSeconds = 0.165;
        public const double DefaultTakeoffSeconds = 0.28;
        public const double DefaultLandingSeconds = 0.34;

        private double _transitionElapsed;
        private double _transitionDuration;
        private double _transitionStartBlend;
        private double _transitionEndBlend;
        private bool _resumeAfterCombat;

        public ClientFlightPhase Phase { get; private set; } = ClientFlightPhase.Grounded;
        public bool WingsEquipped { get; private set; }
        public bool Mounted { get; private set; }
        public bool Dead { get; private set; }
        public bool CombatGuard { get; private set; }
        public bool ManualRequested { get; private set; }
        public bool Moving { get; private set; }
        public double FlightBlend { get; private set; }
        public bool Airborne => Phase != ClientFlightPhase.Grounded;

        public bool CanFly => WingsEquipped && !Mounted && !Dead;

        public void SetWingsEquipped(bool equipped)
        {
            WingsEquipped = equipped;
            if (!equipped)
            {
                ManualRequested = false;
                _resumeAfterCombat = false;
                if (Airborne) BeginLanding(DefaultLandingSeconds, ClientFlightPhase.Landing);
            }
        }

        public void SetMounted(bool mounted)
        {
            Mounted = mounted;
            if (!mounted) return;
            ManualRequested = false;
            _resumeAfterCombat = false;
            if (Airborne) BeginLanding(DefaultLandingSeconds, ClientFlightPhase.Landing);
        }

        public void SetDead(bool dead)
        {
            Dead = dead;
            if (!dead) return;
            ManualRequested = false;
            _resumeAfterCombat = false;
            if (Airborne) BeginLanding(DefaultLandingSeconds, ClientFlightPhase.Landing);
        }

        public void SetMoving(bool moving)
        {
            Moving = moving;
            if (Phase == ClientFlightPhase.Hover && moving) Phase = ClientFlightPhase.Flight;
            else if (Phase == ClientFlightPhase.Flight && !moving) Phase = ClientFlightPhase.Hover;
        }

        public void SetCombatGuard(bool active)
        {
            CombatGuard = active;
            if (!active) TryResumeAfterCombat();
        }

        public bool ToggleManualFlight()
        {
            if (!ManualRequested)
            {
                if (!CanFly || CombatGuard) return false;
                ManualRequested = true;
                BeginTakeoff();
                return true;
            }

            ManualRequested = false;
            _resumeAfterCombat = false;
            if (Airborne) BeginLanding(DefaultLandingSeconds, ClientFlightPhase.Landing);
            return true;
        }

        public bool BeginCombatDescent()
        {
            if (!Airborne ||
                Phase == ClientFlightPhase.CombatDescending ||
                Phase == ClientFlightPhase.Landing)
                return false;

            _resumeAfterCombat = ManualRequested && CanFly;
            BeginLanding(DefaultCombatDescentSeconds, ClientFlightPhase.CombatDescending);
            return true;
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 ||
                deltaSeconds > 10 ||
                double.IsNaN(deltaSeconds) ||
                double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

            if (Phase != ClientFlightPhase.TakingOff &&
                Phase != ClientFlightPhase.CombatDescending &&
                Phase != ClientFlightPhase.Landing)
                return;

            _transitionElapsed += deltaSeconds;
            double t = _transitionDuration <= 0 ? 1 : Math.Min(1.0, _transitionElapsed / _transitionDuration);
            double smooth = t * t * (3.0 - 2.0 * t);
            FlightBlend = Lerp(_transitionStartBlend, _transitionEndBlend, smooth);

            if (t < 1.0) return;

            if (Phase == ClientFlightPhase.TakingOff)
            {
                FlightBlend = 1.0;
                Phase = Moving ? ClientFlightPhase.Flight : ClientFlightPhase.Hover;
            }
            else
            {
                FlightBlend = 0.0;
                Phase = ClientFlightPhase.Grounded;
                if (!CombatGuard) TryResumeAfterCombat();
            }
        }

        private void BeginTakeoff()
        {
            Phase = ClientFlightPhase.TakingOff;
            BeginTransition(FlightBlend, 1.0, DefaultTakeoffSeconds);
        }

        private void BeginLanding(double seconds, ClientFlightPhase phase)
        {
            Phase = phase;
            BeginTransition(FlightBlend, 0.0, seconds);
        }

        private void BeginTransition(double from, double to, double duration)
        {
            _transitionElapsed = 0.0;
            _transitionDuration = Math.Max(0.001, duration);
            _transitionStartBlend = Clamp01(from);
            _transitionEndBlend = Clamp01(to);
        }

        private void TryResumeAfterCombat()
        {
            if (!_resumeAfterCombat ||
                !ManualRequested ||
                !CanFly ||
                CombatGuard ||
                Phase != ClientFlightPhase.Grounded)
                return;

            _resumeAfterCombat = false;
            BeginTakeoff();
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }
    }
}
