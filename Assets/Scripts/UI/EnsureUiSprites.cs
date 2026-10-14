using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    public class EnsureUiSprites : MonoBehaviour
    {
        static Sprite fallback;

        void Awake()
        {
            if (fallback == null)
            {
                var texture = Texture2D.whiteTexture;
                fallback = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    4f);
                fallback.hideFlags = HideFlags.DontSave;
            }

            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    image.sprite = fallback;
            }
        }
    }
}
