using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeBreaker
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public VirtualJoystick joystick;
        public FollowCamera followCamera;
        public float arenaRadius = RunRules.ArenaRadius;

        CharacterController body;
        DashController dash;
        Animator animator;
        Vector3 wish;
        bool scripted;
        Vector3 scriptedTarget;
        float scriptedSpeed;

        /// <summary>True until a scripted walk (through an arena door) has reached its target.</summary>
        public bool IsWalking => scripted && (transform.position - scriptedTarget).sqrMagnitude > 0.0004f;

        /// <summary>Takes over the hero and moves them to <paramref name="target"/>, ignoring input and the arena edge.</summary>
        public void WalkTo(Vector3 target, float speed)
        {
            scripted = true;
            scriptedTarget = target;
            scriptedSpeed = speed;
            if (body != null)
                body.enabled = false;
        }

        /// <summary>Teleports the hero to <paramref name="position"/> and keeps them there until the next walk.</summary>
        public void HoldAt(Vector3 position)
        {
            WalkTo(position, 0f);
            transform.position = position;
        }

        public void ReleaseWalk()
        {
            scripted = false;
            if (body != null)
                body.enabled = true;
            if (animator != null)
                animator.SetFloat("Speed", 0f);
        }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            dash = GetComponent<DashController>();
            animator = GetComponentInChildren<Animator>();
        }

        void Update()
        {
            var flow = GameFlowController.Instance;
            if (flow == null || flow.State != GameState.Playing)
                return;
            var health = GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
                return;
            if (scripted)
            {
                MoveScripted();
                return;
            }

            var stats = flow.Stats;
            Vector2 input = ReadMove();
            var cameraTransform = followCamera != null && followCamera.TargetCamera != null
                ? followCamera.TargetCamera.transform
                : Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            wish = forward * input.y + right * input.x;
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();

            float speed = (stats != null ? stats.TotalMoveSpeed : 5f) * (health != null ? health.MoveScale : 1f);
            Vector3 velocity = wish * speed;
            if (dash != null && dash.IsDashing)
                velocity = dash.Direction * (flow.balance != null ? flow.balance.dashSpeed : 16f);

            velocity.y = 0f;
            body.Move(velocity * Time.deltaTime);
            transform.position = Obstacles.Resolve(transform.position, 0.34f);
            StickToArena();

            Vector3 face = dash != null && dash.IsDashing ? dash.Direction : wish;
            if (face.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 14f * Time.deltaTime);
            }

            if (animator != null)
                animator.SetFloat("Speed", dash != null && dash.IsDashing ? 1f : wish.magnitude);
        }

        void MoveScripted()
        {
            wish = Vector3.zero;
            Vector3 to = scriptedTarget - transform.position;
            to.y = 0f;
            bool moving = scriptedSpeed > 0f && to.sqrMagnitude > 0.0004f;
            if (moving)
            {
                transform.position = Vector3.MoveTowards(transform.position, scriptedTarget, scriptedSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 14f * Time.deltaTime);
            }
            if (animator != null)
                animator.SetFloat("Speed", moving ? 1f : 0f);
        }

        public Vector3 WishDirection => wish;
        public bool IsMoving => wish.sqrMagnitude > 0.04f || (dash != null && dash.IsDashing);

        void StickToArena()
        {
            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(pos.x, 0f, pos.z);
            if (flat.magnitude > arenaRadius)
                flat = flat.normalized * arenaRadius;
            float y = pos.y;
            if (y < 0f || y > 0.25f)
                y = 0f;
            var grounded = new Vector3(flat.x, y, flat.z);
            if ((pos - grounded).sqrMagnitude > 0.0000001f)
                transform.position = grounded;
        }

        Vector2 ReadMove()
        {
            Vector2 stick = joystick != null ? joystick.Direction : Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return stick;
            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            var keys = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            return keys.sqrMagnitude > stick.sqrMagnitude ? keys : stick;
        }
    }
}
