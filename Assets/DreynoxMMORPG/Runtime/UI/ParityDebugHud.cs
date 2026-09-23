using Dreynox.Mmorpg.Gameplay.Client;
using UnityEngine;

namespace Dreynox.Mmorpg.UI
{
    public sealed class ParityDebugHud : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private bool visible = true;

        public void Bind(ShaiyaClientActor value) => actor = value;

        private void OnGUI()
        {
            if (!visible || actor == null) return;
            const float width = 410f;
            GUI.Box(new Rect(14, 14, width, 200), "Dreynox MMORPG · Client Parity Sandbox");
            GUI.Label(new Rect(28, 42, width - 20, 22), "Movimiento: W/A/S/D · Correr: Shift · Saltar: Space");
            GUI.Label(new Rect(28, 64, width - 20, 22), "Vuelo manual: Shift+Space (requiere alas) · Altura: Q/E");
            GUI.Label(new Rect(28, 86, width - 20, 22), "Cámara: botón derecho + ratón · Zoom: rueda");
            GUI.Label(new Rect(28, 110, width - 20, 22), "Estado: " + actor.Motion.State + " · ANI: " + actor.CurrentSemanticAnimation);
            GUI.Label(new Rect(28, 132, width - 20, 22), "Combate: " + (actor.Combat.InCombatGuard ? "GUARD" : "FUERA") + " · Target: " + (actor.Combat.SelectedTargetId?.ToString() ?? "-") );
            GUI.Label(new Rect(28, 154, width - 20, 22), "Vuelo: " + actor.Flight.Phase + " · intención=" + actor.Flight.ManualRequested);
            GUI.Label(new Rect(28, 176, width - 20, 22), "F8: captura PNG para comparación visual contra game.exe");
        }
    }
}
