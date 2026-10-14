using UnityEngine;

namespace SlimeBreaker
{
    public class RunManager : MonoBehaviour
    {
        public BalanceConfig balance;
        public EnemySpawner spawner;
        public float Elapsed { get; private set; }
        bool bossSpawned;

        void OnEnable()
        {
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        void OnDisable()
        {
            EnemyHealth.AnyDied -= OnEnemyDied;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || balance == null)
                return;
            Elapsed += Time.deltaTime;
            if (!bossSpawned && Elapsed >= balance.bossTime)
            {
                bossSpawned = true;
                spawner?.StopAndSpawnBoss();
            }
        }

        void OnEnemyDied(EnemyHealth enemy)
        {
            if (enemy != null && enemy.IsBoss && GameFlowController.IsPlaying)
                GameFlowController.Instance.EndRun(true);
        }
    }
}
