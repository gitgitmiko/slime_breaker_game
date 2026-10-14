using UnityEngine;

namespace SlimeBreaker
{
    public class XPOrb : MonoBehaviour
    {
        public int amount = 3;
        public float magnetRadius = 4.5f;
        public float speed = 9f;

        public static void Spawn(GameObject prefab, Vector3 position, int xp)
        {
            var instance = Instantiate(prefab, position, Quaternion.identity);
            var orb = instance.GetComponent<XPOrb>();
            if (orb == null)
                orb = instance.AddComponent<XPOrb>();
            orb.amount = xp;
        }

        void Update()
        {
            if (!GameFlowController.IsPlaying)
                return;
            var player = PlayerHealth.Instance;
            if (player == null)
                return;
            Vector3 to = player.transform.position + Vector3.up * 0.6f - transform.position;
            float distance = to.magnitude;
            if (distance < 0.7f)
            {
                XPManager.Instance?.Add(amount);
                AudioService.Instance?.Play("pickup");
                Destroy(gameObject);
                return;
            }
            if (distance < magnetRadius)
                transform.position += to.normalized * speed * Time.deltaTime;
            else
                transform.position += Vector3.up * Mathf.Sin(Time.time * 4f) * 0.15f * Time.deltaTime;
        }
    }
}
