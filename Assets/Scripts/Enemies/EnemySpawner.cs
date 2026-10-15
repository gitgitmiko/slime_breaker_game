using UnityEngine;

namespace SlimeBreaker
{
    public class EnemySpawner : MonoBehaviour
    {
        public BalanceConfig balance;
        public GameObject normalPrefab;
        public GameObject fastPrefab;
        public GameObject tankPrefab;
        public GameObject rangedPrefab;
        public GameObject bossPrefab;
        public GameObject hostileBolt;
        public RunManager run;
        public float arenaRadius = RunRules.ArenaRadius;

        float timer = 1.2f;
        public bool Paused { get; set; }
        public int Stage { get; set; } = 1;

        void Update()
        {
            if (Paused || !GameFlowController.IsPlaying || balance == null || balance.phases == null)
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
            int floor = StageManager.Instance != null ? StageManager.Instance.Floor : Mathf.Max(1, Stage);
            Stage = floor;
            int band = Mathf.Max(1, (floor - 1) / RunRules.BossEvery + 1);
            float interval = phase.interval * Mathf.Pow(0.93f, Mathf.Max(0, band - 1));
            timer = Mathf.Max(0.28f, interval);
            Spawn(Pick(phase), band);
        }

        GameObject Pick(SpawnPhase phase)
        {
            int total = Mathf.Max(0, phase.normal) + Mathf.Max(0, phase.fast) + Mathf.Max(0, phase.tank) + Mathf.Max(0, phase.ranged);
            if (total <= 0)
                return normalPrefab;
            int roll = Random.Range(0, total);
            if (roll < phase.normal)
                return normalPrefab;
            roll -= phase.normal;
            if (roll < phase.fast)
                return fastPrefab;
            roll -= phase.fast;
            if (roll < phase.tank)
                return tankPrefab;
            return rangedPrefab != null ? rangedPrefab : fastPrefab;
        }

        void Spawn(GameObject prefab, int band)
        {
            if (prefab == null)
                return;
            Vector3 position = Vector3.zero;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(arenaRadius * 0.58f, arenaRadius * 0.86f);
                position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!Obstacles.Overlaps(position, 0.7f))
                    break;
            }
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var health = instance.GetComponent<EnemyHealth>();
            health?.Scale(Mathf.Max(1, band));
            var ai = instance.GetComponent<EnemyChaseAI>();
            if (ai != null)
            {
                ai.arenaRadius = arenaRadius;
                if (ai.boltPrefab == null)
                    ai.boltPrefab = hostileBolt;
            }
        }
    }
}
