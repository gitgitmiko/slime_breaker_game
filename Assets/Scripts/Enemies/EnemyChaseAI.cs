using UnityEngine;

namespace SlimeBreaker
{
    public class EnemyChaseAI : MonoBehaviour
    {
        public float arenaRadius = 11.2f;
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
            if (distance > definition.contactRange * 0.85f && distance > 0.05f)
            {
                float speed = definition.moveSpeed * health.SpeedMultiplier;
                Vector3 steer = Obstacles.Steer(transform.position, to.normalized, 0.38f, 1.5f);
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
            else if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.speed = 1f;
            }

            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.magnitude > arenaRadius)
                flat = flat.normalized * arenaRadius;
            transform.position = new Vector3(flat.x, 0f, flat.z);

            contactTimer -= Time.deltaTime;
            if (distance <= definition.contactRange && contactTimer <= 0f)
            {
                player.TakeDamage(health.ContactDamage);
                contactTimer = definition.contactCooldown;
            }
        }
    }
}
