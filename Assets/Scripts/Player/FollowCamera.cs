using UnityEngine;

namespace SlimeBreaker
{
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 11.5f, -9.5f);
        public float follow = 6f;
        public Camera TargetCamera { get; private set; }

        void Awake()
        {
            TargetCamera = GetComponent<Camera>();
            Snap(1f);
        }

        void LateUpdate()
        {
            Snap(1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));
        }

        void Snap(float t)
        {
            if (target == null)
                return;
            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, t);
            var look = target.position + Vector3.up * 0.8f;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(look - transform.position, Vector3.up),
                t);
        }
    }
}
