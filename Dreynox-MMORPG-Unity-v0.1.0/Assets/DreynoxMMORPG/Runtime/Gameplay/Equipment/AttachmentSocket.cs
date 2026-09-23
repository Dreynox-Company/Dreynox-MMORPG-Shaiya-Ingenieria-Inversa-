using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Equipment
{
    public sealed class AttachmentSocket : MonoBehaviour
    {
        [SerializeField] private string socketName;
        public string SocketName => socketName;

        public void Configure(string value)
        {
            socketName = value ?? string.Empty;
        }
    }
}
