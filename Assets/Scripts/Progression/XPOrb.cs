using UnityEngine;

namespace SlimeBreaker
{
    public class XPOrb : MonoBehaviour
    {
        public int amount = 3;
        public float magnetRadius = 4.5f;
        public float speed = 9f;
        public float hoverHeight = 0.45f;

        float age;
        float phase;
        Vector3 pop;

        public static void Spawn(GameObject prefab, Vector3 position, int xp)
        {
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var orb = instance.GetComponent<XPOrb>();
            if (orb == null)
                orb = instance.AddComponent<XPOrb>();
            orb.amount = xp;
            // Small outward pop so clustered drops spread instead of stacking in one spot.
            var dir = Random.insideUnitCircle * 1.6f;
            orb.pop = new Vector3(dir.x, 0f, dir.y);
        }

        void Awake()
        {
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            float dt = Time.deltaTime;
            age += dt;
            transform.Rotate(0f, 160f * dt, 0f, Space.World);

            var player = PlayerHealth.Instance;
            Vector3 position = transform.position;
            if (age < 0.35f)
                position += pop * (1f - age / 0.35f) * dt;

            if (player != null && !player.IsDead)
            {
                Vector3 to = player.transform.position + Vector3.up * 0.7f - position;
                float distance = to.magnitude;
                if (distance < 0.75f)
                {
                    XPManager.Instance?.Add(amount);
                    AudioService.Instance?.Play("pickup");
                    VfxPlayer.Instance?.Play("pickup", position);
                    Destroy(gameObject);
                    return;
                }
                if (distance < magnetRadius)
                {
                    float pull = speed * Mathf.Lerp(1.6f, 0.8f, distance / magnetRadius);
                    transform.position = position + to.normalized * pull * dt;
                    return;
                }
            }

            float targetY = hoverHeight + Mathf.Sin(age * 3f + phase) * 0.08f;
            position.y = Mathf.Lerp(position.y, targetY, 1f - Mathf.Exp(-6f * dt));
            transform.position = position;
        }
    }
}
