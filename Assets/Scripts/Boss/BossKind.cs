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
            return tier * RunRules.BossHealthPerTier;
        }

        public static int EnrageSkillCount(int floor)
        {
            return Mathf.Clamp(RunRules.BossTier(floor), 1, 10);
        }

        public static int ContactDamageForFloor(int floor)
        {
            return RunRules.BossBasicDamage;
        }

        public static int SkillDamage(bool enraged)
        {
            return enraged ? RunRules.BossEnrageDamage : RunRules.BossSkillDamage;
        }

        public static string DisplayName(BossKind kind)
        {
            return kind switch
            {
                BossKind.Rock => "Rock Slime",
                BossKind.Flame => "Flame Slime",
                BossKind.Frost => "Frost Slime",
                BossKind.Volt => "Volt Slime",
                BossKind.Toxic => "Toxic Slime",
                BossKind.Shadow => "Shadow Slime",
                BossKind.Magma => "Magma Slime",
                BossKind.Storm => "Storm Slime",
                BossKind.Crystal => "Crystal Slime",
                BossKind.Royal => "Royal Slime",
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

        public static bool IsFinal(BossKind kind) => kind == BossKind.Royal;
    }
}
