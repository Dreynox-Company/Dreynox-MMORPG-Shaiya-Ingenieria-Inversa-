using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.Client;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    public sealed class ShaiyaCombatInteraction : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private Camera rayCamera;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float maxSelectionDistance = 100f;
        [SerializeField] private int[] attackDamage = { 95, 130, 180, 240 };

        private readonly Dictionary<int, ShaiyaCombatTarget> _targets = new Dictionary<int, ShaiyaCombatTarget>();
        private int _lastHitSerial;

        public void Bind(ShaiyaClientActor value, Camera cameraValue)
        {
            actor = value;
            rayCamera = cameraValue;
        }

        private void Awake()
        {
            if (rayCamera == null) rayCamera = Camera.main;
        }

        private void Update()
        {
            if (actor == null || rayCamera == null) return;
            if (Input.GetMouseButtonDown(0)) SelectUnderCursor();
            for (int i = 0; i < attackDamage.Length && i < 4; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(key)) actor.RequestAttack(attackDamage[i]);
            }
            FlushHitToScene();
        }

        private void SelectUnderCursor()
        {
            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance, targetMask, QueryTriggerInteraction.Ignore)) return;
            ShaiyaCombatTarget target = hit.collider.GetComponentInParent<ShaiyaCombatTarget>();
            if (target == null || !target.IsAlive) return;
            _targets[target.TargetId] = target;
            actor.SelectCombatTarget(target.TargetId, target.MaxHealth);
        }

        private void FlushHitToScene()
        {
            if (actor.Combat.HitSerial == _lastHitSerial) return;
            _lastHitSerial = actor.Combat.HitSerial;
            if (!actor.Combat.LastHitTargetId.HasValue) return;
            if (_targets.TryGetValue(actor.Combat.LastHitTargetId.Value, out ShaiyaCombatTarget target) && target != null)
                target.ApplyDamage(actor.Combat.LastHitDamage);
        }
    }
}
