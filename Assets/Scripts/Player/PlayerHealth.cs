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
        /// <summary>Lives left this run (<see cref="RunRules.HeroLives"/> at the start).</summary>
        public int Lives { get; private set; } = RunRules.HeroLives;
        public event Action Changed;
        /// <summary>Raised when HP hits zero but a life was left: the hero gets back up with full HP.</summary>
        public event Action LifeLost;

        /// <summary>Lifesteal shown so far as green numbers over the hero.</summary>
        public int ShownLifesteal { get; private set; }

        float invulnerableFor;
        int pendingSteal;
        float stealFlush;
        float chillUntil;
        float chillFactor = 1f;
        Animator animator;

        void Awake()
        {
            Instance = this;
            animator = GetComponentInChildren<Animator>();
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            Max = stats != null ? stats.TotalMaxHealth : 100;
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
            if (pendingSteal > 0)
            {
                stealFlush -= Time.unscaledDeltaTime;
                if (stealFlush <= 0f)
                    FlushLifesteal();
            }
        }

        /// <summary>Heals from lifesteal and batches the amount into one green number over the hero's head.</summary>
        /// <summary>Plays a one-shot hero animation (Cast, Victory) if the controller has that trigger.</summary>
        public bool Emote(string trigger)
        {
            if (animator == null || IsDead || animator.runtimeAnimatorController == null)
                return false;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == trigger && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(trigger);
                    LastEmote = trigger;
                    return true;
                }
            }
            return false;
        }

        public string LastEmote { get; private set; }

        public void Lifesteal(int amount)
        {
            int before = Current;
            Heal(amount);
            int healed = Current - before;
            if (healed <= 0)
                return;
            if (pendingSteal == 0)
                stealFlush = 0.25f;
            pendingSteal += healed;
        }

        void FlushLifesteal()
        {
            if (pendingSteal <= 0)
                return;
            DamagePopup.Show(transform.position + Vector3.up * 1.75f, pendingSteal, DamagePopup.Kind.Heal);
            ShownLifesteal += pendingSteal;
            pendingSteal = 0;
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
                ? Mathf.Max(1, GameFlowController.Instance.Stats.TotalMaxHealth)
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
            if (stats != null && stats.TotalDamageReduction > 0f)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - stats.TotalDamageReduction)));
            Current = Mathf.Max(0, Current - amount);
            DamagePopup.Show(transform.position + Vector3.up * 1.35f, amount, DamagePopup.Kind.Hero);
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
                LoseLife();
        }

        void LoseLife()
        {
            Lives = Mathf.Max(0, Lives - 1);
            if (Lives > 0)
            {
                Current = Max;
                GiveInvulnerability(RunRules.ReviveInvulnerability);
                VfxPlayer.Instance?.Play("levelup", transform.position + Vector3.up * 0.6f);
                Changed?.Invoke();
                LifeLost?.Invoke();
                return;
            }
            IsDead = true;
            Changed?.Invoke();
            if (animator != null)
                animator.SetTrigger("Death");
            GameFlowController.Instance?.OutOfLives();
        }

        /// <summary>Back on its feet after a continue: full HP, every life back, a moment of invulnerability.</summary>
        public void Revive()
        {
            IsDead = false;
            Lives = RunRules.HeroLives;
            Current = Max;
            invulnerableFor = RunRules.ReviveInvulnerability;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            VfxPlayer.Instance?.Play("levelup", transform.position + Vector3.up * 0.6f);
            Changed?.Invoke();
        }

        public void RestoreLives(int lives)
        {
            Lives = lives > 0 ? Mathf.Min(lives, RunRules.HeroLives) : RunRules.HeroLives;
            Changed?.Invoke();
        }
    }
}
