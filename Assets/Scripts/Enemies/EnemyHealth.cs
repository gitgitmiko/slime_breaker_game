using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class EnemyHealth : MonoBehaviour
    {
        public static readonly List<EnemyHealth> Active = new List<EnemyHealth>();
        public static event Action<EnemyHealth> AnyDied;

        public EnemyDefinition definition;
        public GameObject xpOrbPrefab;
        public int Current { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsBoss => definition != null && definition.isBoss;

        Animator animator;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            Current = definition != null ? definition.maxHealth : 20;
        }

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
                return;
            Current = Mathf.Max(0, Current - amount);
            if (animator != null)
                animator.SetTrigger("Hit");
            AudioService.Instance?.Play("hit");
            if (Current > 0)
                return;
            IsDead = true;
            if (animator != null)
                animator.SetTrigger("Death");
            AnyDied?.Invoke(this);
            if (xpOrbPrefab != null && definition != null && definition.xp > 0)
                XPOrb.Spawn(xpOrbPrefab, transform.position + Vector3.up * 0.4f, definition.xp);
            var chase = GetComponent<EnemyChaseAI>();
            if (chase != null)
                chase.enabled = false;
            var boss = GetComponent<BossController>();
            if (boss != null)
                boss.enabled = false;
            Destroy(gameObject, 0.55f);
        }
    }
}
