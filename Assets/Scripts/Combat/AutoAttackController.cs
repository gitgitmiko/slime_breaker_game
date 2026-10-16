using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>The sorceress auto-casts a homing magic bolt while standing still. Walking or dashing cancels basic attacks.</summary>
    public class AutoAttackController : MonoBehaviour
    {
        public GameObject boltPrefab;
        public float boltSpeed = 17f;
        /// <summary>Delay between the cast animation starting and the bolt leaving the staff.</summary>
        /// <summary>Bolt leaves the staff when the bonk lands (Attack clip impact ≈ frame 6 of 16 at 30 fps).</summary>
        public float releaseDelay = 0.17f;

        float cooldown;
        bool casting;
        Animator animator;
        Transform staffTip;
        PlayerHealth health;
        PlayerController motor;
        EnemyHealth aim;

        public Transform StaffTip => staffTip;
        public bool IsCasting => casting;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<PlayerHealth>();
            motor = GetComponent<PlayerController>();
            staffTip = SkillController.FindDeep(transform, "StaffTip");
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || casting)
                return;
            if (health != null && health.IsDead)
                return;
            if (cooldown > 0f)
                cooldown -= Time.deltaTime;
            if (motor != null && motor.IsMoving)
                return;
            if (cooldown > 0f)
                return;

            var stats = GameFlowController.Instance.Stats;
            var target = FindNearest(stats.attackRange);
            if (target == null)
                return;
            cooldown = stats.attackCooldown;
            bool crit;
            int damage = CombatMath.ApplyCrit(stats.damage, out crit);
            StartCoroutine(Cast(target, damage, stats.attackRange, crit));
        }

        void LateUpdate()
        {
            if (!casting || aim == null || aim.IsDead)
                return;
            if (motor != null && motor.IsMoving)
                return;
            Face(aim.transform.position);
        }

        EnemyHealth FindNearest(float range)
        {
            EnemyHealth best = null;
            EnemyHealth fallback = null;
            float bestDistance = range * range;
            float fallbackDistance = range * range;
            Vector3 origin = transform.position;
            Vector3 eye = AimEye;
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

        Vector3 AimEye => transform.position + Vector3.up * 1.15f;

        Vector3 LaunchOrigin(EnemyHealth target)
        {
            Vector3 chest = AimEye;
            Vector3 aimPoint = target != null
                ? target.transform.position + Vector3.up * target.HitHeight
                : chest + transform.forward;
            Vector3 to = aimPoint - chest;
            if (to.sqrMagnitude < 0.0001f)
                to = transform.forward;
            Vector3 dir = to.normalized;
            Vector3 staff = staffTip != null ? staffTip.position : chest + dir * 0.55f;
            staff.y = Mathf.Max(staff.y, 0.7f);
            Vector3 fromStaff = staff - chest;
            fromStaff.y = 0f;
            Vector3 flatDir = dir;
            flatDir.y = 0f;
            // While running the staff swings behind the body; fire from the chest toward the target instead.
            if (flatDir.sqrMagnitude > 0.001f && Vector3.Dot(fromStaff, flatDir) < 0.05f)
                return chest + dir * 0.55f;
            return staff;
        }

        void Face(Vector3 world)
        {
            Vector3 to = world - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        IEnumerator Cast(EnemyHealth target, int damage, float range, bool crit)
        {
            casting = true;
            aim = target;
            bool moving = motor != null && motor.IsMoving;
            if (!moving && target != null)
                Face(target.transform.position);
            if (!moving && animator != null)
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
            aim = target;

            if (target != null && !target.IsDead)
            {
                Vector3 origin = LaunchOrigin(target);
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
            aim = null;
            casting = false;
        }
    }
}
