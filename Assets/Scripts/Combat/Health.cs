using System;
using UnityEngine;

namespace CyberDefender.Combat
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead => Current <= 0f;

        public event Action<Health, float> Damaged; // (кто, сколько снято)
        public event Action<Health> Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void SetMax(float value)
        {
            maxHealth = Mathf.Max(1f, value);
            Current = maxHealth;
        }

        public void ResetHealth() => Current = maxHealth;

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
                return;
            Current = Mathf.Min(Current + amount, maxHealth);
        }

        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return 0f;

            float applied = Mathf.Min(amount, Current);
            Current -= applied;
            Damaged?.Invoke(this, applied);

            if (IsDead)
                Died?.Invoke(this);

            return applied;
        }
    }
}
