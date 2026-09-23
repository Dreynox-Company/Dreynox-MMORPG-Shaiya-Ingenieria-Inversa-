using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Gameplay.Flight;
using Dreynox.Mmorpg.Gameplay.Locomotion;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.InputSystem
{
    public sealed class ShaiyaPlayerInput : MonoBehaviour
    {
        [SerializeField] private CharacterLocomotionMotor locomotion;
        [SerializeField] private FlightController flight;
        [SerializeField] private ShaiyaCombatController combat;
        [SerializeField] private ShaiyaThirdPersonCameraRig cameraRig;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask combatTargetMask = ~0;

        private bool _flightChordWasDown;

        public void Configure(
            CharacterLocomotionMotor locomotionMotor,
            FlightController flightController,
            ShaiyaCombatController combatController,
            ShaiyaThirdPersonCameraRig cameraController,
            Camera camera)
        {
            locomotion = locomotionMotor;
            flight = flightController;
            combat = combatController;
            cameraRig = cameraController;
            worldCamera = camera;
        }

        private void Update()
        {
            if (locomotion == null) return;

            Vector2 move = new Vector2(
                UnityEngine.Input.GetAxisRaw("Horizontal"),
                UnityEngine.Input.GetAxisRaw("Vertical"));
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) ||
                         UnityEngine.Input.GetKey(KeyCode.RightShift);

            locomotion.SetMoveInput(move, shift);
            if (cameraRig != null) locomotion.SetCameraYaw(cameraRig.MovementYawDegrees);

            bool flightChord = shift && UnityEngine.Input.GetKey(KeyCode.Space);
            if (flightChord && !_flightChordWasDown)
            {
                if (flight != null) flight.TryToggleFlight();
            }
            else if (!flightChord && UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                locomotion.RequestJump();
            }
            _flightChordWasDown = flightChord;

            if (flight != null) flight.SetMoveIntent(move.sqrMagnitude > 0.0001f);

            if (cameraRig != null && UnityEngine.Input.GetMouseButton(1))
            {
                cameraRig.AddLookDelta(new Vector2(
                    UnityEngine.Input.GetAxis("Mouse X"),
                    UnityEngine.Input.GetAxis("Mouse Y")));
            }
            if (cameraRig != null)
                cameraRig.AddZoom(UnityEngine.Input.mouseScrollDelta.y);

            if (combat != null && UnityEngine.Input.GetMouseButtonDown(0))
                TryAttackUnderCursor();
        }

        private void TryAttackUnderCursor()
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 500f, combatTargetMask, QueryTriggerInteraction.Ignore))
                return;

            CombatTarget target = hit.collider.GetComponentInParent<CombatTarget>();
            if (target != null) combat.RequestAttack(target);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || locomotion == null) return;
            locomotion.ClearInput();
            _flightChordWasDown = false;
        }
    }
}
