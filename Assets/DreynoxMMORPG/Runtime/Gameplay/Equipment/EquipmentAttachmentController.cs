using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    public sealed class EquipmentAttachmentController : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _sockets = new Dictionary<string, Transform>();
        private readonly Dictionary<EquipmentSlot, GameObject> _instances = new Dictionary<EquipmentSlot, GameObject>();
        private readonly Dictionary<EquipmentSlot, AttachmentDefinition> _definitions = new Dictionary<EquipmentSlot, AttachmentDefinition>();

        private void Awake() => RebuildSocketCache();

        public void RebuildSocketCache()
        {
            _sockets.Clear();
            foreach (AttachmentSocket socket in GetComponentsInChildren<AttachmentSocket>(true))
                if (!string.IsNullOrWhiteSpace(socket.SocketName)) _sockets[socket.SocketName] = socket.transform;
        }

        public bool Equip(AttachmentDefinition definition)
        {
            if (definition == null || definition.prefab == null || string.IsNullOrWhiteSpace(definition.socketName)) return false;
            if (!_sockets.TryGetValue(definition.socketName, out Transform socket)) return false;

            if (definition.slot == EquipmentSlot.OffHand && _definitions.TryGetValue(EquipmentSlot.MainHand, out AttachmentDefinition main) && main.occupiesBothHands)
                return false;

            if (definition.slot == EquipmentSlot.MainHand && definition.occupiesBothHands)
                Unequip(EquipmentSlot.OffHand);

            Unequip(definition.slot);
            GameObject instance = Instantiate(definition.prefab, socket, false);
            instance.name = definition.prefab.name + "_Equipped";
            instance.transform.localPosition = definition.localPosition;
            instance.transform.localRotation = Quaternion.Euler(definition.localEulerAngles);
            instance.transform.localScale = definition.localScale;
            _instances[definition.slot] = instance;
            _definitions[definition.slot] = definition;
            return true;
        }

        public void Unequip(EquipmentSlot slot)
        {
            _definitions.Remove(slot);
            if (!_instances.TryGetValue(slot, out GameObject instance)) return;
            _instances.Remove(slot);
            if (instance != null) Destroy(instance);
        }

        public bool Has(EquipmentSlot slot) => _instances.ContainsKey(slot) && _instances[slot] != null;

        public bool TryGetDefinition(
            EquipmentSlot slot,
            out AttachmentDefinition definition)
        {
            return _definitions.TryGetValue(slot, out definition);
        }

        public bool TryGetInstance(
            EquipmentSlot slot,
            out GameObject instance)
        {
            return _instances.TryGetValue(slot, out instance) &&
                   instance != null;
        }
    }
}
