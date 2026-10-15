using UnityEngine;

namespace SlimeBreaker
{
    public class EnemySpawner : MonoBehaviour
    {
        public BalanceConfig balance;
        public GameObject normalPrefab;
        public GameObject fastPrefab;
        public GameObject tankPrefab;
        public GameObject bossPrefab;
        public RunManager run;
        public float arenaRadius = 11.2f;

        float timer = 1.2f;
        bool stopped;
        public bool Paused { get; set; }
        public int Stage { get; set; } = 1;

        void Update()
        {
            if (stopped || Paused || !GameFlowController.IsPlaying || balance == null || balance.phases == null)
                return;
            timer -= Time.deltaTime;
            if (timer > 0f)
                return;
            if (EnemyHealth.Active.Count >= balance.maxAliveEnemies)
            {
                timer = 0.4f;
                return;
            }

            float elapsed = run != null ? run.Elapsed : 0f;
            SpawnPhase phase = balance.phases[balance.phases.Length - 1];
            for (int i = 0; i < balance.phases.Length; i++)
            {
                if (elapsed <= balance.phases[i].untilSeconds)
                {
                    phase = balance.phases[i];
                    break;
                }
            }
            float interval = phase.interval * Mathf.Pow(0.93f, Mathf.Max(0, Stage - 1));
            timer = Mathf.Max(0.28f, interval);
            Spawn(Pick(phase));
        }

        GameObject Pick(SpawnPhase phase)
        {
            int total = Mathf.Max(0, phase.normal) + Mathf.Max(0, phase.fast) + Mathf.Max(0, phase.tank);
            if (total <= 0)
                return normalPrefab;
            int roll = Random.Range(0, total);
            if (roll < phase.normal)
                return normalPrefab;
            roll -= phase.normal;
            if (roll < phase.fast)
                return fastPrefab;
            return tankPrefab;
        }

        void Spawn(GameObject prefab)
        {
            if (prefab == null)
                return;
            Vector3 position = Vector3.zero;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(arenaRadius * 0.72f, arenaRadius * 0.98f);
                position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!Obstacles.Overlaps(position, 0.7f))
                    break;
            }
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var health = instance.GetComponent<EnemyHealth>();
            health?.Scale(Stage);
        }

        public void StopAndSpawnBoss()
        {
            stopped = true;
            if (bossPrefab == null)
                return;
            Vector3 bossPosition = new Vector3(0f, 0f, arenaRadius * 0.55f);
            if (Obstacles.Overlaps(bossPosition, 1.2f))
                bossPosition = new Vector3(arenaRadius * 0.45f, 0f, 0f);
            var instance = Instantiate(bossPrefab, bossPosition, Quaternion.identity);
            instance.GetComponent<EnemyHealth>()?.Scale(Stage);
            AudioService.Instance?.Play("bosswarn");
        }
    }
}
