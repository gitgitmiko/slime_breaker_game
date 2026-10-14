using UnityEngine;

namespace SlimeBreaker
{
    public class BossController : MonoBehaviour
    {
        public Transform ring;
        public float slamRadius = 3.4f;
        public float arenaRadius = 11.2f;

        enum Pattern
        {
            Idle,
            Telegraph,
            Slam,
            Charge
        }

        Pattern pattern = Pattern.Idle;
        float timer;
        float patternTimer = 1.4f;
        Vector3 chargeDirection;
        bool chargeHit;
        Animator animator;
        EnemyHealth health;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            health = GetComponent<EnemyHealth>();
            if (ring != null)
                ring.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || health == null || health.IsDead)
                return;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;

            timer -= Time.deltaTime;
            switch (pattern)
            {
                case Pattern.Idle:
                    Chase(player.transform.position, health.definition != null ? health.definition.moveSpeed : 2.2f);
                    if (timer <= 0f)
                        BeginTelegraph(player.transform.position);
                    break;
                case Pattern.Telegraph:
                    if (timer <= 0f)
                        Slam(player);
                    break;
                case Pattern.Slam:
                    if (timer <= 0f)
                        BeginCharge(player.transform.position);
                    break;
                case Pattern.Charge:
            transform.position += chargeDirection * 9.5f * Time.deltaTime;
            ClampArena();
            if (!chargeHit && (player.transform.position - transform.position).sqrMagnitude < 1.6f * 1.6f)
            {
                chargeHit = true;
                player.TakeDamage(health.definition != null ? health.definition.contactDamage : 16);
            }
                    if (timer <= 0f)
                    {
                        pattern = Pattern.Idle;
                        timer = patternTimer;
                        if (animator != null)
                            animator.SetFloat("Speed", 0f);
                    }
                    break;
            }
        }

        void Chase(Vector3 target, float speed)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 4f)
            {
                transform.position += to.normalized * speed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                if (animator != null)
                    animator.SetFloat("Speed", 1f);
            }
            else if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }
            ClampArena();
        }

        void BeginTelegraph(Vector3 playerPosition)
        {
            pattern = Pattern.Telegraph;
            timer = 0.95f;
            if (ring != null)
            {
                ring.gameObject.SetActive(true);
                ring.localScale = new Vector3(0.2f, 1f, 0.2f);
            }
            if (animator != null)
                animator.SetTrigger("Telegraph");
            AudioService.Instance?.Play("bosswarn");
            Vector3 to = playerPosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        void Slam(PlayerHealth player)
        {
            pattern = Pattern.Slam;
            timer = 0.45f;
            if (ring != null)
                ring.localScale = new Vector3(slamRadius, 1f, slamRadius);
            if (animator != null)
                animator.SetTrigger("Slam");
            AudioService.Instance?.Play("slam");
            VfxPlayer.Instance?.Play("slam", transform.position);
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= slamRadius)
                player.TakeDamage(health.definition != null ? health.definition.contactDamage + 6 : 20);
        }

        void BeginCharge(Vector3 playerPosition)
        {
            pattern = Pattern.Charge;
            timer = 0.55f;
            chargeHit = false;
            if (ring != null)
                ring.gameObject.SetActive(false);
            chargeDirection = playerPosition - transform.position;
            chargeDirection.y = 0f;
            if (chargeDirection.sqrMagnitude < 0.01f)
                chargeDirection = transform.forward;
            chargeDirection.Normalize();
            transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);
            if (animator != null)
            {
                animator.SetTrigger("Attack");
                animator.SetFloat("Speed", 1f);
            }
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
