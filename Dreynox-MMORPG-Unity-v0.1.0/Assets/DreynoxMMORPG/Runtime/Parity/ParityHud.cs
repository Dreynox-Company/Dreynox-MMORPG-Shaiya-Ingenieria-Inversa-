using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Gameplay.Flight;
using Dreynox.Mmorpg.Gameplay.Locomotion;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    public sealed class ParityHud : MonoBehaviour
    {
        [SerializeField] private CharacterLocomotionMotor locomotion;
        [SerializeField] private FlightController flight;
        [SerializeField] private ShaiyaCombatController combat;

        public void Configure(
            CharacterLocomotionMotor locomotionMotor,
            FlightController flightController,
            ShaiyaCombatController combatController)
        {
            locomotion = locomotionMotor;
            flight = flightController;
            combat = combatController;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(18f, 18f, 410f, 170f), GUI.skin.box);
            GUILayout.Label("Dreynox MMORPG · Shaiya parity lab");
            if (locomotion != null)
            {
                GUILayout.Label("Locomotion: " + locomotion.State +
                                "  speed=" + locomotion.PlanarSpeed.ToString("0.00"));
                GUILayout.Label("Grounded: " + locomotion.IsGrounded +
                                "  mounted=" + locomotion.IsMounted);
            }
            if (flight != null)
                GUILayout.Label("Flight: " + flight.State +
                                "  requested=" + flight.ManualFlightRequested);
            if (combat != null)
                GUILayout.Label("Combat: " + combat.State +
                                "  guard=" + combat.InCombatGuard +
                                "  target=" + (combat.LockedTarget != null ? combat.LockedTarget.name : "-"));
            GUILayout.Label("WASD · Shift run · Space jump · Shift+Space flight · RMB camera · LMB attack");
            GUILayout.EndArea();
        }
    }
}
