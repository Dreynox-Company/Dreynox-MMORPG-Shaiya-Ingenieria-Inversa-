using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    public sealed class ShaiyaCombatTarget : MonoBehaviour
    {
        [SerializeField] private int targetId = 1;
        [SerializeField] private int maxHealth = 1000;
        [SerializeField] private int health = 1000;

        public int Generation { get; private set; }
        public int TargetId => targetId;
        public int MaxHealth => maxHealth;
        public int Health => health;
        public bool IsAlive => health > 0;

        public event Action<ShaiyaCombatTarget, int> Damaged;
        public event Action<ShaiyaCombatTarget> Died;
        public event Action<ShaiyaCombatTarget> Reborn;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            health = Mathf.Clamp(
                health <= 0 ? maxHealth : health,
                0,
                maxHealth);
        }

        public int ApplyDamage(int amount)
        {
            if (amount <= 0 || health <= 0)
                return 0;

            int before = health;
            health = Mathf.Max(0, health - amount);

            int applied = before - health;
            if (applied > 0)
                Damaged?.Invoke(this, applied);

            if (before > 0 && health == 0)
                Died?.Invoke(this);

            return applied;
        }

        public void Configure(
            int id,
            int maximumHealth,
            int currentHealth = -1)
        {
            Generation++;
            targetId = id;
            maxHealth = Mathf.Max(1, maximumHealth);
            health = currentHealth < 0 ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        public void Rebirth()
        {
            bool wasDead = health <= 0;
            health = maxHealth;

            if (wasDead)
                Reborn?.Invoke(this);
        }
    }
}
