using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Client
{
    public sealed class ParitySandboxSetup : MonoBehaviour
    {
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private bool startWithWings = true;

        public void Bind(ShaiyaClientActor value) => actor = value;

        private void Start()
        {
            if (actor != null && startWithWings) actor.SetWings(true, 0, 0);
        }
    }
}
