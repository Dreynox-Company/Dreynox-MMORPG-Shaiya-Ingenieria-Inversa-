using Dreynox.Mmorpg.Gameplay.Client;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    /// <summary>Explicit single-player starting visual, not an authoritative inventory grant.</summary>
    public sealed class LocalStarterEquipment : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private AttachmentDefinition definition;
        public bool Equipped { get; private set; }
        public void Configure(ShaiyaClientActor player, AttachmentDefinition starter)
        { actor = player; definition = starter; }
        private void Start()
        {
            Equipped = actor != null && definition != null && actor.EquipAttachment(definition);
            if (!Equipped) Debug.LogError("Original local starter equipment did not attach. No replacement item was created.");
        }
    }
}
