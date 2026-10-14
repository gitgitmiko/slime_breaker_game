using System;
using UnityEngine;

namespace SlimeBreaker
{
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsDead { get; private set; }
        public event Action Changed;

        float invulnerableFor;
        Animator animator;

        void Awake()
        {
            Instance = this;
            animator = GetComponentInChildren<Animator>();
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            Max = stats != null ? stats.maxHealth : 100;
            Current = Max;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (invulnerableFor > 0f)
                invulnerableFor -= Time.deltaTime;
        }

        public bool IsInvulnerable => invulnerableFor > 0f || IsDead;

        public void GiveInvulnerability(float seconds)
        {
            if (seconds > invulnerableFor)
                invulnerableFor = seconds;
        }

        public void SetMaxHealth(int max, int heal)
        {
            Max = Mathf.Max(1, max);
            Current = Mathf.Min(Max, Current + Mathf.Max(0, heal));
            Changed?.Invoke();
        }

        public void TakeDamage(int amount)
        {
            if (IsInvulnerable || amount <= 0)
                return;
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            if (stats != null && stats.damageReduction > 0f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Mathf.Clamp01(stats.damageReduction))));
            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke();
            if (animator != null)
                animator.SetTrigger("Hit");
            AudioService.Instance?.Play("playerhit");
            VfxPlayer.Instance?.Play("playerhit", transform.position + Vector3.up * 0.8f);
            var camera = Camera.main != null ? Camera.main.GetComponent<FollowCamera>() : null;
            if (camera != null)
                camera.Shake(0.18f);
            GiveInvulnerability(0.35f);
            if (Current <= 0)
            {
                IsDead = true;
                if (animator != null)
                    animator.SetTrigger("Death");
                GameFlowController.Instance?.EndRun(false);
            }
        }
    }
}
