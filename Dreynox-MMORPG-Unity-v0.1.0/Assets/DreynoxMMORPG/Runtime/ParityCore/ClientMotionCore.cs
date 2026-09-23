using System;

namespace Dreynox.Mmorpg.ParityCore
{
    public enum ClientMotionState
    {
        Idle,
        Walk,
        Run,
        Jump,
        MountIdle,
        MountWalk,
        MountRun,
        Hover,
        Flight,
        CombatIdle,
        CombatMove,
        Dead
    }

    public enum ClientWeaponFamily
    {
        None,
        OneHand,
        TwoHand,
        Spear,
        Bow,
        Staff,
        Dagger
    }

    public sealed class ClientMotionCore
    {
        private const double DefaultJumpSeconds = 0.72;
        private double _jumpRemaining;
        private double _moveX;
        private double _moveY;
        private bool _sprint;
        private bool _hasFocus = true;
        private bool _mounted;
        private bool _wingsEquipped;
        private bool _flightRequested;
        private bool _combatGuard;
        private bool _dead;
        private ClientWeaponFamily _weaponFamily;
        private string _clipKey = "idle";
        private int _clipGeneration = 1;
        private ClientMotionState _state = ClientMotionState.Idle;

        public ClientMotionState State => _state;
        public string ClipKey => _clipKey;
        public int ClipGeneration => _clipGeneration;
        public double MoveX => _moveX;
        public double MoveY => _moveY;
        public bool Sprint => _sprint;
        public bool Mounted => _mounted;
        public bool WingsEquipped => _wingsEquipped;
        public bool FlightRequested => _flightRequested;
        public bool CombatGuard => _combatGuard;
        public bool Dead => _dead;
        public bool HasMovement => Math.Abs(_moveX) > 0.0001 || Math.Abs(_moveY) > 0.0001;
        public bool IsAirborne => _flightRequested && _wingsEquipped && !_mounted && !_dead;

        public void SetMove(double x, double y, bool sprint)
        {
            _moveX = Clamp(x, -1.0, 1.0);
            _moveY = Clamp(y, -1.0, 1.0);
            _sprint = sprint;
            Evaluate();
        }

        public void SetWeaponFamily(ClientWeaponFamily family)
        {
            if (_weaponFamily == family) return;
            _weaponFamily = family;
            Evaluate();
        }

        public void SetCombatGuard(bool active)
        {
            if (_combatGuard == active) return;
            _combatGuard = active;
            Evaluate();
        }

        public void SetMounted(bool mounted)
        {
            if (_mounted == mounted) return;
            _mounted = mounted;
            if (mounted) _flightRequested = false;
            Evaluate();
        }

        public void SetWingsEquipped(bool equipped)
        {
            if (_wingsEquipped == equipped) return;
            _wingsEquipped = equipped;
            if (!equipped) _flightRequested = false;
            Evaluate();
        }

        public bool ToggleFlight()
        {
            if (_dead || _mounted || !_wingsEquipped || _jumpRemaining > 0) return false;
            _flightRequested = !_flightRequested;
            Evaluate();
            return true;
        }

        public bool BeginJump(double durationSeconds = DefaultJumpSeconds)
        {
            if (_dead || _mounted || IsAirborne || _jumpRemaining > 0) return false;
            _jumpRemaining = Math.Max(0.05, durationSeconds);
            Evaluate();
            return true;
        }

        public void SetDead(bool dead)
        {
            _dead = dead;
            if (dead)
            {
                _flightRequested = false;
                _jumpRemaining = 0;
                _moveX = 0;
                _moveY = 0;
                _sprint = false;
            }
            Evaluate();
        }

        public void SetFocus(bool focused)
        {
            _hasFocus = focused;
            if (!focused)
            {
                _moveX = 0;
                _moveY = 0;
                _sprint = false;
            }
            Evaluate();
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds < 0 || deltaSeconds > 10 || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (_jumpRemaining > 0)
            {
                _jumpRemaining = Math.Max(0, _jumpRemaining - deltaSeconds);
                Evaluate();
            }
        }

        public double SpeedMultiplier
        {
            get
            {
                if (_dead || !_hasFocus || !HasMovement) return 0;
                if (_mounted) return _sprint ? 1.0 : 0.62;
                if (IsAirborne) return _sprint ? 1.0 : 0.72;
                return _sprint ? 1.0 : 0.60;
            }
        }

        private void Evaluate()
        {
            ClientMotionState next;
            string clip;

            if (_dead)
            {
                next = ClientMotionState.Dead;
                clip = "dead";
            }
            else if (_jumpRemaining > 0)
            {
                next = ClientMotionState.Jump;
                clip = "jump";
            }
            else if (_mounted)
            {
                if (!HasMovement)
                {
                    next = ClientMotionState.MountIdle;
                    clip = "mount_idle";
                }
                else if (_sprint)
                {
                    next = ClientMotionState.MountRun;
                    clip = "mount_run";
                }
                else
                {
                    next = ClientMotionState.MountWalk;
                    clip = "mount_walk";
                }
            }
            else if (IsAirborne)
            {
                if (HasMovement)
                {
                    next = ClientMotionState.Flight;
                    clip = "flight";
                }
                else
                {
                    next = ClientMotionState.Hover;
                    clip = "hover";
                }
            }
            else if (_combatGuard)
            {
                if (HasMovement)
                {
                    next = ClientMotionState.CombatMove;
                    clip = CombatMoveClip();
                }
                else
                {
                    next = ClientMotionState.CombatIdle;
                    clip = "combat_idle";
                }
            }
            else if (!HasMovement || !_hasFocus)
            {
                next = ClientMotionState.Idle;
                clip = "idle";
            }
            else if (_sprint)
            {
                next = ClientMotionState.Run;
                clip = RunClip();
            }
            else
            {
                next = ClientMotionState.Walk;
                clip = WalkClip();
            }

            _state = next;
            if (string.Equals(_clipKey, clip, StringComparison.Ordinal)) return;
            _clipKey = clip;
            _clipGeneration++;
        }

        private string WalkClip()
        {
            switch (_weaponFamily)
            {
                case ClientWeaponFamily.Spear: return "walk_spear";
                case ClientWeaponFamily.Bow: return "walk_bow";
                case ClientWeaponFamily.Staff: return "walk_staff";
                default: return "walk";
            }
        }

        private string RunClip()
        {
            switch (_weaponFamily)
            {
                case ClientWeaponFamily.Spear: return "run_spear";
                case ClientWeaponFamily.Bow: return "run_bow";
                case ClientWeaponFamily.Staff: return "run_staff";
                default: return "run";
            }
        }

        private string CombatMoveClip()
        {
            switch (_weaponFamily)
            {
                case ClientWeaponFamily.Spear: return "combat_move_spear";
                case ClientWeaponFamily.Bow: return "combat_move_bow";
                case ClientWeaponFamily.Staff: return "combat_move_staff";
                default: return "combat_move";
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
