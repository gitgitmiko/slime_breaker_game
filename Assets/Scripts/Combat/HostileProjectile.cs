using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Enemy or boss bolt that flies toward a point and hurts the hero. Blocked by obstacles.</summary>
    public class HostileProjectile : MonoBehaviour
    {
        public float speed = 9f;
        public float hitRadius = 0.4f;
        public float lifetime = 3.2f;
        public string hitSfx = "rangedhit";
        public string hitVfx = "hit";

        int damage;
        float age;
        Vector3 velocity;
        Transform spinner;

        public static HostileProjectile Launch(GameObject prefab, Vector3 origin, Vector3 target, int damage, float speed = 9f)
        {
            if (prefab == null)
                return null;
            var instance = Object.Instantiate(prefab, origin, Quaternion.identity);
            var projectile = instance.GetComponent<HostileProjectile>();
            if (projectile == null)
                projectile = instance.AddComponent<HostileProjectile>();
            projectile.speed = speed;
            projectile.Begin(target, damage);
            return projectile;
        }

        void Begin(Vector3 target, int amount)
        {
            damage = amount;
            Vector3 to = target - transform.position;
            if (to.sqrMagnitude < 0.001f)
                to = Vector3.forward;
            velocity = to.normalized * speed;
            transform.rotation = Quaternion.LookRotation(velocity);
            spinner = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            age += Time.deltaTime;
            if (age > lifetime)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 delta = velocity * Time.deltaTime;
            if (Obstacles.Cast(transform.position, delta, 0.12f, out var blocked))
            {
                VfxPlayer.Instance?.Play(hitVfx, blocked.point);
                Destroy(gameObject);
                return;
            }
            transform.position += delta;
            if (spinner != null)
                spinner.Rotate(0f, 0f, 360f * Time.deltaTime, Space.Self);

            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;
            Vector3 point = player.transform.position + Vector3.up * 0.7f;
            if ((point - transform.position).sqrMagnitude <= hitRadius * hitRadius + 0.35f)
            {
                // The bolt's own sound replaces "playerhit". It only plays when the hit lands, so the invulnerability
                // window after a hit keeps a crowd of ranged slimes from drowning out the music.
                player.TakeDamage(damage, hitSfx);
                VfxPlayer.Instance?.Play(hitVfx, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
