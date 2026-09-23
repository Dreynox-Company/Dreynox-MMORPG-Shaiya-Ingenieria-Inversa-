using System.Collections.Generic;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    public sealed class EquipmentAttachmentController : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> _sockets = new Dictionary<string, Transform>();
        private readonly Dictionary<EquipmentSlot, GameObject> _instances = new Dictionary<EquipmentSlot, GameObject>();

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
            Unequip(definition.slot);
            GameObject instance = Instantiate(definition.prefab, socket, false);
            instance.name = definition.prefab.name + "_Equipped";
            instance.transform.localPosition = definition.localPosition;
            instance.transform.localRotation = Quaternion.Euler(definition.localEulerAngles);
            instance.transform.localScale = definition.localScale;
            _instances[definition.slot] = instance;
            return true;
        }

        public void Unequip(EquipmentSlot slot)
        {
            if (!_instances.TryGetValue(slot, out GameObject instance)) return;
            _instances.Remove(slot);
            if (instance != null) Destroy(instance);
        }

        public bool Has(EquipmentSlot slot) => _instances.ContainsKey(slot) && _instances[slot] != null;
    }
}
