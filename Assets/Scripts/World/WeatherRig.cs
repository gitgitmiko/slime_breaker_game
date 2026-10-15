using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Keeps a weather particle emitter (rain, snow, fireflies, dust) centred above the hero.</summary>
    public class WeatherRig : MonoBehaviour
    {
        public float height = 10f;
        public string ambienceId;

        void Start()
        {
            if (!string.IsNullOrEmpty(ambienceId))
                AudioService.Instance?.SetAmbience(ambienceId);
            Follow();
        }

        void LateUpdate()
        {
            Follow();
        }

        void Follow()
        {
            var hero = PlayerHealth.Instance;
            if (hero == null)
                return;
            Vector3 target = hero.transform.position;
            transform.position = new Vector3(target.x, height, target.z);
        }

        void OnDestroy()
        {
            if (!string.IsNullOrEmpty(ambienceId))
                AudioService.Instance?.SetAmbience(null);
        }
    }
}
