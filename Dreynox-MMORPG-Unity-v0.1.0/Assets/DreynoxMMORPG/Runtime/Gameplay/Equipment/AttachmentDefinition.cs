using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "Dreynox MMORPG/Equipment/Attachment Definition", fileName = "AttachmentDefinition")]
    public sealed class AttachmentDefinition : ScriptableObject
    {
        public string legacyResourceId;
        public EquipmentSlot slot;
        public GameObject prefab;
        public string socketName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public bool mirrorForOppositeSide;
    }
}
