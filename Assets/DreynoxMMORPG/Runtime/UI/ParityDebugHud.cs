using Dreynox.Mmorpg.Gameplay.Client;
using UnityEngine;

namespace Dreynox.Mmorpg.UI
{
    public sealed class ParityDebugHud : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private bool visible = true;

        public void Bind(ShaiyaClientActor value)
        {
            actor = value;
        }

        private void OnGUI()
        {
            if (!visible || actor == null)
                return;

            const float width = 410f;

            GUI.Box(
                new Rect(14f, 14f, width, 200f),
                "Dreynox MMORPG · Client Parity");

            GUI.Label(
                new Rect(28f, 42f, width - 20f, 22f),
                "Movimiento: W/A/S/D · Correr: Shift · Saltar: Space");

            GUI.Label(
                new Rect(28f, 64f, width - 20f, 22f),
                "Vuelo: Shift+Space · Altura: Q/E");

            GUI.Label(
                new Rect(28f, 86f, width - 20f, 22f),
                "Cámara: RMB + ratón · Zoom: rueda");

            GUI.Label(
                new Rect(28f, 110f, width - 20f, 22f),
                "Estado: " + actor.Motion.State +
                " · ANI: " + actor.CurrentSemanticAnimation);

            GUI.Label(
                new Rect(28f, 132f, width - 20f, 22f),
                "Combate: " +
                (actor.Combat.InCombatGuard ? "GUARD" : "FUERA") +
                " · Target: " +
                (actor.Combat.SelectedTargetId?.ToString() ?? "-"));

            GUI.Label(
                new Rect(28f, 154f, width - 20f, 22f),
                "Vuelo: " + actor.Flight.Phase +
                " · intención=" + actor.Flight.ManualRequested);

            GUI.Label(
                new Rect(28f, 176f, width - 20f, 22f),
                "F8: captura PNG para comparar con game.exe");
        }
    }
}
