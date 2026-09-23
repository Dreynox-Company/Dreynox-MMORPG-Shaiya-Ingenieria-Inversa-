using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    public sealed class ShaiyaCombatTarget : MonoBehaviour
    {
        [SerializeField] private int targetId = 1;
        [SerializeField] private int maxHealth = 1000;
        [SerializeField] private int health = 1000;

        public int TargetId => targetId;
        public int MaxHealth => maxHealth;
        public int Health => health;
        public bool IsAlive => health > 0;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            health = Mathf.Clamp(health <= 0 ? maxHealth : health, 0, maxHealth);
        }

        public int ApplyDamage(int amount)
        {
            if (amount <= 0 || health <= 0) return 0;
            int before = health;
            health = Mathf.Max(0, health - amount);
            return before - health;
        }

        public void Configure(int id, int maximumHealth)
        {
            targetId = id;
            maxHealth = Mathf.Max(1, maximumHealth);
            health = maxHealth;
        }

        public void Rebirth()
        {
            health = maxHealth;
        }
    }
}
