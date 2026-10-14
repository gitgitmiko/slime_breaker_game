using System.Collections;
using UnityEngine;

namespace SlimeBreaker
{
    public class AutoAttackController : MonoBehaviour
    {
        float cooldown;
        bool swinging;
        Animator animator;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying || swinging)
                return;
            var health = GetComponent<PlayerHealth>();
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
            StartCoroutine(Strike(target, stats.damage, stats.attackRange));
        }

        EnemyHealth FindNearest(float range)
        {
            EnemyHealth best = null;
            float bestDistance = range * range;
            Vector3 origin = transform.position;
            for (int i = 0; i < EnemyHealth.Active.Count; i++)
            {
                var enemy = EnemyHealth.Active[i];
                if (enemy == null || enemy.IsDead)
                    continue;
                float distance = (enemy.transform.position - origin).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        IEnumerator Strike(EnemyHealth target, int damage, float range)
        {
            swinging = true;
            if (target != null)
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
            if (animator != null)
                animator.SetTrigger("Attack");
            yield return new WaitForSeconds(0.12f);
            if (target != null && !target.IsDead)
            {
                float limit = range + 0.8f;
                if ((target.transform.position - transform.position).sqrMagnitude <= limit * limit)
                {
                    target.TakeDamage(damage);
                    VfxPlayer.Instance?.Play("hit", target.transform.position + Vector3.up * 0.6f);
                }
            }
            swinging = false;
        }
    }
}
