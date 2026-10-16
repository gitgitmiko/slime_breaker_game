using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeBreaker
{
    public class DashController : MonoBehaviour
    {
        public bool IsDashing => timeLeft > 0f;
        public bool CanDash => cooldown <= 0f && timeLeft <= 0f;
        public Vector3 Direction { get; private set; } = Vector3.forward;
        public float Cooldown01
        {
            get
            {
                var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
                float max = stats != null ? Mathf.Max(0.01f, stats.dashCooldown) : 1f;
                return Mathf.Clamp01(1f - cooldown / max);
            }
        }

        float timeLeft;
        float cooldown;
        bool queued;
        PlayerController motor;
        PlayerHealth health;

        void Awake()
        {
            motor = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
        }

        void Update()
        {
            float delta = Time.deltaTime;
            if (timeLeft > 0f)
                timeLeft -= delta;
            if (cooldown > 0f)
                cooldown -= delta;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                TryQueue();

            var flow = GameFlowController.Instance;
            if (!queued || flow == null || flow.State != GameState.Playing)
                return;
            if (health != null && health.IsDead)
            {
                queued = false;
                return;
            }
            if (!CanDash)
            {
                queued = false;
                return;
            }

            queued = false;
            var stats = flow.Stats;
            Direction = transform.forward;
            if (motor != null && motor.WishDirection.sqrMagnitude > 0.01f)
                Direction = motor.WishDirection.normalized;
            timeLeft = flow.balance != null ? flow.balance.dashDuration : 0.16f;
            cooldown = stats != null ? stats.dashCooldown : 1.25f;
            health?.GiveInvulnerability(timeLeft);
            var animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetTrigger("Dash");
            AudioService.Instance?.Play("dash");
            VfxPlayer.Instance?.Play("dash", transform.position + Vector3.up * 0.4f);
        }

        public void QueueDash()
        {
            TryQueue();
        }

        void TryQueue()
        {
            if (CanDash)
                queued = true;
        }
    }
}
