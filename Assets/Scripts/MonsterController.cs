using UnityEngine;

namespace IdleF1.Combat
{
    [RequireComponent(typeof(Health))]
    public class MonsterController : MonoBehaviour
    {
        [SerializeField]
        private float moveSpeed = 2.5f;

        [SerializeField]
        private float stoppingDistance = 1.25f;

        [SerializeField]
        private float attackDamage = 4f;

        [SerializeField]
        private float attackCooldown = 1.25f;

        [SerializeField]
        private float lookAtLerp = 10f;

        [SerializeField]
        private bool destroyOnDeath = true;

        [SerializeField]
        private string targetTag = "Player";

        [SerializeField]
        private Transform target;

        [SerializeField]
        private Health health;

        private Health targetHealth;
        private float attackTimer;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }
        }

        private void Start()
        {
            ResolveTarget();
            if (health != null)
            {
                health.OnDeath.AddListener(OnDeath);
            }
        }

        private void Update()
        {
            if (health != null && health.IsDead) return;
            if (target == null) return;

            attackTimer += Time.deltaTime;

            Vector3 toTarget = target.position - transform.position;
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.zero;

            if (distance > stoppingDistance)
            {
                transform.position += direction * moveSpeed * Time.deltaTime;
            }

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lookAtLerp * Time.deltaTime);
            }

            if (distance <= stoppingDistance && attackTimer >= attackCooldown)
            {
                attackTimer = 0f;
                targetHealth?.TakeDamage(attackDamage);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            targetHealth = target != null ? target.GetComponent<Health>() : null;
        }

        private void ResolveTarget()
        {
            if (target != null)
            {
                targetHealth = target.GetComponent<Health>();
                return;
            }

            if (!string.IsNullOrEmpty(targetTag))
            {
                GameObject targetObj = GameObject.FindGameObjectWithTag(targetTag);
                if (targetObj != null)
                {
                    SetTarget(targetObj.transform);
                }
            }
        }

        private void OnDeath()
        {
            enabled = false;
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        }
    }
}

