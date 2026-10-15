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
        float chillUntil;
        float chillFactor = 1f;
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
        public float MoveScale => Time.time < chillUntil ? chillFactor : 1f;

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

        public void Restore(int hp)
        {
            Max = GameFlowController.Instance != null && GameFlowController.Instance.Stats != null
                ? Mathf.Max(1, GameFlowController.Instance.Stats.maxHealth)
                : Mathf.Max(1, Max);
            Current = Mathf.Clamp(hp, 1, Max);
            IsDead = false;
            Changed?.Invoke();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0 || Current >= Max)
                return;
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke();
        }

        public void Chill(float factor, float seconds)
        {
            if (IsDead || seconds <= 0f)
                return;
            factor = Mathf.Clamp(factor, 0.2f, 1f);
            if (Time.time >= chillUntil || factor < chillFactor)
                chillFactor = factor;
            chillUntil = Mathf.Max(chillUntil, Time.time + seconds);
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
