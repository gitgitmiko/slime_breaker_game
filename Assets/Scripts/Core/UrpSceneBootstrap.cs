using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    public class UrpSceneBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // vSync must be off for targetFrameRate to apply; scenes without a GraphicsService still honour the saved level.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = GraphicsService.For(SaveService.Load().graphicsLevel).targetFps;

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
