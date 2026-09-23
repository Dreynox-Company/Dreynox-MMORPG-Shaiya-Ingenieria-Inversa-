using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Gameplay.Combat
{
    public sealed class CombatTarget : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0f;

        public event Action<CombatTarget, float> Damaged;
        public event Action<CombatTarget> Died;

        private void Awake()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke(this, amount);
            if (currentHealth <= 0f) Died?.Invoke(this);
        }
    }
}
