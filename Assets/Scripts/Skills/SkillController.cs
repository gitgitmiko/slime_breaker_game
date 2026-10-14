using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Runs the hero's power-up skills: orbiting ice shards, chain lightning, fire nova, arcane missiles.</summary>
    public class SkillController : MonoBehaviour
    {
        public static SkillController Instance { get; private set; }

        [Serializable]
        public class ActiveSkill
        {
            public SkillDefinition definition;
            public int level = 1;
            [NonSerialized] public float cooldown;
            [NonSerialized] public Transform pivot;
            [NonSerialized] public readonly List<Transform> shards = new List<Transform>();
            [NonSerialized] public readonly Dictionary<EnemyHealth, float> contactTimers = new Dictionary<EnemyHealth, float>();
        }

        public GameObject iceShardPrefab;
        public GameObject missilePrefab;
        public GameObject fireRingPrefab;
        public GameObject lightningPrefab;
        public float orbitHeight = 0.75f;
        public float orbitSpeed = 150f;

        public event Action Changed;
        public IReadOnlyList<ActiveSkill> Skills => skills;
        public int Count => skills.Count;

        readonly List<ActiveSkill> skills = new List<ActiveSkill>();
        readonly List<EnemyHealth> scratch = new List<EnemyHealth>();
        Transform staffTip;

        void Awake()
        {
            Instance = this;
            staffTip = FindDeep(transform, "StaffTip");
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        public bool Has(SkillKind kind) => Find(kind) != null;

        public ActiveSkill Find(SkillKind kind)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].definition != null && skills[i].definition.kind == kind)
                    return skills[i];
            }
            return null;
        }

        public int LevelOf(SkillKind kind)
        {
            var skill = Find(kind);
            return skill != null ? skill.level : 0;
        }

        /// <summary>Adds the skill or raises its level. Returns the resulting level.</summary>
        public int Grant(SkillDefinition definition)
        {
            if (definition == null)
                return 0;
            var skill = Find(definition.kind);
            if (skill == null)
            {
                skill = new ActiveSkill { definition = definition, level = 1 };
                skills.Add(skill);
            }
            else
            {
                skill.level = Mathf.Min(definition.maxLevel, skill.level + 1);
            }
            if (definition.kind == SkillKind.IceOrbit)
                RebuildOrbit(skill);
            Changed?.Invoke();
            return skill.level;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            var health = PlayerHealth.Instance;
            if (health != null && health.IsDead)
                return;
            float dt = Time.deltaTime;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill.definition == null)
                    continue;
                skill.cooldown -= dt;
                switch (skill.definition.kind)
                {
                    case SkillKind.IceOrbit:
                        TickIce(skill, dt);
                        break;
                    case SkillKind.LightningChain:
                        if (skill.cooldown <= 0f && CastLightning(skill))
                            skill.cooldown = skill.definition.CooldownAt(skill.level);
                        break;
                    case SkillKind.FireNova:
                        if (skill.cooldown <= 0f && CastFire(skill))
                            skill.cooldown = skill.definition.CooldownAt(skill.level);
                        break;
                    case SkillKind.ArcaneMissile:
                        if (skill.cooldown <= 0f && CastMissiles(skill))
                            skill.cooldown = skill.definition.CooldownAt(skill.level);
                        break;
                }
            }
        }

        Vector3 CastOrigin => staffTip != null ? staffTip.position : transform.position + Vector3.up * 1.2f;

        // ------------------------------------------------------------ ice orbit

        void RebuildOrbit(ActiveSkill skill)
        {
            if (skill.pivot == null)
            {
                var pivotObject = new GameObject("IceOrbit");
                pivotObject.transform.SetParent(transform, false);
                pivotObject.transform.localPosition = new Vector3(0f, orbitHeight, 0f);
                skill.pivot = pivotObject.transform;
            }
            int wanted = skill.definition.CountAt(skill.level);
            while (skill.shards.Count < wanted)
            {
                GameObject shard = iceShardPrefab != null
                    ? Instantiate(iceShardPrefab, skill.pivot)
                    : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (iceShardPrefab == null)
                {
                    shard.transform.SetParent(skill.pivot, false);
                    shard.transform.localScale = Vector3.one * 0.3f;
                    var collider = shard.GetComponent<Collider>();
                    if (collider != null)
                        Destroy(collider);
                }
                shard.name = "IceShard";
                skill.shards.Add(shard.transform);
            }
            float radius = skill.definition.RadiusAt(skill.level);
            for (int i = 0; i < skill.shards.Count; i++)
            {
                float angle = i * Mathf.PI * 2f / skill.shards.Count;
                skill.shards[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                skill.shards[i].localRotation = Quaternion.Euler(90f, angle * Mathf.Rad2Deg, 0f);
            }
        }

        void TickIce(ActiveSkill skill, float dt)
        {
            if (skill.pivot == null)
                RebuildOrbit(skill);
            float speed = orbitSpeed + 25f * (skill.level - 1);
            skill.pivot.Rotate(0f, speed * dt, 0f, Space.World);
            for (int i = 0; i < skill.shards.Count; i++)
                skill.shards[i].Rotate(0f, 0f, 240f * dt, Space.Self);

            var keys = scratch;
            keys.Clear();
            foreach (var pair in skill.contactTimers)
            {
                if (pair.Key == null)
                    keys.Add(pair.Key);
            }
            for (int i = 0; i < keys.Count; i++)
                skill.contactTimers.Remove(keys[i]);

            int damage = skill.definition.DamageAt(skill.level);
            for (int e = 0; e < EnemyHealth.Active.Count; e++)
            {
                var enemy = EnemyHealth.Active[e];
                if (enemy == null || enemy.IsDead)
                    continue;
                if (skill.contactTimers.TryGetValue(enemy, out float wait) && wait > Time.time)
                    continue;
                Vector3 enemyPoint = enemy.transform.position + Vector3.up * enemy.HitHeight;
                float reach = 0.42f + enemy.HitRadius;
                for (int s = 0; s < skill.shards.Count; s++)
                {
                    Vector3 shardPoint = skill.shards[s].position;
                    Vector3 delta = enemyPoint - shardPoint;
                    delta.y *= 0.5f;
                    if (delta.sqrMagnitude > reach * reach)
                        continue;
                    enemy.TakeDamage(damage, playHitSound: false);
                    enemy.ApplySlow(skill.definition.slowFactor, skill.definition.effectDuration);
                    skill.contactTimers[enemy] = Time.time + 0.5f;
                    VfxPlayer.Instance?.Play("ice", shardPoint);
                    AudioService.Instance?.Play("freeze");
                    break;
                }
            }
        }

        // ------------------------------------------------------------ lightning

        bool CastLightning(ActiveSkill skill)
        {
            float range = 7.5f + skill.level * 0.5f;
            var first = Nearest(transform.position, range, null);
            if (first == null)
                return false;
            int damage = skill.definition.DamageAt(skill.level);
            int jumps = skill.definition.CountAt(skill.level);
            var hitList = new List<EnemyHealth> { first };
            Vector3 from = CastOrigin;
            var current = first;
            for (int i = 0; i < jumps && current != null; i++)
            {
                Vector3 point = current.transform.position + Vector3.up * current.HitHeight;
                LightningBolt.Flash(lightningPrefab, from, point);
                current.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * (i == 0 ? 1f : 0.75f))), playHitSound: false);
                VfxPlayer.Instance?.Play("zap", point);
                from = point;
                current = Nearest(current.transform.position, 3.8f, hitList);
                if (current != null)
                    hitList.Add(current);
            }
            AudioService.Instance?.Play("lightning");
            return true;
        }

        // ------------------------------------------------------------ fire nova

        bool CastFire(ActiveSkill skill)
        {
            if (Nearest(transform.position, 6f, null) == null)
                return false;
            float radius = skill.definition.RadiusAt(skill.level);
            int damage = skill.definition.DamageAt(skill.level);
            Vector3 center = transform.position;
            if (fireRingPrefab != null)
            {
                var ring = Instantiate(fireRingPrefab, center + Vector3.up * 0.08f, Quaternion.identity);
                var pulse = ring.GetComponent<RingPulse>();
                if (pulse == null)
                    pulse = ring.AddComponent<RingPulse>();
                pulse.targetRadius = radius;
            }
            VfxPlayer.Instance?.Play("fire", center + Vector3.up * 0.3f);
            AudioService.Instance?.Play("fire");
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                Vector3 flat = enemy.transform.position - center;
                flat.y = 0f;
                if (flat.magnitude <= radius + enemy.HitRadius)
                {
                    enemy.TakeDamage(damage, playHitSound: false);
                    enemy.ApplyBurn(Mathf.Max(1, damage / 4), 3, 0.5f);
                }
            }
            return true;
        }

        // ------------------------------------------------------------ arcane missiles

        bool CastMissiles(ActiveSkill skill)
        {
            int count = skill.definition.CountAt(skill.level);
            int damage = skill.definition.DamageAt(skill.level);
            var candidates = new List<EnemyHealth>();
            Vector3 origin = transform.position;
            float range = 9.5f;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                if ((enemy.transform.position - origin).sqrMagnitude <= range * range)
                    candidates.Add(enemy);
            }
            if (candidates.Count == 0)
                return false;
            for (int i = 0; i < count; i++)
            {
                var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                Vector3 spawn = CastOrigin + UnityEngine.Random.insideUnitSphere * 0.25f;
                var missile = HomingProjectile.Launch(missilePrefab, spawn, target, damage, 11f + skill.level);
                if (missile != null)
                {
                    missile.hitSfx = "arcanehit";
                    missile.hitVfx = "arcane";
                    missile.turnRate = 6f;
                }
            }
            AudioService.Instance?.Play("arcane");
            return true;
        }

        EnemyHealth Nearest(Vector3 from, float range, List<EnemyHealth> exclude)
        {
            EnemyHealth best = null;
            float bestDistance = range * range;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                if (exclude != null && exclude.Contains(enemy))
                    continue;
                float distance = (enemy.transform.position - from).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }
    }
}
