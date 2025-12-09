using UnityEngine;
using UnityEngine.Events;

namespace IdleF1.Combat
{
    /// <summary>
    /// Basic health container that raises an event when the entity dies.
    /// Works for both the player character and monsters.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField]
        private float maxHealth = 50f;

        [SerializeField]
        private UnityEvent onDeath = new UnityEvent();

        private float currentHealth;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public float NormalizedValue => Mathf.Approximately(maxHealth, 0f) ? 0f : currentHealth / maxHealth;
        public UnityEvent OnDeath => onDeath;

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
        }

        public void Revive()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            currentHealth -= Mathf.Max(0f, amount);
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                onDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0f, amount));
        }

        public void Kill()
        {
            if (IsDead) return;

            currentHealth = 0f;
            onDeath?.Invoke();
        }
    }
}

