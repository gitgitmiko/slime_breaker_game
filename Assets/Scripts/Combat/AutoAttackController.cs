using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>The sorceress auto-casts a homing magic bolt from her staff at the nearest enemy in range.</summary>
    public class AutoAttackController : MonoBehaviour
    {
        public GameObject boltPrefab;
        public float boltSpeed = 17f;
        /// <summary>Delay between the cast animation starting and the bolt leaving the staff.</summary>
        public float releaseDelay = 0.14f;

        float cooldown;
        bool casting;
        Animator animator;
        Transform staffTip;
        PlayerHealth health;

        public Transform StaffTip => staffTip;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<PlayerHealth>();
            staffTip = SkillController.FindDeep(transform, "StaffTip");
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || casting)
                return;
            if (health != null && health.IsDead)
                return;
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                return;
            }

            var stats = GameFlowController.Instance.Stats;
            var target = FindNearest(stats.attackRange);
            if (target == null)
                return;
            cooldown = stats.attackCooldown;
            bool crit;
            int damage = CombatMath.ApplyCrit(stats.damage, out crit);
            StartCoroutine(Cast(target, damage, stats.attackRange, crit));
        }

        EnemyHealth FindNearest(float range)
        {
            EnemyHealth best = null;
            EnemyHealth fallback = null;
            float bestDistance = range * range;
            float fallbackDistance = range * range;
            Vector3 origin = transform.position;
            Vector3 eye = CastOrigin;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                float distance = (enemy.transform.position - origin).sqrMagnitude;
                if (distance > bestDistance && distance > fallbackDistance)
                    continue;
                if (distance <= fallbackDistance)
                {
                    fallbackDistance = distance;
                    fallback = enemy;
                }
                if (distance <= bestDistance && !Obstacles.Blocked(eye, enemy.transform.position + Vector3.up * enemy.HitHeight))
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best != null ? best : fallback;
        }

        Vector3 CastOrigin
        {
            get
            {
                if (staffTip != null)
                    return staffTip.position;
                return transform.position + Vector3.up * 1.15f + transform.forward * 0.45f;
            }
        }

        IEnumerator Cast(EnemyHealth target, int damage, float range, bool crit)
        {
            casting = true;
            if (target != null)
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            if (animator != null)
                animator.SetTrigger("Attack");
            AudioService.Instance?.Play("cast");

            float t = 0f;
            while (t < releaseDelay)
            {
                if (!GameFlowController.IsPlaying)
                {
                    yield return null;
                    continue;
                }
                t += Time.deltaTime;
                yield return null;
            }

            if (target == null || target.IsDead)
                target = FindNearest(range + 1.5f);

            if (target != null && !target.IsDead)
            {
                Vector3 origin = CastOrigin;
                VfxPlayer.Instance?.Play("cast", origin);
                if (boltPrefab != null)
                {
                    HomingProjectile.Launch(boltPrefab, origin, target, damage, boltSpeed, crit);
                }
                else
                {
                    target.TakeDamage(damage, true, true, crit);
                    VfxPlayer.Instance?.Play("hit", target.transform.position + Vector3.up * target.HitHeight);
                }
            }
            casting = false;
        }
    }
}
