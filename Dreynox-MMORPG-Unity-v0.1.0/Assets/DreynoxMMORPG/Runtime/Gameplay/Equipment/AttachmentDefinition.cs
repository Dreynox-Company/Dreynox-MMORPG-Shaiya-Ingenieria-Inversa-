using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    [CreateAssetMenu(menuName = "Dreynox MMORPG/Equipment/Attachment Definition", fileName = "AttachmentDefinition")]
    public sealed class AttachmentDefinition : ScriptableObject
    {
        [Header("Legacy identity")]
        public string legacyResourceId;
        public EquipmentSlot slot;

        [Header("Visual")]
        public GameObject prefab;
        public string socketName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        public bool mirrorForOppositeSide;

        [Header("Gameplay semantics")]
        public ClientWeaponFamily weaponFamily;
        public bool occupiesBothHands;
        public float mountSeatHeight;
        public float wingLocalYawCorrection;
        public float wingLocalHeightCorrection;
    }
}
