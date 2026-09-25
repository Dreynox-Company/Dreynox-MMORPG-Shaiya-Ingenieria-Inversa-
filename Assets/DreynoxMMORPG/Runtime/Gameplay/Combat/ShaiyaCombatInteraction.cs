using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.Client;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    /// <summary>Local combat qualification adapter. Damage/timing defaults are not server-authoritative parity.</summary>
    public sealed class ShaiyaCombatInteraction : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private Camera rayCamera;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private float maxSelectionDistance = 100f;
        [SerializeField, Min(0.1f)] private float meleeReach = 3.5f;
        [SerializeField] private int[] attackDamage = { 95, 130, 180, 240 };
        private readonly Dictionary<int, TargetBinding> targets = new Dictionary<int, TargetBinding>();
        private readonly RaycastHit[] lineHits = new RaycastHit[32];
        private int lastHitSerial;
        public ShaiyaCombatTarget SelectedTarget { get; private set; }
        public string Feedback { get; private set; } = string.Empty;
        public event Action<ShaiyaCombatTarget> TargetSelected;
        public event Action<ShaiyaCombatTarget, int> HitApplied;

        public void Bind(ShaiyaClientActor value, Camera cameraValue)
        {
            if (actor != null) actor.Combat.ImpactValidator = null;
            actor = value; rayCamera = cameraValue;
            if (actor != null) actor.Combat.ImpactValidator = CanImpact;
        }
        private void Awake()
        {
            if (rayCamera == null) rayCamera = Camera.main;
            if (actor != null) actor.Combat.ImpactValidator = CanImpact;
        }
        private void OnDestroy() { if (actor != null) actor.Combat.ImpactValidator = null; }
        private void Update()
        {
            if (actor == null || rayCamera == null) return;
            bool overUi = Dreynox.Mmorpg.Interaction.WorldInputGate.IsBlocked ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            if (!overUi && Input.GetMouseButtonDown(0)) SelectUnderCursor();
            if (!overUi)
                for (int i = 0; i < attackDamage.Length && i < 4; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) TryAttackSelected(attackDamage[i]);
            FlushHitToScene();
        }

        public bool Select(ShaiyaCombatTarget target)
        {
            if (actor == null || target == null || !target.gameObject.activeInHierarchy || !target.IsAlive) return false;
            TargetBinding previous;
            bool identityChanged = !targets.TryGetValue(target.TargetId, out previous) ||
                previous.Target != target || previous.Generation != target.Generation;
            if (identityChanged)
            {
                actor.Combat.UnregisterTarget(target.TargetId);
                actor.Combat.SynchronizeTarget(target.TargetId, target.MaxHealth, target.Health);
                targets[target.TargetId] = new TargetBinding(target);
            }
            actor.SelectCombatTarget(target.TargetId, target.MaxHealth);
            SelectedTarget = target;
            Feedback = target.name;
            TargetSelected?.Invoke(target);
            return true;
        }

        public bool TryAttackSelected(int damage)
        {
            if (SelectedTarget == null || !CanImpact(SelectedTarget.TargetId))
            { Feedback = "Objetivo fuera de alcance, obstruido o no disponible."; return false; }
            bool accepted = actor.RequestAttack(damage);
            if (accepted) Feedback = "Atacando " + SelectedTarget.name;
            return accepted;
        }

        public bool CanImpact(int targetId)
        {
            if (actor == null) return false;
            TargetBinding binding;
            if (!targets.TryGetValue(targetId, out binding)) return false;
            ShaiyaCombatTarget target = binding.Target;
            if (target == null || target.Generation != binding.Generation || target.TargetId != targetId ||
                !target.gameObject.activeInHierarchy || !target.IsAlive) return false;
            Vector3 origin = actor.transform.position + Vector3.up;
            Collider body = target.GetComponentInChildren<Collider>();
            Vector3 point = body != null ? body.bounds.center : target.transform.position + Vector3.up;
            Vector3 contact = body != null ? body.ClosestPoint(origin) : point;
            if ((contact - origin).sqrMagnitude > meleeReach * meleeReach) return false;
            Vector3 line = point - origin;
            float length = line.magnitude;
            if (length <= 0.001f) return true;
            int count = Physics.RaycastNonAlloc(origin, line / length, lineHits, length, targetMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] values = lineHits;
            if (count == lineHits.Length)
            { values = Physics.RaycastAll(origin, line / length, length, targetMask, QueryTriggerInteraction.Ignore); count = values.Length; }
            for (int i = 0; i < count; i++)
            {
                Transform hit = values[i].collider.transform;
                if (hit.IsChildOf(actor.transform) || hit.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        private void SelectUnderCursor()
        {
            Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance, targetMask, QueryTriggerInteraction.Ignore))
                Select(hit.collider.GetComponentInParent<ShaiyaCombatTarget>());
        }

        public void FlushHitToScene()
        {
            if (actor == null || actor.Combat.HitSerial == lastHitSerial) return;
            lastHitSerial = actor.Combat.HitSerial;
            if (!actor.Combat.LastHitTargetId.HasValue) return;
            TargetBinding binding;
            if (!targets.TryGetValue(actor.Combat.LastHitTargetId.Value, out binding)) return;
            ShaiyaCombatTarget target = binding.Target;
            if (target == null || target.Generation != binding.Generation || target.TargetId != actor.Combat.LastHitTargetId.Value) return;
            int applied = target.ApplyDamage(actor.Combat.LastHitDamage);
            Feedback = target.IsAlive ? target.name + " HP " + target.Health + "/" + target.MaxHealth : target.name + " derrotado";
            HitApplied?.Invoke(target, applied);
        }
        private readonly struct TargetBinding
        {
            public readonly ShaiyaCombatTarget Target;
            public readonly int Generation;
            public TargetBinding(ShaiyaCombatTarget target) { Target = target; Generation = target.Generation; }
        }
    }
}
