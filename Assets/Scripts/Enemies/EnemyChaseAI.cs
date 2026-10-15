using UnityEngine;

namespace SlimeBreaker
{
    public class EnemyChaseAI : MonoBehaviour
    {
        public float arenaRadius = RunRules.ArenaRadius;
        public GameObject boltPrefab;
        float contactTimer;
        Animator animator;
        EnemyHealth health;

        void Awake()
        {
            health = GetComponent<EnemyHealth>();
            animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || health == null || health.IsDead || health.definition == null)
                return;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;

            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            var definition = health.definition;
            if (definition.isRanged)
                TickRanged(player, to, distance);
            else
                TickMelee(player, to, distance);
            ClampArena();
        }

        void TickMelee(PlayerHealth player, Vector3 to, float distance)
        {
            var definition = health.definition;
            if (distance > definition.contactRange * 0.85f && distance > 0.05f)
            {
                MoveToward(to.normalized, definition.moveSpeed * health.SpeedMultiplier);
            }
            else
            {
                Idle();
            }

            contactTimer -= Time.deltaTime;
            if (distance <= definition.contactRange && contactTimer <= 0f)
            {
                player.TakeDamage(health.ContactDamage);
                contactTimer = definition.contactCooldown;
            }
        }

        void TickRanged(PlayerHealth player, Vector3 to, float distance)
        {
            var definition = health.definition;
            float preferred = Mathf.Max(3.5f, definition.contactRange);
            Vector3 move = Vector3.zero;
            if (distance < preferred * 0.72f && distance > 0.05f)
                move = -to.normalized;
            else if (distance > preferred)
                move = to.normalized;

            if (move.sqrMagnitude > 0.01f)
                MoveToward(move, definition.moveSpeed * health.SpeedMultiplier);
            else
                Idle();

            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 10f * Time.deltaTime);

            contactTimer -= Time.deltaTime;
            if (distance <= preferred * 1.2f && contactTimer <= 0f)
            {
                Vector3 origin = transform.position + Vector3.up * health.HitHeight;
                Vector3 aim = player.transform.position + Vector3.up * 0.7f;
                if (!Obstacles.Blocked(origin, aim))
                    HostileProjectile.Launch(boltPrefab, origin, aim, health.ContactDamage, 8.5f);
                contactTimer = definition.contactCooldown;
                AudioService.Instance?.Play("cast");
            }
        }

        void MoveToward(Vector3 direction, float speed)
        {
            Vector3 steer = Obstacles.Steer(transform.position, direction, 0.38f, 1.5f);
            transform.position += steer * speed * Time.deltaTime;
            transform.position = Obstacles.Resolve(transform.position, 0.38f);
            if (steer.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(steer.normalized, Vector3.up), 10f * Time.deltaTime);
            if (animator != null)
            {
                animator.SetFloat("Speed", 1f);
                animator.speed = Mathf.Lerp(0.55f, 1f, health.SpeedMultiplier);
            }
        }

        void Idle()
        {
            if (animator == null)
                return;
            animator.SetFloat("Speed", 0f);
            animator.speed = 1f;
        }

        void ClampArena()
        {
            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.magnitude > arenaRadius)
                flat = flat.normalized * arenaRadius;
            transform.position = new Vector3(flat.x, 0f, flat.z);
        }
    }
}
