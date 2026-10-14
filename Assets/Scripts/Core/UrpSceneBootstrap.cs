using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    public class UrpSceneBootstrap : MonoBehaviour
    {
        public int targetFrameRate = 60;

        void Awake()
        {
            // Mobile defaults to 30 fps; vSync must be off for targetFrameRate to apply.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                cameras[i].GetUniversalAdditionalCameraData();
                if (cameras[i].GetComponent<AudioListener>() == null && cameras[i].CompareTag("MainCamera"))
                    cameras[i].gameObject.AddComponent<AudioListener>();
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                lights[i].GetUniversalAdditionalLightData();
        }
    }
}
