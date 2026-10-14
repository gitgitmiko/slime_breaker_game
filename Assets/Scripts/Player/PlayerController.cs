using UnityEngine;
using UnityEngine.InputSystem;

namespace SlimeBreaker
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public VirtualJoystick joystick;
        public FollowCamera followCamera;
        public float arenaRadius = 11.2f;

        CharacterController body;
        DashController dash;
        Animator animator;
        Vector3 wish;

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

            float speed = stats != null ? stats.moveSpeed : 5f;
            Vector3 velocity = wish * speed;
            if (dash != null && dash.IsDashing)
                velocity = dash.Direction * (flow.balance != null ? flow.balance.dashSpeed : 16f);

            velocity.y = -6f;
            body.Move(velocity * Time.deltaTime);

            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.magnitude > arenaRadius)
            {
                flat = flat.normalized * arenaRadius;
                transform.position = new Vector3(flat.x, transform.position.y, flat.z);
            }

            Vector3 face = dash != null && dash.IsDashing ? dash.Direction : wish;
            if (face.sqrMagnitude > 0.001f)
            {
                var look = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 14f * Time.deltaTime);
            }

            if (animator != null)
                animator.SetFloat("Speed", dash != null && dash.IsDashing ? 1f : wish.magnitude);
        }

        public Vector3 WishDirection => wish;

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
