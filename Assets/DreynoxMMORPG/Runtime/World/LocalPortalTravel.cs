using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using UnityEngine;

namespace Dreynox.Mmorpg.World
{
    /// <summary>
    /// Original SVMAP same-map travel in the local single-player client.
    /// Cross-map destinations stay unavailable until their scene and state
    /// handoff are qualified. Never substitute the current map for another one.
    /// </summary>
    public sealed class LocalPortalTravel : MonoBehaviour
    {
        [SerializeField] private NativeWorldSession session;
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private ShaiyaCombatInteraction combat;
        [SerializeField] private QuestJournalRuntime journal;
        [Tooltip("Local interaction radius, not a decoded game.exe constant.")]
        [SerializeField, Min(0.1f)] private float entryRadius = 2.5f;
        private LegacyPortalRuntime[] portals = Array.Empty<LegacyPortalRuntime>();
        private readonly HashSet<int> entered = new HashSet<int>();
        private float nextEvaluation;
        private NativeWorldHud hud;
        public int TravelSerial { get; private set; }
        public Vector3 LastAuthoredDestination { get; private set; }
        public Vector3 LastResolvedDestination { get; private set; }
        public string LastMessage { get; private set; } = "";

        public void Configure(NativeWorldSession world, ShaiyaClientActor player,
            ShaiyaCombatInteraction interaction, QuestJournalRuntime quests)
        { session = world; actor = player; combat = interaction; journal = quests; }
        private void Start()
        {
            if (session == null) session = FindFirstObjectByType<NativeWorldSession>();
            if (actor == null && session != null) actor = session.Actor;
            if (combat == null) combat = FindFirstObjectByType<ShaiyaCombatInteraction>();
            if (journal == null) journal = FindFirstObjectByType<QuestJournalRuntime>();
            hud = FindFirstObjectByType<NativeWorldHud>();
            portals = FindObjectsByType<LegacyPortalRuntime>(FindObjectsSortMode.None);
        }
        private void Update()
        {
            if (actor == null || session == null || journal == null || !journal.Ready ||
                WorldInputGate.IsBlocked || Time.unscaledTime < nextEvaluation) return;
            nextEvaluation = Time.unscaledTime + 0.1f;
            LegacyPortalRuntime nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var portal in portals)
            {
                if (portal == null || !portal.isActiveAndEnabled || portal.SourceMapId != session.MapId) continue;
                float distance = (portal.transform.position - actor.transform.position).sqrMagnitude;
                int key = portal.GetInstanceID(); // PortalId is the faction/boss rule, not a unique placement ID.
                if (distance > (entryRadius + 1f) * (entryRadius + 1f)) entered.Remove(key);
                if (distance <= entryRadius * entryRadius && !entered.Contains(key) && distance < nearestDistance)
                { nearest = portal; nearestDistance = distance; }
            }
            if (nearest == null) return;
            entered.Add(nearest.GetInstanceID());
            int family = actor.Family;
            int faction = family >= 0 && family < 4 ? (family < 2 ? 1 : 2) : -1;
            TryTravel(nearest, journal.Player.Level, faction);
            if (hud != null) hud.ShowMessage(LastMessage);
        }
        public bool TryTravel(LegacyPortalRuntime portal, int level, int portalFaction)
        {
            if (session == null || actor == null || combat == null || portal == null || !portal.isActiveAndEnabled)
                return Fail("El portal no está disponible.");
            if (WorldInputGate.IsBlocked) return Fail("Cierra la conversación antes de entrar al portal.");
            if (portal.gameObject.scene != session.gameObject.scene || actor.gameObject.scene != session.gameObject.scene ||
                portal.SourceMapId != session.MapId)
                return Fail("El portal no pertenece al mapa actual.");
            if ((portal.transform.position - actor.transform.position).sqrMagnitude > entryRadius * entryRadius)
                return Fail("Acércate al portal.");
            if (level < 1 || portalFaction < 1 || portalFaction > 2 || !portal.CanUse(level, portalFaction))
                return Fail("El nivel, la facción o el estado del portal no permiten entrar.");
            if (portal.TargetMapId != session.MapId)
                return Fail("El destino Mapa " + portal.TargetMapId + " todavía no está integrado en esta versión local.");
            if (actor.Flight.Airborne)
                return Fail("Aterriza antes de usar el portal en esta versión local.");
            Physics.SyncTransforms();
            var body = actor.GetComponent<CharacterController>();
            if (!WorldGroundPlacement.TryFind(body, portal.TargetPosition, out Vector3 destination, out string reason,
                    horizontalRadius: 1f, verticalTolerance: 8f))
                return Fail("Destino sin suelo o espacio libre: " + reason);
            // Resolve first. A denied transfer never changes position, selection,
            // quest rewards or the pending combat operation.
            combat.ClearSelection();
            actor.TeleportGrounded(destination);
            Physics.SyncTransforms();
            var camera = FindFirstObjectByType<ShaiyaThirdPersonCamera>();
            if (camera != null && camera.Target == actor.transform) camera.SetTarget(actor.transform);
            LastAuthoredDestination = portal.TargetPosition;
            LastResolvedDestination = destination;
            TravelSerial++;
            LastMessage = "Traslado completado dentro del mapa " + session.MapId + ".";
            return true;
        }
        private bool Fail(string reason) { LastMessage = reason; return false; }
    }
}
