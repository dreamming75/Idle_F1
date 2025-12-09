using UnityEngine;

namespace IdleF1.Combat
{
    /// <summary>
    /// Automatically acquires the nearest monster and deals damage at a fixed cadence.
    /// Attach to the player character to keep monsters away without manual input.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerAutoCombat : MonoBehaviour
    {
        [SerializeField]
        private float detectionRadius = 10f;

        [SerializeField]
        private float attackRange = 8f;

        [SerializeField]
        private float attackDamage = 12f;

        [SerializeField]
        private float attackInterval = 0.4f;

        [SerializeField]
        private LayerMask monsterLayers = ~0;

        [SerializeField]
        private Transform aimPivot;

        [SerializeField]
        private bool drawDebug = true;

        private Transform currentTarget;
        private float attackTimer;

        private void Update()
        {
            attackTimer += Time.deltaTime;

            if (ShouldAcquireNewTarget())
            {
                currentTarget = AcquireTarget();
            }

            if (currentTarget != null && attackTimer >= attackInterval)
            {
                float sqrDistance = (currentTarget.position - transform.position).sqrMagnitude;
                if (sqrDistance <= attackRange * attackRange)
                {
                    attackTimer = 0f;
                    Health targetHealth = currentTarget.GetComponent<Health>();
                    targetHealth?.TakeDamage(attackDamage);
                }
                else
                {
                    currentTarget = null;
                }
            }

            AimAtTarget();
        }

        private bool ShouldAcquireNewTarget()
        {
            if (currentTarget == null) return true;
            if (!currentTarget.gameObject.activeInHierarchy) return true;

            Health targetHealth = currentTarget.GetComponent<Health>();
            if (targetHealth != null && targetHealth.IsDead) return true;

            float sqrDistance = (currentTarget.position - transform.position).sqrMagnitude;
            return sqrDistance > detectionRadius * detectionRadius;
        }

        private Transform AcquireTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, monsterLayers, QueryTriggerInteraction.Ignore);

            float bestScore = float.MaxValue;
            Transform bestTarget = null;

            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponent<Health>();
                if (health == null || health.IsDead) continue;

                float sqrDistance = (hit.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < bestScore)
                {
                    bestScore = sqrDistance;
                    bestTarget = hit.transform;
                }
            }

            return bestTarget;
        }

        private void AimAtTarget()
        {
            if (aimPivot == null || currentTarget == null) return;

            Vector3 toTarget = currentTarget.position - aimPivot.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Quaternion lookRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            aimPivot.rotation = lookRotation;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebug) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}

