using System;
using UnityEngine;

namespace SlimeBreaker
{
    public enum Biome
    {
        Forest,
        Snow,
        Desert,
        Thunder,
        Swamp,
        Shadow,
        Volcano,
        Storm,
        Crystal,
        Royal
    }

    public enum Weather
    {
        Clear,
        Rain,
        Snowfall,
        Heat,
        Ash,
        Spores,
        Sparks
    }

    public enum TimeOfDay
    {
        Day,
        Night
    }

    /// <summary>One arena look: biome × weather × time of day plus the seed used to scatter its props.</summary>
    [Serializable]
    public struct ArenaTheme
    {
        public Biome biome;
        public Weather weather;
        public TimeOfDay time;
        public int seed;

        public ArenaTheme(Biome biome, Weather weather, TimeOfDay time, int seed)
        {
            this.biome = biome;
            this.weather = weather;
            this.time = time;
            this.seed = seed;
        }

        public bool IsNight => time == TimeOfDay.Night;

        public string PlaceName => biome switch
        {
            Biome.Desert => "Reruntuhan Gurun",
            Biome.Snow => "Lembah Salju",
            Biome.Thunder => "Padang Petir",
            Biome.Swamp => "Rawa Beracun",
            Biome.Shadow => "Lembah Bayangan",
            Biome.Volcano => "Kaldera Magma",
            Biome.Storm => "Puncak Badai",
            Biome.Crystal => "Gua Kristal",
            Biome.Royal => "Taman Kerajaan",
            _ => "Hutan Rune"
        };

        public string WeatherName => weather switch
        {
            Weather.Rain => "Hujan",
            Weather.Snowfall => "Badai Salju",
            Weather.Heat => "Terik",
            Weather.Ash => "Hujan Abu",
            Weather.Spores => "Spora",
            Weather.Sparks => "Kilat",
            _ => "Cerah"
        };

        public string Title => PlaceName + " · " + WeatherName + " · " + (IsNight ? "Malam" : "Siang");

        public static int BiomeCount => 10;

        public static Biome BiomeAtFloor(int floor)
        {
            int band = Mathf.Clamp((Mathf.Max(1, floor) - 1) / RunRules.BossEvery, 0, BiomeCount - 1);
            return band switch
            {
                1 => Biome.Desert,
                2 => Biome.Snow,
                3 => Biome.Thunder,
                4 => Biome.Swamp,
                5 => Biome.Shadow,
                6 => Biome.Volcano,
                7 => Biome.Storm,
                8 => Biome.Crystal,
                9 => Biome.Royal,
                _ => Biome.Forest
            };
        }

        public static Weather[] WeathersFor(Biome biome)
        {
            return biome switch
            {
                Biome.Snow => new[] { Weather.Clear, Weather.Snowfall },
                Biome.Desert => new[] { Weather.Clear, Weather.Heat },
                Biome.Thunder => new[] { Weather.Rain, Weather.Sparks, Weather.Clear },
                Biome.Swamp => new[] { Weather.Spores, Weather.Rain, Weather.Clear },
                Biome.Shadow => new[] { Weather.Clear, Weather.Ash },
                Biome.Volcano => new[] { Weather.Ash, Weather.Heat },
                Biome.Storm => new[] { Weather.Rain, Weather.Clear },
                Biome.Crystal => new[] { Weather.Clear, Weather.Sparks },
                Biome.Royal => new[] { Weather.Clear, Weather.Rain },
                _ => new[] { Weather.Clear, Weather.Rain }
            };
        }

        /// <summary>
        /// Floor 1 is always a calm daytime forest. Later floors keep the 10-floor biome; weather and time of day are
        /// rolled independently from the run seed (so rain can fall by day or night), and the weather never repeats
        /// on two consecutive floors of the same biome. The same run seed and floor always give the same theme.
        /// </summary>
        public static ArenaTheme ForFloor(int floor, int runSeed, ArenaTheme previous = default)
        {
            floor = Mathf.Clamp(floor, 1, RunRules.MaxFloor);
            var biome = BiomeAtFloor(floor);
            int seed = unchecked(runSeed * 397 ^ floor * 104729);
            if (seed == 0)
                seed = floor;
            if (floor == 1)
                return new ArenaTheme(biome, Weather.Clear, TimeOfDay.Day, seed);
            var rng = new System.Random(unchecked(seed * 31 + 17));
            var weathers = WeathersFor(biome);
            var weather = weathers[rng.Next(weathers.Length)];
            if (previous.biome == biome && weather == previous.weather && weathers.Length > 1)
                weather = weathers[(System.Array.IndexOf(weathers, weather) + 1 + rng.Next(weathers.Length - 1)) % weathers.Length];
            // Slightly more days than nights so the HD environment is mostly seen in daylight.
            var time = rng.NextDouble() < 0.6 ? TimeOfDay.Day : TimeOfDay.Night;
            return new ArenaTheme(biome, weather, time, seed);
        }

        /// <summary>The opening arena is always the calm daytime forest so new players learn the game in a readable space.</summary>
        public static ArenaTheme First(int seed) => ForFloor(1, seed);

        /// <summary>Kept for older call sites; new floors use <see cref="ForFloor"/>.</summary>
        public static ArenaTheme Roll(ArenaTheme previous, System.Random rng)
        {
            int seed = rng != null ? rng.Next(1, int.MaxValue) : 2024;
            int floor = previous.seed == 0 ? 2 : 2;
            return ForFloor(floor, seed, previous);
        }
    }
}
