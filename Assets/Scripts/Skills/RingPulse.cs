using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Scales a ring mesh outward and removes it; used for the fire nova shockwave.</summary>
    public class RingPulse : MonoBehaviour
    {
        public float targetRadius = 3f;
        public float duration = 0.5f;
        public float startRadius = 0.4f;

        float age;

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            float eased = 1f - (1f - t) * (1f - t);
            float radius = Mathf.Lerp(startRadius, targetRadius, eased);
            float thickness = Mathf.Lerp(1f, 0.15f, t);
            transform.localScale = new Vector3(radius, thickness, radius);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
