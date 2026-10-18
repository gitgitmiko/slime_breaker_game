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
        /// <summary>Orbit around the hero, in degrees; set by dragging the right half of the screen (movement is camera-relative, so it follows).</summary>
        public float Yaw { get; private set; }
        /// <summary>Extra tilt on top of the default camera elevation (positive = more top-down).</summary>
        public float Pitch { get; private set; }
        public float minElevation = 32f;
        public float maxElevation = 78f;
        /// <summary>Orbit angles ease toward the dragged target so touch jitter never shakes the view.</summary>
        public float orbitSmoothing = 14f;
        float targetYaw;
        float targetPitch;
        Vector3 smoothFocus;
        bool hasFocus;

        Vector3 lead;
        Vector3 lastTargetPosition;
        float shake;
        Transform storedTarget;
        Vector3 storedOffset;
        float storedLookHeight;
        float focusLeft;
        bool focusing;
        Vector3 focusOffset;
        float focusLookHeight;
        static readonly Vector3 CinematicOffset = new Vector3(0f, 4.8f, -5.1f);
        /// <summary>Tight, low angle used for the boss enrage cutscene.</summary>
        public static readonly Vector3 CloseUpOffset = new Vector3(0f, 2.6f, -3.6f);

        public bool IsFocusing => focusing;
        public Transform FocusTarget => focusing ? target : null;

        void Awake()
        {
            TargetCamera = GetComponent<Camera>();
            if (target != null)
                lastTargetPosition = target.position;
            Snap(1f);
        }

        public void Focus(Transform subject, float seconds)
        {
            Focus(subject, seconds, CinematicOffset, lookHeight);
        }

        /// <summary>Cinematic: frames <paramref name="subject"/> from <paramref name="cameraOffset"/> for a few unscaled seconds, then returns to the hero.</summary>
        public void Focus(Transform subject, float seconds, Vector3 cameraOffset, float look)
        {
            if (subject == null || seconds <= 0f)
                return;
            if (!focusing)
            {
                storedTarget = target;
                storedOffset = offset;
                storedLookHeight = lookHeight;
            }
            focusOffset = cameraOffset;
            focusLookHeight = look;
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
                float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
                offset = Vector3.Lerp(offset, focusOffset, blend);
                lookHeight = Mathf.Lerp(lookHeight, focusLookHeight, blend);
                if (focusLeft <= 0f)
                {
                    focusing = false;
                    target = storedTarget != null ? storedTarget : target;
                    offset = storedOffset;
                    lookHeight = storedLookHeight;
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
            float ease = 1f - Mathf.Exp(-orbitSmoothing * dt);
            Yaw = Mathf.Lerp(Yaw, targetYaw, ease);
            Pitch = Mathf.Lerp(Pitch, targetPitch, ease);
            Snap(1f - Mathf.Exp(-follow * dt));
            if (shake > 0f)
            {
                shake -= dt;
                transform.position += Random.insideUnitSphere * shake * 0.25f;
            }
        }

        public float TargetYaw => targetYaw;

        public void Rotate(float degrees)
        {
            targetYaw += degrees;
        }

        public void Tilt(float degrees)
        {
            float baseElevation = Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch + degrees, minElevation - baseElevation, maxElevation - baseElevation);
        }

        public void Shake(float seconds)
        {
            shake = Mathf.Max(shake, seconds);
        }

        void Snap(float t)
        {
            if (target == null)
                return;
            // Only the focus point lags behind the hero; the camera is rigidly placed around it from the orbit angles,
            // so turning never swings the view along a lagging arc.
            var wanted = target.position + lead;
            smoothFocus = hasFocus ? Vector3.Lerp(smoothFocus, wanted, t) : wanted;
            hasFocus = true;
            var focus = smoothFocus;
            float distance = offset.magnitude;
            float elevation = Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
            elevation = Mathf.Clamp(elevation + Pitch, minElevation, maxElevation) * Mathf.Deg2Rad;
            var flat = new Vector3(offset.x, 0f, offset.z);
            flat = flat.sqrMagnitude < 1e-6 ? Vector3.back : flat.normalized;
            flat = Quaternion.Euler(0f, Yaw, 0f) * flat;
            transform.position = focus + (flat * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation)) * distance;
            var look = focus + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
        }
    }
}
