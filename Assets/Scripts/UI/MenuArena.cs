using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// The menu backdrop follows the player's history: it is the arena (biome, weather, time of day) of the furthest
    /// floor reached, built by the same generator as the game. Floor 1 and a fresh save give the calm daytime forest.
    /// </summary>
    public class MenuArena : MonoBehaviour
    {
        public ArenaGenerator generator;
        public Transform showcase;
        public int seed = 2024;

        void Start()
        {
            Build(SaveService.Load().highestFloor);
        }

        public ArenaTheme Build(int floor)
        {
            var theme = ArenaTheme.ForFloor(floor, seed);
            if (generator != null)
                generator.Generate(theme);
            if (theme.IsNight && showcase != null)
            {
                // Like in the game, the hero (and the beaten bosses around it) carry a warm light after dark.
                var lightObject = new GameObject("ShowcaseLight");
                lightObject.transform.SetParent(showcase, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.0f, 1.8f);
                var glow = lightObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.range = 12f;
                glow.intensity = 5f;
                glow.color = new Color(1f, 0.85f, 0.6f);
                glow.shadows = LightShadows.None;
            }
            return theme;
        }
    }
}
