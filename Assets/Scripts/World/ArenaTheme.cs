using System;

namespace SlimeBreaker
{
    public enum Biome
    {
        Forest,
        Snow,
        Desert
    }

    public enum Weather
    {
        Clear,
        Rain,
        Snowfall,
        Heat
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

        public string Title
        {
            get
            {
                string place = biome switch
                {
                    Biome.Snow => "Lembah Salju",
                    Biome.Desert => "Reruntuhan Gurun",
                    _ => "Hutan Rune"
                };
                string sky = weather switch
                {
                    Weather.Rain => "Hujan",
                    Weather.Snowfall => "Badai Salju",
                    Weather.Heat => "Terik",
                    _ => "Cerah"
                };
                return place + " · " + sky + " · " + (IsNight ? "Malam" : "Siang");
            }
        }

        /// <summary>The opening arena is always the calm daytime forest so new players learn the game in a readable space.</summary>
        public static ArenaTheme First(int seed) => new ArenaTheme(Biome.Forest, Weather.Clear, TimeOfDay.Day, seed);

        /// <summary>Rolls the next arena, never repeating the previous biome and only picking weather that fits it.</summary>
        public static ArenaTheme Roll(ArenaTheme previous, Random rng)
        {
            Biome biome;
            do
            {
                biome = (Biome)rng.Next(0, 3);
            }
            while (biome == previous.biome);

            var time = rng.NextDouble() < 0.42 ? TimeOfDay.Night : TimeOfDay.Day;
            Weather weather;
            double roll = rng.NextDouble();
            switch (biome)
            {
                case Biome.Snow:
                    weather = roll < 0.6 ? Weather.Snowfall : Weather.Clear;
                    break;
                case Biome.Desert:
                    weather = roll < 0.55 ? Weather.Heat : Weather.Clear;
                    if (weather == Weather.Heat)
                        time = TimeOfDay.Day;
                    break;
                default:
                    weather = roll < 0.45 ? Weather.Rain : Weather.Clear;
                    break;
            }
            return new ArenaTheme(biome, weather, time, rng.Next(1, int.MaxValue));
        }
    }
}
