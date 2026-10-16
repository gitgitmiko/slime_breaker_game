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
            // Post-processing (tonemapping, bloom, grading) comes from the PostFx volume; Low turns it off.
            extra.renderPostProcessing = GraphicsService.Instance == null || GraphicsService.Instance.Level != GraphicsService.Low;
            extra.antialiasing = AntialiasingMode.None;
        }
    }
}
