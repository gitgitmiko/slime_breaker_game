using UnityEngine;

namespace SlimeBreaker
{
    public enum BossKind
    {
        None = 0,
        Rock = 1,
        Flame = 2,
        Frost = 3,
        Volt = 4,
        Toxic = 5,
        Shadow = 6,
        Magma = 7,
        Storm = 8,
        Crystal = 9,
        Royal = 10
    }

    public static class BossCatalog
    {
        public static BossKind KindAtFloor(int floor)
        {
            int tier = RunRules.BossTier(floor);
            return tier <= 0 ? BossKind.None : (BossKind)Mathf.Clamp(tier, 1, 10);
        }

        public static int HealthForFloor(int floor)
        {
            int tier = Mathf.Max(1, RunRules.BossTier(floor));
            return Difficulty.Scale(tier * RunRules.BossHealthPerTier, Difficulty.Profile.bossHealth);
        }

        public static int EnrageSkillCount(int floor)
        {
            return Mathf.Clamp(RunRules.BossTier(floor), 1, 10);
        }

        public static int ContactDamageForFloor(int floor)
        {
            return Difficulty.Scale(RunRules.BossContactDamage(floor), Difficulty.Profile.bossDamage);
        }

        public static int SkillDamage(bool enraged)
        {
            return SkillDamage(1, enraged);
        }

        public static int SkillDamage(int floor, bool enraged)
        {
            return Difficulty.Scale(RunRules.BossSkillDamageAt(floor, enraged), Difficulty.Profile.bossDamage);
        }

        public static string DisplayName(BossKind kind)
        {
            return kind switch
            {
                BossKind.Rock => "Rock Dumpling",
                BossKind.Flame => "Flame Dumpling",
                BossKind.Frost => "Frost Dumpling",
                BossKind.Volt => "Volt Dumpling",
                BossKind.Toxic => "Toxic Dumpling",
                BossKind.Shadow => "Shadow Dumpling",
                BossKind.Magma => "Magma Dumpling",
                BossKind.Storm => "Storm Dumpling",
                BossKind.Crystal => "Crystal Dumpling",
                BossKind.Royal => "Royal Dumpling",
                _ => "Slime"
            };
        }

        public static string Warning(BossKind kind)
        {
            return "STAGE BOSS\n" + DisplayName(kind);
        }

        public static Color Tint(BossKind kind)
        {
            return kind switch
            {
                BossKind.Rock => new Color(0.55f, 0.52f, 0.48f),
                BossKind.Flame => new Color(1f, 0.42f, 0.16f),
                BossKind.Frost => new Color(0.55f, 0.85f, 1f),
                BossKind.Volt => new Color(1f, 0.92f, 0.35f),
                BossKind.Toxic => new Color(0.45f, 0.9f, 0.28f),
                BossKind.Shadow => new Color(0.28f, 0.18f, 0.42f),
                BossKind.Magma => new Color(0.85f, 0.22f, 0.1f),
                BossKind.Storm => new Color(0.55f, 0.7f, 1f),
                BossKind.Crystal => new Color(0.85f, 0.7f, 1f),
                BossKind.Royal => new Color(0.85f, 0.25f, 0.7f),
                _ => UnityEngine.Color.white
            };
        }

        /// <summary>Cloak / cape stays a distinct red so it does not flatten into the body tint.</summary>
        public static Color CapeColor(BossKind kind)
        {
            return kind switch
            {
                BossKind.Rock => new Color(0.78f, 0.07f, 0.06f),
                BossKind.Flame => new Color(0.9f, 0.1f, 0.05f),
                BossKind.Frost => new Color(0.72f, 0.12f, 0.18f),
                BossKind.Volt => new Color(0.8f, 0.08f, 0.1f),
                BossKind.Toxic => new Color(0.62f, 0.08f, 0.12f),
                BossKind.Shadow => new Color(0.42f, 0.04f, 0.1f),
                BossKind.Magma => new Color(0.88f, 0.08f, 0.04f),
                BossKind.Storm => new Color(0.7f, 0.1f, 0.16f),
                BossKind.Crystal => new Color(0.74f, 0.08f, 0.22f),
                BossKind.Royal => new Color(0.82f, 0.05f, 0.12f),
                _ => new Color(0.78f, 0.08f, 0.08f)
            };
        }

        public static Color CrownColor => new Color(0.98f, 0.78f, 0.22f);

        public static Color GemColor(BossKind kind)
        {
            return kind switch
            {
                BossKind.Rock => new Color(0.85f, 0.55f, 0.22f),
                BossKind.Flame => new Color(1f, 0.55f, 0.12f),
                BossKind.Frost => new Color(0.55f, 0.9f, 1f),
                BossKind.Volt => new Color(1f, 0.95f, 0.4f),
                BossKind.Toxic => new Color(0.45f, 1f, 0.3f),
                BossKind.Shadow => new Color(0.7f, 0.4f, 1f),
                BossKind.Magma => new Color(1f, 0.35f, 0.08f),
                BossKind.Storm => new Color(0.55f, 0.8f, 1f),
                BossKind.Crystal => new Color(0.9f, 0.7f, 1f),
                BossKind.Royal => new Color(0.45f, 0.95f, 0.85f),
                _ => new Color(0.45f, 0.95f, 0.85f)
            };
        }

        public static Color ColorForMaterial(string materialName, BossKind kind, Color body)
        {
            string n = materialName != null ? materialName.ToLowerInvariant() : "";
            if (n.Contains("cape") || n.Contains("cloak") || n.Contains("robe") || n.Contains("jubah"))
                return CapeColor(kind);
            if (n.Contains("gold") || n.Contains("crown") || n.Contains("star"))
                return CrownColor;
            if (n.Contains("gem") || n.Contains("crystal"))
                return GemColor(kind);
            if (IsFaceMaterial(n))
                return Color.clear;
            return body;
        }

        public static bool IsFaceMaterial(string materialName)
        {
            string n = materialName != null ? materialName.ToLowerInvariant() : "";
            return n.Contains("eye") || n.Contains("pupil") || n.Contains("iris")
                || n.Contains("highlight") || n.Contains("mouth") || n.Contains("blush");
        }

        public static bool IsFinal(BossKind kind) => kind == BossKind.Royal;
    }
}
