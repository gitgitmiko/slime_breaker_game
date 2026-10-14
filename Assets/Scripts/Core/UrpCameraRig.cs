using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlimeBreaker
{
    [RequireComponent(typeof(Camera))]
    public class UrpCameraRig : MonoBehaviour
    {
        void Awake()
        {
            var camera = GetComponent<Camera>();
            var extra = camera.GetUniversalAdditionalCameraData();
            extra.renderType = CameraRenderType.Base;
            extra.renderShadows = true;
        }
    }
}
