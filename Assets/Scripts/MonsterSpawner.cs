using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IdleF1.Combat
{
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject monsterPrefab;

        [SerializeField]
        private Transform player;

        [SerializeField]
        private float spawnRadius = 15f;

        [SerializeField]
        private float minimumSpawnDistance = 6f;

        [SerializeField]
        private float spawnInterval = 2f;

        [SerializeField]
        private int initialSpawnCount = 5;

        [SerializeField]
        private int maxAliveMonsters = 30;

        [SerializeField]
        private Vector2 verticalOffsetRange = new Vector2(0f, 0f);

        [SerializeField]
        private bool drawDebug = true;

        private readonly List<Health> activeMonsters = new List<Health>();
        private WaitForSeconds waitCache;

        private void Awake()
        {
            waitCache = new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));
        }

        private void Start()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                player = playerObj != null ? playerObj.transform : null;
            }

            for (int i = 0; i < Mathf.Max(0, initialSpawnCount); i++)
            {
                TrySpawnMonster();
            }

            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                yield return waitCache;
                TrySpawnMonster();
            }
        }

        private void TrySpawnMonster()
        {
            if (monsterPrefab == null || player == null) return;

            CleanupList();
            if (activeMonsters.Count >= maxAliveMonsters) return;

            Vector3 spawnPosition = GetSpawnPosition();
            GameObject monsterObj = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
            MonsterController monsterController = monsterObj.GetComponent<MonsterController>();
            monsterController?.SetTarget(player);

            Health monsterHealth = monsterObj.GetComponent<Health>();
            if (monsterHealth != null)
            {
                activeMonsters.Add(monsterHealth);
                monsterHealth.OnDeath.AddListener(() => OnMonsterDeath(monsterHealth));
            }
        }

        private Vector3 GetSpawnPosition()
        {
            const int maxAttempts = 8;
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector2 planar = Random.insideUnitCircle;
                if (planar.sqrMagnitude < 0.01f) continue;

                planar.Normalize();
                float distance = Random.Range(minimumSpawnDistance, spawnRadius);
                Vector3 offset = new Vector3(planar.x, 0f, planar.y) * distance;

                Vector3 position = player.position + offset;
                float yOffset = Random.Range(verticalOffsetRange.x, verticalOffsetRange.y);
                position.y += yOffset;
                return position;
            }

            return player.position + new Vector3(minimumSpawnDistance, 0f, 0f);
        }

        private void CleanupList()
        {
            activeMonsters.RemoveAll(item => item == null || item.IsDead);
        }

        private void OnMonsterDeath(Health monsterHealth)
        {
            activeMonsters.Remove(monsterHealth);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebug) return;
            if (player == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, spawnRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, minimumSpawnDistance);
        }
    }
}

