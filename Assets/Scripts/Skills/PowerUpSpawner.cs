using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Drops one power-up crystal somewhere in the arena every N hero levels.</summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        public SkillDefinition[] skills;
        public GameObject pickupPrefab;
        public int everyLevels = RunRules.PowerUpEvery;
        public float minRadius = 3f;
        public float maxRadius = 16f;
        public float minDistanceFromHero = 3.5f;

        public int SpawnedTiers { get; private set; }

        public PowerUpPickup Spawn()
        {
            var definition = Choose();
            if (definition == null || pickupPrefab == null)
                return null;
            SpawnedTiers++;
            Vector3 hero = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
            Vector3 position = Vector3.zero;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minRadius, maxRadius);
                position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 flat = position - hero;
                flat.y = 0f;
                if (flat.magnitude < minDistanceFromHero)
                    continue;
                if (Obstacles.Overlaps(position, 0.9f))
                    continue;
                break;
            }
            var instance = Instantiate(pickupPrefab, position, Quaternion.identity);
            var pickup = instance.GetComponent<PowerUpPickup>();
            if (pickup == null)
                pickup = instance.AddComponent<PowerUpPickup>();
            pickup.Configure(definition);
            AudioService.Instance?.Play("powerupspawn");
            VfxPlayer.Instance?.Play("powerup", position + Vector3.up * 0.6f, definition.color);
            var flow = GameFlowController.Instance;
            if (flow != null && flow.ui != null)
                flow.ui.ShowToast("Power-up muncul di arena!", definition.color);
            return pickup;
        }

        SkillDefinition Choose()
        {
            if (skills == null || skills.Length == 0)
                return null;
            var controller = SkillController.Instance;
            var fresh = new List<SkillDefinition>();
            var upgradable = new List<SkillDefinition>();
            foreach (var skill in skills)
            {
                if (skill == null)
                    continue;
                int level = controller != null ? controller.LevelOf(skill.kind) : 0;
                if (level == 0)
                    fresh.Add(skill);
                else if (level < skill.maxLevel)
                    upgradable.Add(skill);
            }
            if (fresh.Count > 0)
                return fresh[Random.Range(0, fresh.Count)];
            if (upgradable.Count > 0)
                return upgradable[Random.Range(0, upgradable.Count)];
            // Every skill is at MAX: nothing left to grant.
            return null;
        }
    }
}
