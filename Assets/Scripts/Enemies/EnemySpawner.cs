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

        void Update()
        {
            if (stopped || !GameFlowController.IsPlaying || balance == null || balance.phases == null)
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
            timer = Mathf.Max(0.35f, phase.interval);
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
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(arenaRadius * 0.72f, arenaRadius * 0.98f);
            var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Instantiate(prefab, position, Quaternion.identity);
        }

        public void StopAndSpawnBoss()
        {
            stopped = true;
            if (bossPrefab == null)
                return;
            Instantiate(bossPrefab, new Vector3(0f, 0f, arenaRadius * 0.55f), Quaternion.identity);
            AudioService.Instance?.Play("bosswarn");
        }
    }
}
