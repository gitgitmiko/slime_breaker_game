using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    public class UrpSceneBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].GetUniversalAdditionalCameraData();

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                lights[i].GetUniversalAdditionalLightData();
        }
    }
}
