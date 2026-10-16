using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Glowing projectile that chases an enemy and applies damage on contact. Used by the hero bolt and arcane missiles.</summary>
    public class HomingProjectile : MonoBehaviour
    {
        public float speed = 16f;
        public float turnRate = 12f;
        public float hitRadius = 0.45f;
        public float lifetime = 2.5f;
        public string hitSfx = "bolthit";
        public string hitVfx = "hit";
        public float spin = 540f;
        /// <summary>True for power-up missiles: bosses ignore them unless Boss Breaker is active.</summary>
        public bool fromSkill;

        EnemyHealth target;
        int damage;
        bool crit;
        float age;
        Vector3 velocity;
        Transform spinner;
        bool startedInside;

        public static HomingProjectile Launch(GameObject prefab, Vector3 origin, EnemyHealth target, int damage, float speed = -1f, bool crit = false)
        {
            if (prefab == null || target == null)
                return null;
            var instance = Instantiate(prefab, origin, Quaternion.identity);
            var projectile = instance.GetComponent<HomingProjectile>();
            if (projectile == null)
                projectile = instance.AddComponent<HomingProjectile>();
            if (speed > 0f)
                projectile.speed = speed;
            projectile.Begin(target, damage, crit);
            return projectile;
        }

        void Begin(EnemyHealth enemy, int amount, bool isCrit)
        {
            target = enemy;
            damage = amount;
            crit = isCrit;
            Vector3 to = AimPoint() - transform.position;
            velocity = (to.sqrMagnitude > 0.001f ? to.normalized : transform.forward) * speed;
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity);
            spinner = transform.childCount > 0 ? transform.GetChild(0) : null;
            startedInside = Obstacles.Overlaps(transform.position, 0.16f);
        }

        Vector3 AimPoint()
        {
            if (target == null)
                return transform.position + transform.forward;
            return target.transform.position + Vector3.up * target.HitHeight;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > lifetime)
            {
                Destroy(gameObject);
                return;
            }
            if (target != null && !target.IsDead)
            {
                Vector3 desired = (AimPoint() - transform.position).normalized * speed;
                velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-turnRate * Time.deltaTime));
            }
            Vector3 delta = velocity * Time.deltaTime;
            bool stillInsideSpawn = startedInside && Obstacles.Overlaps(transform.position, 0.16f);
            if (startedInside && !stillInsideSpawn)
                startedInside = false;
            if (!stillInsideSpawn && Obstacles.Cast(transform.position, delta, 0.12f, out var blocked))
            {
                VfxPlayer.Instance?.Play(hitVfx, blocked.point);
                AudioService.Instance?.Play(hitSfx);
                Destroy(gameObject);
                return;
            }
            transform.position += delta;
            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity);
            if (spinner != null)
                spinner.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);

            if (target == null || target.IsDead)
            {
                // Keep flying briefly so the missile does not vanish mid-air, then fade out.
                if (age > 0.6f)
                    Destroy(gameObject);
                return;
            }
            var boss = target.GetComponent<BossController>();
            if (boss != null && boss.Shielded)
            {
                VfxPlayer.Instance?.Play("arcane", transform.position, BossCatalog.Tint(BossKind.Crystal));
                AudioService.Instance?.Play(hitSfx);
                Destroy(gameObject);
                return;
            }
            float reach = hitRadius + target.HitRadius;
            if ((AimPoint() - transform.position).sqrMagnitude <= reach * reach)
            {
                if (fromSkill)
                    target.TakeSkillDamage(damage, crit);
                else
                    target.TakeDamage(damage, true, true, crit);
                VfxPlayer.Instance?.Play(hitVfx, transform.position);
                AudioService.Instance?.Play(hitSfx);
                Destroy(gameObject);
            }
        }
    }
}
