using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>On-screen arrows that point at the floor portal and the active power-up.</summary>
    public class Wayfinder : MonoBehaviour
    {
        public RectTransform portalArrow;
        public RectTransform powerArrow;
        public Text portalLabel;
        public Text powerLabel;

        void LateUpdate()
        {
            Point(portalArrow, portalLabel, ExitPortal.Active != null ? ExitPortal.Active.transform : null, "Pintu");
            Transform power = null;
            if (PowerUpPickup.Active.Count > 0 && PowerUpPickup.Active[0] != null)
                power = PowerUpPickup.Active[0].transform;
            Point(powerArrow, powerLabel, power, "Power-up");
        }

        void Point(RectTransform arrow, Text label, Transform target, string caption)
        {
            if (arrow == null)
                return;
            var camera = Camera.main;
            var hero = PlayerHealth.Instance;
            if (target == null || camera == null || hero == null)
            {
                arrow.gameObject.SetActive(false);
                return;
            }
            Vector3 to = target.position - hero.transform.position;
            to.y = 0f;
            Vector3 view = camera.WorldToViewportPoint(target.position);
            bool behind = view.z < 0f;
            bool onScreen = !behind && view.x > 0.18f && view.x < 0.82f && view.y > 0.18f && view.y < 0.82f;
            if (onScreen && to.magnitude < 5.5f)
            {
                arrow.gameObject.SetActive(false);
                return;
            }
            if (behind)
            {
                view.x = 1f - view.x;
                view.y = 1f - view.y;
            }
            Vector2 centered = new Vector2(view.x - 0.5f, view.y - 0.5f);
            if (centered.sqrMagnitude < 0.0001f)
                centered = Vector2.up * 0.4f;
            float max = 0.38f;
            if (centered.magnitude > max)
                centered = centered.normalized * max;
            arrow.gameObject.SetActive(true);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f) + centered;
            arrow.anchoredPosition = Vector2.zero;
            arrow.localRotation = Quaternion.identity;
            float angle = Mathf.Atan2(centered.y, centered.x) * Mathf.Rad2Deg - 90f;
            var glyph = arrow.Find("Glyph") as RectTransform;
            if (glyph != null)
                glyph.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (label != null)
                label.text = caption;
        }
    }
}
