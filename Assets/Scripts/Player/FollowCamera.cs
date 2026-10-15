using UnityEngine;

namespace SlimeBreaker
{
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 8.3f, -6.9f);
        public float lookHeight = 0.9f;
        public float follow = 7f;
        /// <summary>How far the camera leans toward the hero's travel direction so there is more room to see ahead.</summary>
        public float leadDistance = 1.1f;
        public Camera TargetCamera { get; private set; }

        Vector3 lead;
        Vector3 lastTargetPosition;
        float shake;
        Transform storedTarget;
        Vector3 storedOffset;
        float focusLeft;
        bool focusing;
        static readonly Vector3 CinematicOffset = new Vector3(0f, 4.8f, -5.1f);

        void Awake()
        {
            TargetCamera = GetComponent<Camera>();
            if (target != null)
                lastTargetPosition = target.position;
            Snap(1f);
        }

        public void Focus(Transform subject, float seconds)
        {
            if (subject == null || seconds <= 0f)
                return;
            if (!focusing)
            {
                storedTarget = target;
                storedOffset = offset;
            }
            target = subject;
            focusing = true;
            focusLeft = seconds;
            lastTargetPosition = subject.position;
            lead = Vector3.zero;
        }

        void LateUpdate()
        {
            if (focusing)
            {
                focusLeft -= Time.unscaledDeltaTime;
                offset = Vector3.Lerp(offset, CinematicOffset, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
                if (focusLeft <= 0f)
                {
                    focusing = false;
                    target = storedTarget != null ? storedTarget : target;
                    offset = storedOffset;
                    if (target != null)
                        lastTargetPosition = target.position;
                    lead = Vector3.zero;
                }
            }
            if (target == null)
                return;
            float dt = Time.unscaledDeltaTime;
            Vector3 velocity = dt > 0f ? (target.position - lastTargetPosition) / dt : Vector3.zero;
            lastTargetPosition = target.position;
            velocity.y = 0f;
            Vector3 wantedLead = Vector3.ClampMagnitude(velocity * 0.22f, leadDistance);
            lead = Vector3.Lerp(lead, wantedLead, 1f - Mathf.Exp(-3f * dt));
            Snap(1f - Mathf.Exp(-follow * dt));
            if (shake > 0f)
            {
                shake -= dt;
                transform.position += Random.insideUnitSphere * shake * 0.25f;
            }
        }

        public void Shake(float seconds)
        {
            shake = Mathf.Max(shake, seconds);
        }

        void Snap(float t)
        {
            if (target == null)
                return;
            var focus = target.position + lead;
            var desired = focus + offset;
            transform.position = Vector3.Lerp(transform.position, desired, t);
            var look = focus + Vector3.up * lookHeight;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(look - transform.position, Vector3.up),
                t);
        }
    }
}
