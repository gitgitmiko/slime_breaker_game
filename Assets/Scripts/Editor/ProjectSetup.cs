using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlimeBreaker.EditorTools
{
    public static class ProjectSetup
    {
        const string ArtRoot = "Assets/Art";
        const string AudioRoot = "Assets/Audio";
        const string OutputRoot = "asset_work/output";
        const string TextureSource = "asset_work/textures";

        static Sprite panelSprite;
        static Sprite buttonSprite;
        static Sprite dangerSprite;
        static Sprite goldSprite;
        static Sprite slotSprite;
        static Sprite barSprite;
        static Sprite joystickBaseSprite;
        static Sprite joystickKnobSprite;
        static Sprite circleSprite;
        static Sprite whiteSprite;
        static Material additiveParticleMaterial;
        static Material trailMaterial;
        static Material forestGroundMaterial;
        static Material snowGroundMaterial;
        static Material sandGroundMaterial;
        static Material thunderGroundMaterial;
        static Material swampGroundMaterial;
        static Material shadowGroundMaterial;
        static Material volcanoGroundMaterial;
        static Material stormGroundMaterial;
        static Material crystalGroundMaterial;
        static Material royalGroundMaterial;
        static RenderPipelineAsset[] graphicsPresets;

        [MenuItem("Slime Breaker/Build Android APK")]
        public static void BuildAndroidApk()
        {
            try
            {
                ConfigurePlayerSettings();
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;

                var output = AndroidOutputPath();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? "Builds");

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        "Assets/Scenes/Boot.unity",
                        "Assets/Scenes/MainMenu.unity",
                        "Assets/Scenes/GameArena.unity"
                    },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.CompressWithLz4
                });

                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError("SLIMEBREAKER_APK_FAIL " + report.summary.result);
                    if (Application.isBatchMode)
                        EditorApplication.Exit(1);
                    return;
                }

                Debug.Log("SLIMEBREAKER_APK_OK " + Path.GetFullPath(output));
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("SLIMEBREAKER_APK_FAIL\n" + error);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        static string AndroidOutputPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildOutput")
                    return args[i + 1];
            }
            return "Builds/SlimeBreaker.apk";
        }

        [MenuItem("Slime Breaker/Build Project")]
        public static void BuildAll()
        {
            try
            {
                Run();
                Debug.Log("SLIMEBREAKER_SETUP_OK");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("SLIMEBREAKER_SETUP_FAIL\n" + error);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        static void Run()
        {
            foreach (var dir in new[]
            {
                "Assets/Prefabs/Player", "Assets/Prefabs/Enemies", "Assets/Prefabs/Bosses", "Assets/Prefabs/Environment",
                "Assets/Prefabs/Pickups", "Assets/Prefabs/VFX", "Assets/Prefabs/Skills", "Assets/Prefabs/UI", "Assets/Animations",
                "Assets/ScriptableObjects/Enemies", "Assets/ScriptableObjects/Upgrades", "Assets/ScriptableObjects/Skills",
                "Assets/ScriptableObjects/Balance", "Assets/Scenes", "Assets/Art/Textures", "Assets/Art/VFX"
            })
                Directory.CreateDirectory(dir);

            EnsureObstacleLayer();
            SyncArt();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureTextures();
            LoadUiArt();
            SharedMaterials();
            var models = AssetDatabase.FindAssets("t:Model", new[] { ArtRoot });
            foreach (var guid in models)
                ConfigureModel(AssetDatabase.GUIDToAssetPath(guid));
            ConvertMaterials();
            ConfigureRenderPipeline();

            var heroClips = LoadClips("Assets/Art/Characters/Hero/CHR_Hero_Main.fbx");
            var normalClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx");
            var fastClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx");
            var tankClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx");
            var bossClips = LoadClips("Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx");

            var heroController = BuildController("Assets/Animations/CHR_Hero_Main.controller", heroClips, "Run", true, false);
            var normalController = BuildController("Assets/Animations/ENM_Slime_Normal.controller", normalClips, "Move", false, false);
            var fastController = BuildController("Assets/Animations/ENM_Slime_Fast.controller", fastClips, "Move", false, false);
            var tankController = BuildController("Assets/Animations/ENM_Slime_Tank.controller", tankClips, "Move", false, false);
            var bossController = BuildController("Assets/Animations/BOSS_SlimeKing.controller", bossClips, "Move", false, true);

            var vfx = new List<VfxPlayer.Entry>
            {
                Vfx("hit", Burst("VFX_Hit_Small", new Color(1f, 0.86f, 0.35f), 14, 3.2f, 0.16f)),
                Vfx("dash", Burst("VFX_Dash_Trail", new Color(0.3f, 0.9f, 1f), 18, 2f, 0.14f)),
                Vfx("levelup", Burst("VFX_LevelUp", new Color(0.98f, 0.84f, 0.3f), 32, 4f, 0.22f, 0.7f)),
                Vfx("slam", Burst("VFX_Boss_Impact", new Color(0.95f, 0.4f, 0.18f), 36, 5f, 0.28f)),
                Vfx("ice", Burst("VFX_Ice_Hit", new Color(0.62f, 0.9f, 1f), 12, 2.2f, 0.14f)),
                Vfx("zap", Burst("VFX_Zap", new Color(1f, 1f, 0.6f), 10, 3.5f, 0.12f)),
                Vfx("fire", Burst("VFX_Fire_Nova", new Color(1f, 0.5f, 0.15f), 30, 3.6f, 0.26f, 0.55f)),
                Vfx("burn", Burst("VFX_Burn_Tick", new Color(1f, 0.5f, 0.2f), 5, 1.2f, 0.12f)),
                Vfx("arcane", Burst("VFX_Arcane_Hit", new Color(0.75f, 0.4f, 1f), 14, 3f, 0.16f)),
                Vfx("powerup", Burst("VFX_PowerUp", Color.white, 40, 3.5f, 0.2f, 0.8f, 0.3f)),
                Vfx("cast", Burst("VFX_Cast", new Color(0.8f, 0.55f, 1f), 8, 1.6f, 0.1f)),
                Vfx("death", Burst("VFX_Enemy_Death", new Color(0.4f, 0.9f, 0.5f), 22, 3f, 0.2f, 0.5f, 0.2f, 0.8f)),
                Vfx("pickup", Burst("VFX_Pickup", new Color(0.5f, 1f, 0.6f), 8, 2f, 0.1f)),
                Vfx("playerhit", Burst("VFX_Player_Hit", new Color(1f, 0.3f, 0.3f), 16, 3f, 0.16f))
            };

            var normalDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Normal.asset", "Slime", 24, 2.35f, 8, 1.15f, 0.75f, 3, false);
            var fastDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Fast.asset", "Slime Cepat", 14, 3.9f, 6, 1.0f, 0.6f, 4, false);
            var tankDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Tank.asset", "Slime Tank", 64, 1.35f, 14, 1.4f, 0.9f, 8, false);
            var rangedDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Ranged.asset", "Slime Jarak", 18, 2.15f, 7, 6.4f, 1.35f, 4, false, ranged: true, tint: new Color(0.95f, 0.48f, 0.78f));
            int bossHit = RunRules.BossBasicDamage;
            var bossDef = Enemy("Assets/ScriptableObjects/Enemies/BOSS_SlimeKing.asset", "Royal Slime", 100000, 2.1f, bossHit, 1.7f, 0.8f, 80, true, kind: BossKind.Royal);
            var bossDefs = new[]
            {
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_RockSlime.asset", "Rock Slime", 10000, 1.8f, bossHit, 1.8f, 0.85f, 40, true, incoming: 0.65f, kind: BossKind.Rock, tint: BossCatalog.Tint(BossKind.Rock)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_FlameSlime.asset", "Flame Slime", 20000, 2.15f, bossHit, 1.7f, 0.8f, 45, true, kind: BossKind.Flame, tint: BossCatalog.Tint(BossKind.Flame)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_FrostSlime.asset", "Frost Slime", 30000, 1.9f, bossHit, 1.7f, 0.8f, 50, true, kind: BossKind.Frost, tint: BossCatalog.Tint(BossKind.Frost)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_VoltSlime.asset", "Volt Slime", 40000, 2.55f, bossHit, 1.6f, 0.7f, 55, true, kind: BossKind.Volt, tint: BossCatalog.Tint(BossKind.Volt)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_ToxicSlime.asset", "Toxic Slime", 50000, 2.0f, bossHit, 1.7f, 0.8f, 60, true, kind: BossKind.Toxic, tint: BossCatalog.Tint(BossKind.Toxic)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_ShadowSlime.asset", "Shadow Slime", 60000, 2.4f, bossHit, 1.6f, 0.7f, 65, true, kind: BossKind.Shadow, tint: BossCatalog.Tint(BossKind.Shadow)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_MagmaSlime.asset", "Magma Slime", 70000, 1.85f, bossHit, 1.8f, 0.85f, 70, true, kind: BossKind.Magma, tint: BossCatalog.Tint(BossKind.Magma)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_StormSlime.asset", "Storm Slime", 80000, 2.2f, bossHit, 1.7f, 0.75f, 75, true, kind: BossKind.Storm, tint: BossCatalog.Tint(BossKind.Storm)),
                Enemy("Assets/ScriptableObjects/Enemies/BOSS_CrystalSlime.asset", "Crystal Slime", 90000, 1.7f, bossHit, 1.7f, 0.8f, 80, true, incoming: 0.8f, kind: BossKind.Crystal, tint: BossCatalog.Tint(BossKind.Crystal)),
                bossDef
            };

            var upgrades = new[]
            {
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Damage.asset", UpgradeKind.Damage, "Serangan", "+20% damage sihir", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Damage.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Speed.asset", UpgradeKind.Speed, "Laju", "+12% kecepatan gerak", 0.12f, "Assets/Art/UI/Icons/ICON_Upgrade_Speed.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Health.asset", UpgradeKind.Health, "Nyawa", "+25 nyawa maksimum", 25f, "Assets/Art/UI/Icons/ICON_Upgrade_Health.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_AttackRate.asset", UpgradeKind.AttackRate, "Attack Speed", "Cooldown serangan lebih pendek", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_AttackRate.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Dash.asset", UpgradeKind.Dash, "Dash", "Dash lebih sering", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_Dash.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Range.asset", UpgradeKind.Range, "Jangkauan", "+20% jangkauan mantra", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Range.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Xp.asset", UpgradeKind.Xp, "EXP", "+25% EXP", 0.25f, "Assets/Art/UI/Icons/ICON_Upgrade_Xp.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Armor.asset", UpgradeKind.Armor, "Perisai", "Damage masuk -8%", 0.08f, "Assets/Art/UI/Icons/ICON_Upgrade_Armor.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Lifesteal.asset", UpgradeKind.Lifesteal, "Lifesteal", "8% nyawa dari damage", 0.08f, "Assets/Art/UI/Icons/ICON_Upgrade_Lifesteal.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_CritRate.asset", UpgradeKind.CritChance, "Crit Rate", "+8% peluang critical", 0.08f, "Assets/Art/UI/Icons/ICON_Upgrade_AttackRate.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_CritDamage.asset", UpgradeKind.CritDamage, "Critical", "+30% damage critical", 0.3f, "Assets/Art/UI/Icons/ICON_Upgrade_Damage.png")
            };

            var skills = new[]
            {
                Skill("Assets/ScriptableObjects/Skills/SKILL_IceOrbit.asset", SkillKind.IceOrbit, "Cincin Es",
                    "Pecahan es mengitari penyihir. Musuh yang tersentuh terluka dan melambat.",
                    "Assets/Art/UI/Icons/ICON_Skill_Ice.png", new Color(0.55f, 0.85f, 1f), 7, 0f, 1.5f, 3, 0.45f, 1.6f),
                Skill("Assets/ScriptableObjects/Skills/SKILL_LightningChain.asset", SkillKind.LightningChain, "Petir Berantai",
                    "Petir menyambar musuh terdekat lalu melompat ke musuh lain.",
                    "Assets/Art/UI/Icons/ICON_Skill_Lightning.png", new Color(1f, 0.95f, 0.45f), 16, 2.6f, 0f, 3, 1f, 0f),
                Skill("Assets/ScriptableObjects/Skills/SKILL_FireNova.asset", SkillKind.FireNova, "Nova Api",
                    "Gelombang api meledak di sekitar penyihir dan membakar musuh.",
                    "Assets/Art/UI/Icons/ICON_Skill_Fire.png", new Color(1f, 0.5f, 0.2f), 14, 4.2f, 2.6f, 0, 1f, 1.5f),
                Skill("Assets/ScriptableObjects/Skills/SKILL_ArcaneMissile.asset", SkillKind.ArcaneMissile, "Misil Arkana",
                    "Menembakkan misil pelacak ke musuh acak di sekitar arena.",
                    "Assets/Art/UI/Icons/ICON_Skill_Arcane.png", new Color(0.75f, 0.45f, 1f), 11, 1.9f, 0f, 2, 1f, 0f)
            };

            var balance = Scriptable("Assets/ScriptableObjects/Balance/Balance.asset", ScriptableObject.CreateInstance<BalanceConfig>());
            balance.baseAttackRange = 4.4f;
            balance.baseAttackCooldown = 0.55f;
            balance.maxAliveEnemies = 28;
            balance.phases = new[]
            {
                Phase(45f, 2.3f, 1, 0, 0, 0),
                Phase(120f, 1.7f, 3, 2, 0, 1),
                Phase(210f, 1.35f, 3, 2, 1, 2),
                Phase(300f, 1.0f, 2, 3, 2, 2)
            };
            EditorUtility.SetDirty(balance);

            var xpPrefab = PickupPrefab<XPOrb>("Assets/Prefabs/Pickups/VFX_XP_Orb.prefab", "Assets/Art/VFX/VFX_XP_Orb.fbx");
            var bolt = ProjectilePrefab("Assets/Prefabs/Skills/PRJ_Magic_Bolt.prefab", "Assets/Art/VFX/VFX_Magic_Bolt.fbx", new Color(0.85f, 0.6f, 1f), 0.16f, 0.22f);
            var missile = ProjectilePrefab("Assets/Prefabs/Skills/PRJ_Arcane_Missile.prefab", "Assets/Art/VFX/VFX_Arcane_Missile.fbx", new Color(0.7f, 0.35f, 1f), 0.14f, 0.3f);
            var hostileBolt = HostileBoltPrefab("Assets/Prefabs/Skills/PRJ_Hostile_Bolt.prefab", "Assets/Art/VFX/VFX_Magic_Bolt.fbx", new Color(1f, 0.45f, 0.2f), 0.16f, 0.2f);
            var shard = ModelOnlyPrefab("Assets/Prefabs/Skills/SKL_Ice_Shard.prefab", "Assets/Art/VFX/VFX_Ice_Shard.fbx");
            var fireRing = FireNovaPrefab("Assets/Prefabs/Skills/SKL_Fire_Nova.prefab");
            var lightning = LightningPrefab("Assets/Prefabs/Skills/SKL_Lightning.prefab");
            var powerUp = PowerUpPrefab("Assets/Prefabs/Pickups/PWR_Crystal.prefab");

            var hero = CharacterPrefab("Assets/Prefabs/Player/CHR_Hero_Main.prefab", "Assets/Art/Characters/Hero/CHR_Hero_Main.fbx", heroController, 1.35f, true, null, null);
            ConfigureHero(hero, bolt, shard, missile, fireRing, lightning);
            var normal = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Normal.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx", normalController, 0.9f, false, normalDef, xpPrefab);
            var fast = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Fast.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx", fastController, 0.7f, false, fastDef, xpPrefab);
            var tank = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Tank.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx", tankController, 1.15f, false, tankDef, xpPrefab);
            var ranged = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Ranged.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx", fastController, 0.85f, false, rangedDef, xpPrefab);
            WireRangedBolt(ranged, hostileBolt);
            var boss = BossPrefab("Assets/Prefabs/Bosses/BOSS_SlimeKing.prefab", bossController, bossDef);

            var env = new Dictionary<string, GameObject>();
            env["ground"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Ground.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Ground.fbx", 3f, true);
            var propHeights = new Dictionary<string, float>
            {
                { "Tree_A", 2.2f }, { "Tree_B", 2.4f }, { "Rock_A", 0.7f }, { "Rock_B", 0.6f }, { "Mushroom", 0.5f },
                { "Bush", 0.5f }, { "RuinPillar", 1.6f }, { "Grass", 0.3f }, { "Flower", 0.35f }, { "BoundaryStone", 0.9f }
            };
            foreach (var pair in propHeights)
                env[pair.Key] = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_" + pair.Key + ".prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_" + pair.Key + ".fbx", pair.Value, false);
            env["RuneRing"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Arena_RuneRing.prefab", "Assets/Art/Environment/ForestArena/ENV_Arena_RuneRing.fbx", 0.05f, false);
            env["Cactus"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Desert_Cactus.prefab", "Assets/Art/Environment/Biomes/ENV_Desert_Cactus.fbx", 1.7f, false);
            env["IceSpike"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Snow_IceSpike.prefab", "Assets/Art/Environment/Biomes/ENV_Snow_IceSpike.fbx", 1.4f, false);
            env["ThunderSpire"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Thunder_Spire.prefab", "Assets/Art/Environment/Biomes/ENV_Thunder_Spire.fbx", 2.2f, false);
            env["SwampRoot"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Swamp_Root.prefab", "Assets/Art/Environment/Biomes/ENV_Swamp_Root.fbx", 1.4f, false);
            env["SwampReed"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Swamp_Reed.prefab", "Assets/Art/Environment/Biomes/ENV_Swamp_Reed.fbx", 0.7f, false);
            env["ShadowObelisk"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Shadow_Obelisk.prefab", "Assets/Art/Environment/Biomes/ENV_Shadow_Obelisk.fbx", 2.1f, false);
            env["VolcanoRock"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Volcano_Rock.prefab", "Assets/Art/Environment/Biomes/ENV_Volcano_Rock.fbx", 0.8f, false);
            env["VolcanoPillar"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Volcano_Pillar.prefab", "Assets/Art/Environment/Biomes/ENV_Volcano_Pillar.fbx", 1.8f, false);
            env["StormArch"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Storm_Arch.prefab", "Assets/Art/Environment/Biomes/ENV_Storm_Arch.fbx", 1.7f, false);
            env["CrystalSpire"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Crystal_Spire.prefab", "Assets/Art/Environment/Biomes/ENV_Crystal_Spire.fbx", 1.8f, false);
            env["RoyalColumn"] = StaticPrefab("Assets/Prefabs/Environment/ENV_Royal_Column.prefab", "Assets/Art/Environment/Biomes/ENV_Royal_Column.fbx", 2.1f, false);

            var obstacles = new[]
            {
                ObstaclePrefab("Assets/Prefabs/Environment/OBS_RockCluster.prefab", Obstacle.Kind.Rock, 1.15f, env["Rock_A"], env["Rock_B"]),
                ObstaclePrefab("Assets/Prefabs/Environment/OBS_Tree.prefab", Obstacle.Kind.Tree, 0.85f, env["Tree_A"]),
                ObstaclePrefab("Assets/Prefabs/Environment/OBS_Pillar.prefab", Obstacle.Kind.Ruin, 0.7f, env["RuinPillar"]),
                ObstaclePrefab("Assets/Prefabs/Environment/OBS_Boulder.prefab", Obstacle.Kind.Rock, 1.25f, env["Rock_A"]),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_Cactus.prefab", Obstacle.Kind.Tree, 0.7f, env["Cactus"], Biome.Desert),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_IceSpike.prefab", Obstacle.Kind.Rock, 0.7f, env["IceSpike"], Biome.Snow, Biome.Crystal),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_ThunderSpire.prefab", Obstacle.Kind.Ruin, 0.65f, env["ThunderSpire"], Biome.Thunder),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_SwampRoot.prefab", Obstacle.Kind.Tree, 0.8f, env["SwampRoot"], Biome.Swamp),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_Obelisk.prefab", Obstacle.Kind.Ruin, 0.7f, env["ShadowObelisk"], Biome.Shadow),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_VolcanoRock.prefab", Obstacle.Kind.Rock, 1.05f, env["VolcanoRock"], Biome.Volcano),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_StormArch.prefab", Obstacle.Kind.Ruin, 1.1f, env["StormArch"], Biome.Storm),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_CrystalSpire.prefab", Obstacle.Kind.Ruin, 0.75f, env["CrystalSpire"], Biome.Crystal),
                BiomeObstacle("Assets/Prefabs/Environment/OBS_RoyalColumn.prefab", Obstacle.Kind.Ruin, 0.75f, env["RoyalColumn"], Biome.Royal)
            };
            var rain = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Rain.prefab", new Color(0.65f, 0.75f, 0.9f), 280, 14f, 0.08f, 2.4f, ParticleSystemShapeType.Box, 20f, "rain", true);
            var snowWx = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Snow.prefab", new Color(0.92f, 0.96f, 1f), 120, 1.4f, 0.12f, 0.25f, ParticleSystemShapeType.Box, 20f, "wind", false);
            var fireflies = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Fireflies.prefab", new Color(1f, 0.92f, 0.45f), 36, 0.35f, 0.08f, -0.05f, ParticleSystemShapeType.Sphere, 10f, null, false);
            var dust = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Dust.prefab", new Color(1f, 0.82f, 0.5f, 0.5f), 56, 0.4f, 0.16f, -0.08f, ParticleSystemShapeType.Box, 18f, "wind", false);
            var ash = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Ash.prefab", new Color(0.55f, 0.32f, 0.18f, 0.7f), 70, 0.55f, 0.14f, 0.12f, ParticleSystemShapeType.Box, 18f, "wind", false);
            var spores = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Spores.prefab", new Color(0.55f, 0.85f, 0.3f, 0.55f), 48, 0.28f, 0.12f, -0.04f, ParticleSystemShapeType.Box, 16f, null, false);
            var sparks = WeatherPrefab("Assets/Prefabs/VFX/VFX_Weather_Sparks.prefab", new Color(1f, 0.92f, 0.45f), 40, 0.9f, 0.07f, 0.05f, ParticleSystemShapeType.Box, 16f, null, false);
            var portal = PortalPrefab("Assets/Prefabs/Environment/ENV_Exit_Portal.prefab", env["RuneRing"]);

            BuildBoot();
            BuildMenu(hero, heroController, env);
            BuildArena(balance, upgrades, skills, hero, normal, fast, tank, ranged, boss, bossDefs, hostileBolt, lightning, env, vfx, powerUp, portal, obstacles, rain, snowWx, fireflies, dust, ash, spores, sparks);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ asset sync

        /// <summary>Mirrors asset_work/output into Assets so a Blender/audio rebuild is picked up by the next setup run.</summary>
        static void SyncArt()
        {
            if (!Directory.Exists(OutputRoot))
            {
                Debug.LogWarning("asset_work/output tidak ada; memakai aset yang sudah ada di Assets/Art.");
                return;
            }
            // Generated FBX materials are rebuilt from scratch so renamed Blender materials do not leave orphans.
            foreach (var dir in Directory.GetDirectories(ArtRoot, "Materials", SearchOption.AllDirectories))
                AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));

            foreach (var folder in new[] { "Characters", "Enemies", "Environment", "VFX", "UI" })
                Mirror(Path.Combine(OutputRoot, folder), Path.Combine(ArtRoot, folder), new[] { ".fbx", ".png" });
            Mirror(Path.Combine(OutputRoot, "Audio"), AudioRoot, new[] { ".ogg", ".wav" });
            if (Directory.Exists(TextureSource))
                Mirror(TextureSource, Path.Combine(ArtRoot, "Textures"), new[] { ".png" });
        }

        static void Mirror(string source, string destination, string[] extensions)
        {
            if (!Directory.Exists(source))
                return;
            Directory.CreateDirectory(destination);
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant()) < 0)
                    continue;
                var relative = Path.GetRelativePath(source, file);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
                wanted.Add(Path.GetFullPath(target));
                if (!File.Exists(target) || File.GetLastWriteTimeUtc(target) != File.GetLastWriteTimeUtc(file) || new FileInfo(target).Length != new FileInfo(file).Length)
                {
                    File.Copy(file, target, true);
                    File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
                }
            }
            foreach (var file in Directory.GetFiles(destination, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(extensions, ext) < 0 || wanted.Contains(Path.GetFullPath(file)))
                    continue;
                if (Path.GetFileName(file) == "UI_White.png")
                    continue;
                if (file.Replace('\\', '/').Contains(".fbm/"))
                    continue;
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
            }
        }

        static void ConfigureTextures()
        {
            if (AssetDatabase.IsValidFolder("Assets/Art/UI"))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        continue;
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 1024;
                    if (path.Contains("UI_Panel"))
                        importer.spriteBorder = new Vector4(48f, 48f, 48f, 48f);
                    else if (path.Contains("UI_Button"))
                        importer.spriteBorder = new Vector4(60f, 58f, 60f, 58f);
                    else if (path.Contains("UI_Slot"))
                        importer.spriteBorder = new Vector4(28f, 28f, 28f, 28f);
                    else if (path.Contains("UI_Bar_Back"))
                        importer.spriteBorder = new Vector4(20f, 14f, 20f, 14f);
                    importer.SaveAndReimport();
                }
            }
            if (AssetDatabase.IsValidFolder("Assets/Art/Textures"))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Textures" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        continue;
                    bool normal = path.EndsWith("_Normal.png", StringComparison.OrdinalIgnoreCase);
                    importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = !normal;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 4;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;
            importer.globalScale = 1f;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                foreach (var clip in clips)
                {
                    int bar = clip.name.LastIndexOf('|');
                    if (bar >= 0)
                        clip.name = clip.name.Substring(bar + 1);
                    clip.loopTime = clip.name == "Idle" || clip.name == "Run" || clip.name == "Move";
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static Texture2D TextureNamed(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/" + name + ".png");
        }

        /// <summary>Converts the Blender-exported materials to URP Lit and applies a per-material look table.</summary>
        static void ConvertMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Shader URP Lit tidak ditemukan.");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                    continue;
                // Hand-made particle/trail/beam materials keep their unlit shaders.
                if (mat.shader != null && (mat.shader.name.Contains("Particles") || mat.shader.name.Contains("Unlit")))
                    continue;
                Color color = Color.white;
                if (mat.HasProperty("_BaseColor"))
                    color = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color"))
                    color = mat.GetColor("_Color");
                Texture texture = null;
                if (mat.HasProperty("_BaseMap"))
                    texture = mat.GetTexture("_BaseMap");
                if (texture == null && mat.HasProperty("_MainTex"))
                    texture = mat.GetTexture("_MainTex");
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                if (texture != null)
                    mat.SetTexture("_BaseMap", texture);
                mat.SetFloat("_Smoothness", 0.3f);
                mat.SetFloat("_Metallic", 0f);
                mat.DisableKeyword("_EMISSION");
                mat.DisableKeyword("_NORMALMAP");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;

                string name = mat.name.ToLowerInvariant();
                void Emissive(float strength)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * strength);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                void Normal(string textureName, float scale)
                {
                    var normal = TextureNamed(textureName);
                    if (normal == null)
                        return;
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", scale);
                    if (mat.GetTexture("_BaseMap") == null)
                    {
                        var baseMap = TextureNamed(textureName.Replace("_Normal", ""));
                        if (baseMap != null)
                            mat.SetTexture("_BaseMap", baseMap);
                    }
                }

                if (name.Contains("ground"))
                {
                    mat.SetFloat("_Smoothness", 0.12f);
                    Normal("TEX_Forest_Ground_Normal", 0.9f);
                }
                else if (name.Contains("bark"))
                {
                    mat.SetFloat("_Smoothness", 0.18f);
                    Normal("TEX_Bark_Normal", 1f);
                }
                else if (name.Contains("rock"))
                {
                    mat.SetFloat("_Smoothness", 0.2f);
                    Normal("TEX_Rock_Normal", 0.8f);
                }
                else if (name.Contains("gold"))
                {
                    mat.SetFloat("_Metallic", 0.75f);
                    mat.SetFloat("_Smoothness", 0.78f);
                }
                else if (name.Contains("skin"))
                    mat.SetFloat("_Smoothness", 0.38f);
                else if (name.Contains("hair"))
                    mat.SetFloat("_Smoothness", 0.58f);
                else if (name.Contains("highlight"))
                {
                    mat.SetFloat("_Smoothness", 0.95f);
                    Emissive(1.4f);
                }
                else if (name.Contains("iris") || name.Contains("crystal") || name.Contains("gem"))
                {
                    mat.SetFloat("_Smoothness", 0.9f);
                    Emissive(0.8f);
                }
                else if (name.Contains("lava") || name.Contains("thunder"))
                {
                    mat.SetFloat("_Smoothness", 0.62f);
                    Emissive(1.3f);
                }
                else if (name.Contains("ice") || name.Contains("rune"))
                {
                    mat.SetFloat("_Smoothness", 0.85f);
                    Emissive(0.55f);
                }
                else if (name.Contains("arcane") || name.Contains("fire") || name.Contains("powerup") || name.Contains("xp") || name.Contains("ring"))
                {
                    mat.SetFloat("_Smoothness", 0.85f);
                    Emissive(1.1f);
                }
                else if (name.Contains("marble"))
                    mat.SetFloat("_Smoothness", 0.62f);
                else if (name.Contains("cactus") || name.Contains("reed"))
                    mat.SetFloat("_Smoothness", 0.22f);
                else if (name.Contains("slime") || name.Contains("king"))
                {
                    mat.SetFloat("_Smoothness", 0.78f);
                    Emissive(0.35f);
                }
                else if (name.Contains("leaf") || name.Contains("grass") || name.Contains("bush") || name.Contains("petal") || name.Contains("flower"))
                    mat.SetFloat("_Smoothness", 0.22f);
                else if (name.Contains("robe") || name.Contains("hat") || name.Contains("cape"))
                    mat.SetFloat("_Smoothness", 0.32f);
                EditorUtility.SetDirty(mat);
            }
        }

        static void SharedMaterials()
        {
            Directory.CreateDirectory("Assets/Art/VFX/Materials");
            Directory.CreateDirectory("Assets/Art/Environment/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            additiveParticleMaterial = AdditiveMaterial("Assets/Art/VFX/Materials/MAT_Particle_Additive.mat", shader, true);
            trailMaterial = AdditiveMaterial("Assets/Art/VFX/Materials/MAT_Trail_Additive.mat", shader, false);
            forestGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Forest.mat", "TEX_Forest_Ground", "TEX_Forest_Ground_Normal");
            snowGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Snow.mat", "TEX_Snow_Ground", "TEX_Snow_Ground_Normal");
            sandGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Sand.mat", "TEX_Sand_Ground", "TEX_Sand_Ground_Normal");
            thunderGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Thunder.mat", "TEX_Thunder_Ground", "TEX_Thunder_Ground_Normal");
            swampGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Swamp.mat", "TEX_Swamp_Ground", "TEX_Swamp_Ground_Normal");
            shadowGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Shadow.mat", "TEX_Shadow_Ground", "TEX_Shadow_Ground_Normal");
            volcanoGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Volcano.mat", "TEX_Volcano_Ground", "TEX_Volcano_Ground_Normal");
            stormGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Storm.mat", "TEX_Storm_Ground", "TEX_Storm_Ground_Normal");
            crystalGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Crystal.mat", "TEX_Crystal_Ground", "TEX_Crystal_Ground_Normal");
            royalGroundMaterial = GroundMaterial("Assets/Art/Environment/Materials/MAT_Ground_Royal.mat", "TEX_Royal_Ground", "TEX_Royal_Ground_Normal");
        }

        static Material GroundMaterial(string path, string albedoName, string normalName)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            var albedo = TextureNamed(albedoName);
            if (albedo != null)
                material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            var normal = TextureNamed(normalName);
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 0.9f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material AdditiveMaterial(string path, Shader shader, bool softParticle)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 2f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.One);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.One);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)CullMode.Off);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (softParticle && material.HasProperty("_BaseMap"))
            {
                var soft = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
                if (soft != null)
                    material.SetTexture("_BaseMap", soft);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureRenderPipeline()
        {
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            // Default project quality stays High so a missing GraphicsService still looks sharp.
            WriteUrp(mobile, 1f, 4, false, 40f, 4096, 2, true, 3, 1);
            WriteUrp(pc, 1f, 4, true, 48f, 4096, 2, true, 3, 1);

            graphicsPresets = new RenderPipelineAsset[3];
            if (mobile == null)
                return;
            graphicsPresets[0] = MakeUrpPreset("Assets/Settings/URP_Low.asset", mobile, 0.8f, 1, false, 16f, 1024, 1, false, 1, 0);
            graphicsPresets[1] = MakeUrpPreset("Assets/Settings/URP_Medium.asset", mobile, 1f, 2, false, 28f, 2048, 1, true, 2, 1);
            graphicsPresets[2] = MakeUrpPreset("Assets/Settings/URP_High.asset", mobile, 1f, 4, false, 40f, 4096, 2, true, 3, 1);
        }

        static UniversalRenderPipelineAsset MakeUrpPreset(string path, UniversalRenderPipelineAsset source, float scale, int msaa, bool hdr, float shadowDistance, int shadowMap, int cascades, bool soft, int softQuality, int additionalLights)
        {
            if (File.Exists(path))
                AssetDatabase.DeleteAsset(path);
            var copy = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(copy, path);
            WriteUrp(copy, scale, msaa, hdr, shadowDistance, shadowMap, cascades, soft, softQuality, additionalLights);
            return AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        }

        static void WriteUrp(UniversalRenderPipelineAsset asset, float scale, int msaa, bool hdr, float shadowDistance, int shadowMap, int cascades, bool soft, int softQuality, int additionalLights)
        {
            if (asset == null)
                return;
            var so = new SerializedObject(asset);
            Set(so, "m_RenderScale", scale);
            Set(so, "m_MSAA", msaa);
            Set(so, "m_SupportsHDR", hdr);
            Set(so, "m_ShadowDistance", shadowDistance);
            Set(so, "m_MainLightShadowmapResolution", shadowMap);
            Set(so, "m_ShadowCascadeCount", cascades);
            Set(so, "m_Cascade2Split", 0.22f);
            Set(so, "m_SoftShadowsSupported", soft);
            Set(so, "m_SoftShadowQuality", softQuality);
            Set(so, "m_AdditionalLightsRenderingMode", additionalLights);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_AnyShadowsSupported", true);
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void Set(SerializedObject so, string name, float value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.floatValue = value;
        }

        static void Set(SerializedObject so, string name, int value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.intValue = value;
        }

        static void Set(SerializedObject so, string name, bool value)
        {
            var prop = so.FindProperty(name);
            if (prop != null)
                prop.boolValue = value;
        }

        static void EnsureObstacleLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null || layers.arraySize <= 8)
                return;
            var slot = layers.GetArrayElementAtIndex(8);
            if (slot != null && slot.stringValue != Obstacles.LayerName)
            {
                slot.stringValue = Obstacles.LayerName;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ animation

        static Dictionary<string, AnimationClip> LoadClips(string path)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip || clip.name.StartsWith("__"))
                    continue;
                var name = clip.name;
                int bar = name.LastIndexOf('|');
                if (bar >= 0)
                    name = name.Substring(bar + 1);
                result[name] = clip;
            }
            Debug.Log(path + " clips: " + string.Join(",", result.Keys));
            return result;
        }

        static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips, string moveClip, bool combat, bool boss)
        {
            if (File.Exists(path))
                AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            if (combat)
            {
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            }
            if (boss)
            {
                controller.AddParameter("Telegraph", AnimatorControllerParameterType.Trigger);
                controller.AddParameter("Slam", AnimatorControllerParameterType.Trigger);
            }
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string stateName, string clipName)
            {
                var state = machine.AddState(stateName);
                if (clips != null && clips.TryGetValue(clipName, out var clip))
                    state.motion = clip;
                else
                    Debug.LogWarning(path + " tidak punya clip " + clipName);
                return state;
            }
            var idle = State("Idle", "Idle");
            machine.defaultState = idle;
            var move = State("Move", moveClip);
            var toMove = idle.AddTransition(move);
            toMove.hasExitTime = false;
            toMove.duration = 0.1f;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var toIdle = move.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.12f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // One-shot states return to Idle when they finish; Idle hands off to Move via Speed.
            void OneShot(string stateName, string clipName, float exitTime)
            {
                var state = State(stateName, clipName);
                Any(machine, state, stateName);
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = exitTime;
                back.duration = 0.08f;
            }
            OneShot("Hit", "Hit", 0.9f);
            var death = State("Death", "Death");
            Any(machine, death, "Death");
            if (combat)
            {
                OneShot("Attack", "Attack", 0.82f);
                OneShot("Dash", "Dash", 0.9f);
            }
            if (boss)
            {
                OneShot("Telegraph", "Telegraph", 0.95f);
                OneShot("Slam", "Slam", 0.92f);
            }
            return controller;
        }

        static void Any(AnimatorStateMachine machine, AnimatorState destination, string trigger)
        {
            var transition = machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0.05f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        // ------------------------------------------------------------------ VFX

        static VfxPlayer.Entry Vfx(string id, GameObject prefab, float lifetime = 1.6f)
        {
            return new VfxPlayer.Entry { id = id, prefab = prefab, lifetime = lifetime };
        }

        static GameObject Burst(string name, Color color, int count, float speed, float size, float lifetime = 0.42f, float radius = 0.25f, float gravity = 0f)
        {
            string path = "Assets/Prefabs/VFX/" + name + ".prefab";
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = Mathf.Max(0.3f, lifetime);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = color;
            main.maxParticles = count + 4;
            main.playOnAwake = true;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var colorOverLife = particles.colorOverLifetime;
            colorOverLife.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = gradient;
            var sizeOverLife = particles.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = additiveParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var prefab = SavePrefab(go, path);
            return prefab;
        }

        // ------------------------------------------------------------------ data

        static T Scriptable<T>(string path, T asset) where T : ScriptableObject
        {
            if (File.Exists(path))
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static EnemyDefinition Enemy(string path, string displayName, int health, float speed, int damage, float range, float cooldown, int xp, bool boss,
            bool ranged = false, float incoming = 1f, BossKind kind = BossKind.None, Color tint = default)
        {
            var asset = ScriptableObject.CreateInstance<EnemyDefinition>();
            asset.displayName = displayName;
            asset.maxHealth = health;
            asset.moveSpeed = speed;
            asset.contactDamage = damage;
            asset.contactRange = range;
            asset.contactCooldown = cooldown;
            asset.xp = xp;
            asset.isBoss = boss;
            asset.isRanged = ranged;
            asset.incomingDamageScale = incoming;
            asset.bossKind = kind;
            asset.tint = tint == default ? Color.white : tint;
            return Scriptable(path, asset);
        }

        static UpgradeDefinition Upgrade(string path, UpgradeKind kind, string title, string description, float amount, string iconPath)
        {
            var asset = ScriptableObject.CreateInstance<UpgradeDefinition>();
            asset.kind = kind;
            asset.title = title;
            asset.description = description;
            asset.amount = amount;
            asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (asset.icon == null)
                Debug.LogWarning("Ikon hilang: " + iconPath);
            return Scriptable(path, asset);
        }

        static SkillDefinition Skill(string path, SkillKind kind, string title, string description, string iconPath, Color color,
            int damage, float cooldown, float radius, int count, float slow, float duration)
        {
            var asset = ScriptableObject.CreateInstance<SkillDefinition>();
            asset.kind = kind;
            asset.title = title;
            asset.description = description;
            asset.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (asset.icon == null)
                Debug.LogWarning("Ikon skill hilang: " + iconPath);
            asset.color = color;
            asset.baseDamage = damage;
            asset.cooldown = cooldown;
            asset.radius = radius;
            asset.count = count;
            asset.slowFactor = slow;
            asset.effectDuration = duration;
            return Scriptable(path, asset);
        }

        static SpawnPhase Phase(float until, float interval, int normal, int fast, int tank, int ranged = 0)
        {
            return new SpawnPhase { untilSeconds = until, interval = interval, normal = normal, fast = fast, tank = tank, ranged = ranged };
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject LoadModel(string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new FileNotFoundException(modelPath);
            return model;
        }

        static GameObject PickupPrefab<T>(string path, string modelPath) where T : Component
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(modelPath));
            visual.transform.SetParent(root.transform, false);
            visual.name = "Visual";
            root.AddComponent<T>();
            return SavePrefab(root, path);
        }

        static GameObject ModelOnlyPrefab(string path, string modelPath)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(modelPath));
            instance.name = Path.GetFileNameWithoutExtension(path);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            return SavePrefab(instance, path);
        }

        static GameObject ProjectilePrefab(string path, string modelPath, Color trailColor, float width, float time)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(modelPath));
            visual.transform.SetParent(root.transform, false);
            visual.name = "Spinner";
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 4;
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(trailColor, Color.white, 0.4f), 0f), new GradientColorKey(trailColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            var projectile = root.AddComponent<HomingProjectile>();
            projectile.speed = 17f;
            return SavePrefab(root, path);
        }

        static GameObject HostileBoltPrefab(string path, string modelPath, Color trailColor, float width, float time)
        {
            var prefab = ProjectilePrefab(path, modelPath, trailColor, width, time);
            var contents = PrefabUtility.LoadPrefabContents(path);
            var homing = contents.GetComponent<HomingProjectile>();
            if (homing != null)
                UnityEngine.Object.DestroyImmediate(homing);
            var bolt = contents.GetComponent<HostileProjectile>();
            if (bolt == null)
                bolt = contents.AddComponent<HostileProjectile>();
            bolt.speed = 9f;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? prefab;
        }

        static void WireRangedBolt(GameObject prefab, GameObject bolt)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            var ai = root.GetComponent<EnemyChaseAI>();
            if (ai != null)
            {
                ai.boltPrefab = bolt;
                ai.arenaRadius = RunRules.ArenaRadius;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static GameObject FireNovaPrefab(string path)
        {
            const string meshPath = "Assets/Art/VFX/MESH_Annulus.asset";
            if (File.Exists(meshPath))
                AssetDatabase.DeleteAsset(meshPath);
            var mesh = FireNova.BuildAnnulus(48, 0.72f, 0.88f, "MESH_Annulus");
            AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = trailMaterial;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            var trail = new GameObject("Trail");
            trail.transform.SetParent(root.transform, false);
            trail.AddComponent<MeshFilter>().sharedMesh = mesh;
            var trailRenderer = trail.AddComponent<MeshRenderer>();
            trailRenderer.sharedMaterial = trailMaterial;
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;

            var flameObject = new GameObject("Flames");
            flameObject.transform.SetParent(root.transform, false);
            flameObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var flames = flameObject.AddComponent<ParticleSystem>();
            var flameMain = flames.main;
            flameMain.loop = false;
            flameMain.playOnAwake = false;
            flameMain.duration = 0.55f;
            flameMain.startLifetime = 0.5f;
            flameMain.startSpeed = 4f;
            flameMain.startSize = 0.42f;
            flameMain.startColor = new Color(1f, 0.55f, 0.15f);
            flameMain.maxParticles = 80;
            flameMain.gravityModifier = -0.5f;
            flameMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var flameEmission = flames.emission;
            flameEmission.rateOverTime = 0f;
            flameEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 56) });
            var flameShape = flames.shape;
            flameShape.shapeType = ParticleSystemShapeType.Circle;
            flameShape.radius = 0.35f;
            flameShape.radiusThickness = 0f;
            var flameColor = flames.colorOverLifetime;
            flameColor.enabled = true;
            var flameGradient = new Gradient();
            flameGradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            flameColor.color = flameGradient;
            var flameSize = flames.sizeOverLifetime;
            flameSize.enabled = true;
            flameSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var flameRenderer = flameObject.GetComponent<ParticleSystemRenderer>();
            flameRenderer.sharedMaterial = additiveParticleMaterial;
            flameRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var emberObject = new GameObject("Embers");
            emberObject.transform.SetParent(root.transform, false);
            var embers = emberObject.AddComponent<ParticleSystem>();
            var emberMain = embers.main;
            emberMain.loop = false;
            emberMain.playOnAwake = false;
            emberMain.startLifetime = 0.7f;
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            emberMain.startSize = 0.1f;
            emberMain.startColor = new Color(1f, 0.75f, 0.3f);
            emberMain.maxParticles = 32;
            emberMain.gravityModifier = -0.8f;
            emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var emberEmission = embers.emission;
            emberEmission.rateOverTime = 0f;
            emberEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var emberShape = embers.shape;
            emberShape.shapeType = ParticleSystemShapeType.Sphere;
            emberShape.radius = 0.35f;
            var emberRenderer = emberObject.GetComponent<ParticleSystemRenderer>();
            emberRenderer.sharedMaterial = additiveParticleMaterial;
            emberRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var nova = root.AddComponent<FireNova>();
            nova.ring = ring.transform;
            nova.trail = trail.transform;
            nova.flames = flames;
            nova.embers = embers;
            nova.color = new Color(1f, 0.5f, 0.15f);
            return SavePrefab(root, path);
        }

        static GameObject BiomeObstacle(string path, Obstacle.Kind kind, float footprint, GameObject part, params Biome[] biomes)
        {
            var prefab = ObstaclePrefab(path, kind, footprint, part);
            var marker = prefab.GetComponent<Obstacle>();
            if (marker != null)
            {
                marker.allowedBiomes = biomes;
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
            return prefab;
        }

        static GameObject ObstaclePrefab(string path, Obstacle.Kind kind, float footprint, params GameObject[] parts)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                    continue;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(parts[i]);
                visual.transform.SetParent(root.transform, false);
                visual.name = parts[i].name;
                if (parts.Length > 1)
                {
                    float side = i == 0 ? -0.28f : 0.32f;
                    visual.transform.localPosition = new Vector3(side, 0f, i == 0 ? 0.1f : -0.12f);
                    visual.transform.localScale = parts[i].transform.localScale * (i == 0 ? 1f : 0.82f);
                }
                else if (path.Contains("Boulder"))
                    visual.transform.localScale = parts[i].transform.localScale * 1.35f;
            }

            if (kind == Obstacle.Kind.Tree)
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 1.15f, 0f);
                capsule.radius = 0.62f;
                capsule.height = 2.4f;
            }
            else if (kind == Obstacle.Kind.Ruin)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.9f, 0f);
                box.size = new Vector3(0.95f, 1.85f, 0.95f);
            }
            else
            {
                var sphere = root.AddComponent<SphereCollider>();
                sphere.center = new Vector3(0f, 0.55f, 0f);
                sphere.radius = path.Contains("Boulder") ? 1.05f : 0.92f;
            }

            var marker = root.AddComponent<Obstacle>();
            marker.kind = kind;
            marker.footprint = footprint;
            SetLayerRecursive(root.transform, Obstacles.Layer);
            return SavePrefab(root, path);
        }

        static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        static GameObject WeatherPrefab(string path, Color color, int count, float speed, float size, float gravity, ParticleSystemShapeType shapeType, float shapeSize, string ambience, bool streaks)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = streaks ? 0.7f : 2.4f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = count * 3;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = count;
            var shape = particles.shape;
            shape.shapeType = shapeType;
            if (shapeType == ParticleSystemShapeType.Box)
                shape.scale = new Vector3(shapeSize, 1.2f, shapeSize);
            else
                shape.radius = shapeSize;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = additiveParticleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (streaks)
            {
                renderer.lengthScale = 2.4f;
                renderer.velocityScale = 0.12f;
            }
            var rig = root.AddComponent<WeatherRig>();
            rig.height = 10f;
            rig.ambienceId = ambience;
            return SavePrefab(root, path);
        }

        static GameObject PortalPrefab(string path, GameObject runePrefab)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var frame = runePrefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(runePrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            frame.name = "Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            frame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            frame.transform.localScale = Vector3.one * 1.35f;
            foreach (var collider in frame.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            core.transform.localScale = Vector3.one * 0.85f;
            var coreRenderer = core.GetComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = TranslucentMaterial("Assets/Art/VFX/Materials/MAT_Portal_Core.mat", new Color(0.35f, 0.9f, 1f, 0.35f));
            coreRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 6f;
            glow.intensity = 1.8f;
            glow.color = new Color(0.35f, 0.9f, 1f);
            glow.shadows = LightShadows.None;

            var swirlObject = new GameObject("Swirl");
            swirlObject.transform.SetParent(root.transform, false);
            swirlObject.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var swirl = swirlObject.AddComponent<ParticleSystem>();
            var main = swirl.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.4f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.45f, 0.95f, 1f);
            main.maxParticles = 40;
            var emission = swirl.emission;
            emission.rateOverTime = 22f;
            var shape = swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
            var swirlRenderer = swirlObject.GetComponent<ParticleSystemRenderer>();
            swirlRenderer.sharedMaterial = additiveParticleMaterial;
            swirlRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var portal = root.AddComponent<ExitPortal>();
            portal.frame = frame.transform;
            portal.core = core.transform;
            portal.glow = glow;
            portal.color = new Color(0.3f, 0.9f, 1f);
            return SavePrefab(root, path);
        }

        static GameObject LightningPrefab(string path)
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = trailMaterial;
            line.startWidth = 0.1f;
            line.endWidth = 0.05f;
            line.numCapVertices = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 1f, 0.75f), 0f), new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            line.colorGradient = gradient;
            go.AddComponent<LightningBolt>();
            return SavePrefab(go, path);
        }

        static GameObject PowerUpPrefab(string path)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var crystal = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("Assets/Art/VFX/VFX_PowerUp_Crystal.fbx"));
            crystal.name = "Crystal";
            crystal.transform.SetParent(root.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 1f, 0f);
            crystal.transform.localScale = Vector3.one * 1.45f;

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            beam.transform.localScale = new Vector3(0.28f, 2.2f, 0.28f);
            var beamRenderer = beam.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = TranslucentMaterial("Assets/Art/VFX/Materials/MAT_PowerUp_Beam.mat", new Color(1f, 1f, 1f, 0.12f));
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;

            var sparkObject = new GameObject("Sparkle");
            sparkObject.transform.SetParent(root.transform, false);
            sparkObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var sparkle = sparkObject.AddComponent<ParticleSystem>();
            var main = sparkle.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.9f;
            main.startSize = 0.12f;
            main.startColor = Color.white;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = sparkle.emission;
            emission.rateOverTime = 18f;
            var shape = sparkle.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            var velocity = sparkle.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(1.2f);
            var sparkRenderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.sharedMaterial = additiveParticleMaterial;
            sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var pickup = root.AddComponent<PowerUpPickup>();
            pickup.crystal = crystal.transform;
            pickup.beam = beam.transform;
            return SavePrefab(root, path);
        }

        static Material TranslucentMaterial(string path, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetColor("_BaseColor", color);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CharacterPrefab(string path, string modelPath, RuntimeAnimatorController controller, float expectedHeight, bool player, EnemyDefinition definition, GameObject xp)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(modelPath));
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual, expectedHeight);
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                skinned.updateWhenOffscreen = false;
            if (player)
            {
                var body = root.AddComponent<CharacterController>();
                body.height = 1.3f;
                body.radius = 0.28f;
                body.center = new Vector3(0f, 0.65f, 0f);
                body.skinWidth = 0.03f;
                body.stepOffset = 0.2f;
                root.AddComponent<PlayerHealth>();
                var mover = root.AddComponent<PlayerController>();
                mover.arenaRadius = RunRules.ArenaRadius;
                root.AddComponent<DashController>();
                root.AddComponent<AutoAttackController>();
                root.AddComponent<SkillController>();
            }
            else
            {
                var health = root.AddComponent<EnemyHealth>();
                health.definition = definition;
                health.xpOrbPrefab = xp;
                var chase = root.AddComponent<EnemyChaseAI>();
                chase.arenaRadius = RunRules.ArenaRadius;
            }
            return SavePrefab(root, path);
        }

        static void ConfigureHero(GameObject heroPrefab, GameObject bolt, GameObject shard, GameObject missile, GameObject fireRing, GameObject lightning)
        {
            var path = AssetDatabase.GetAssetPath(heroPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            var attack = root.GetComponent<AutoAttackController>();
            attack.boltPrefab = bolt;
            var skills = root.GetComponent<SkillController>();
            skills.iceShardPrefab = shard;
            skills.missilePrefab = missile;
            skills.fireRingPrefab = fireRing;
            skills.lightningPrefab = lightning;
            var tip = SkillController.FindDeep(root.transform, "StaffTip");
            Debug.Log("Hero StaffTip " + (tip != null ? tip.position.ToString("F2") : "tidak ditemukan"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static GameObject BossPrefab(string path, RuntimeAnimatorController controller, EnemyDefinition definition)
        {
            var root = new GameObject("BOSS_SlimeKing");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx"));
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual, 2.1f);
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("Assets/Art/VFX/VFX_Boss_Ring.fbx"));
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            foreach (var renderer in ring.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            ring.SetActive(false);
            var health = root.AddComponent<EnemyHealth>();
            health.definition = definition;
            var boss = root.AddComponent<BossController>();
            boss.ring = ring.transform;
            boss.arenaRadius = RunRules.ArenaRadius;
            return SavePrefab(root, path);
        }

        static GameObject StaticPrefab(string path, string modelPath, float expectedHeight, bool ground)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(modelPath));
            instance.name = Path.GetFileNameWithoutExtension(path);
            FitHeight(instance, expectedHeight);
            if (!ground)
            {
                bool small = path.Contains("Grass") || path.Contains("Flower") || path.Contains("Mushroom") || path.Contains("RuneRing");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = small ? ShadowCastingMode.Off : ShadowCastingMode.On;
                return SavePrefab(instance, path);
            }

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            instance.transform.SetParent(root.transform, false);
            instance.name = "Visual";
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.5f, 0f);
            box.size = new Vector3(100f, 1f, 100f);
            return SavePrefab(root, path);
        }

        static void FitHeight(GameObject visual, float expectedHeight)
        {
            var renderer = visual.GetComponentInChildren<Renderer>();
            if (renderer == null)
                return;
            float height = renderer.bounds.size.y;
            Debug.Log(visual.name + " bounds " + renderer.bounds.size);
            if (height > expectedHeight * 4f || (height > 0.001f && height < expectedHeight * 0.25f))
                visual.transform.localScale *= expectedHeight / height;
        }

        static GameObject SavePrefab(GameObject instance, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        // ------------------------------------------------------------------ scenes

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera("Camera", new Color(0.08f, 0.1f, 0.18f));
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            new GameObject("Boot").AddComponent<BootLoader>();
            AttachGraphics(new GameObject("Graphics"));
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        }

        static void BuildMenu(GameObject hero, RuntimeAnimatorController heroController, Dictionary<string, GameObject> env)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = MakeCamera("Camera", new Color(0.09f, 0.12f, 0.2f));
            camera.fieldOfView = 36f;
            camera.transform.position = new Vector3(0.1f, 1.35f, -4.3f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.85f, 0.9f, 0f) - camera.transform.position, Vector3.up);
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            var sun = Light();
            sun.transform.rotation = Quaternion.Euler(42f, 18f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.3f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.18f);
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.09f, 0.12f, 0.2f);
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 30f;

            // Showcase: ground, rune ring and the idle sorceress to the right of the menu.
            var ground = (GameObject)PrefabUtility.InstantiatePrefab(env["ground"]);
            ground.transform.position = Vector3.zero;
            var rune = (GameObject)PrefabUtility.InstantiatePrefab(env["RuneRing"]);
            rune.transform.position = new Vector3(1.55f, 0.012f, 0f);
            rune.transform.localScale = Vector3.one * 0.55f;
            var showcase = new GameObject("HeroShowcase");
            showcase.transform.position = new Vector3(1.55f, 0f, 0f);
            showcase.transform.rotation = Quaternion.Euler(0f, 195f, 0f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("Assets/Art/Characters/Hero/CHR_Hero_Main.fbx"));
            model.transform.SetParent(showcase.transform, false);
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = heroController;
            animator.applyRootMotion = false;
            var rng = new System.Random(5);
            Scatter(env["Grass"], 14, 0.8f, 3.2f, rng, new Vector3(1.55f, 0f, 0f));
            Scatter(env["Flower"], 5, 1f, 3f, rng, new Vector3(1.55f, 0f, 0f));
            Scatter(env["Tree_A"], 5, 6f, 11f, rng, new Vector3(1.55f, 0f, 2f));
            Scatter(env["Tree_B"], 4, 6f, 12f, rng, new Vector3(1.55f, 0f, 2f));
            Scatter(env["Bush"], 4, 3.5f, 6f, rng, new Vector3(1.55f, 0f, 1.5f));

            var audio = new GameObject("Audio").AddComponent<AudioService>();
            audio.music = Clip("Assets/Audio/Music/MUS_Menu.ogg");
            audio.musicVolume = 0.4f;
            audio.entries = new[] { Sfx("ui", "SFX_UI", 0.7f, 0.03f) };

            EventSystem();
            Canvas(out var safe);
            var menu = new GameObject("Menu").AddComponent<MainMenuController>();
            menu.showcase = showcase.transform;
            var font = Font();
            var title = Text(safe, "Title", "Slime Breaker", 84, TextAnchor.MiddleLeft, font);
            title.color = new Color(1f, 0.92f, 0.7f);
            title.fontStyle = FontStyle.Bold;
            Place(title.gameObject, safe, new Vector2(0.3f, 0.8f), new Vector2(760f, 120f));
            var subtitle = Text(safe, "Subtitle", "Satu arena. 100 level. 10 slime boss.", 30, TextAnchor.MiddleLeft, font);
            subtitle.color = new Color(0.8f, 0.85f, 1f);
            Place(subtitle.gameObject, safe, new Vector2(0.3f, 0.715f), new Vector2(760f, 50f));
            var gold = Text(safe, "Gold", "Emas 0", 36, TextAnchor.MiddleLeft, font);
            Place(gold.gameObject, safe, new Vector2(0.3f, 0.62f), new Vector2(500f, 60f));
            var best = Text(safe, "Best", "Rekor 00:00", 28, TextAnchor.MiddleLeft, font);
            Place(best.gameObject, safe, new Vector2(0.3f, 0.565f), new Vector2(500f, 50f));
            var play = Button(safe, "Play", "Main", font, Color.white, goldSprite);
            play.GetComponentInChildren<Text>().fontSize = 40;
            Place(play.gameObject, safe, new Vector2(0.3f, 0.46f), new Vector2(440f, 108f));
            var cont = Button(safe, "Continue", "Lanjutkan", font, Color.white, buttonSprite);
            Place(cont.gameObject, safe, new Vector2(0.3f, 0.355f), new Vector2(440f, 92f));
            var upgradesLabel = Text(safe, "UpgradesLabel", "Peningkatan permanen", 26, TextAnchor.MiddleLeft, font);
            Place(upgradesLabel.gameObject, safe, new Vector2(0.3f, 0.26f), new Vector2(760f, 40f));
            var damage = Button(safe, "Damage", "Damage", font, Color.white, buttonSprite);
            Place(damage.gameObject, safe, new Vector2(0.155f, 0.155f), new Vector2(300f, 110f));
            var health = Button(safe, "Health", "Nyawa", font, Color.white, buttonSprite);
            Place(health.gameObject, safe, new Vector2(0.325f, 0.155f), new Vector2(300f, 110f));
            var speed = Button(safe, "Speed", "Laju", font, Color.white, buttonSprite);
            Place(speed.gameObject, safe, new Vector2(0.495f, 0.155f), new Vector2(300f, 110f));
            var settingsButton = Button(safe, "Settings", "⚙", font, Color.white, circleSprite);
            settingsButton.GetComponentInChildren<Text>().fontSize = 40;
            Place(settingsButton.gameObject, safe, new Vector2(0.955f, 0.925f), new Vector2(84f, 84f));
            var settings = BuildSettingsOverlay(safe, font);
            menu.goldText = gold;
            menu.bestText = best;
            menu.damageButton = damage;
            menu.healthButton = health;
            menu.speedButton = speed;
            menu.damageLabel = damage.GetComponentInChildren<Text>();
            menu.healthLabel = health.GetComponentInChildren<Text>();
            menu.speedLabel = speed.GetComponentInChildren<Text>();
            menu.continueButton = cont;
            menu.continueLabel = cont.GetComponentInChildren<Text>();
            menu.settings = settings;
            AttachGraphics(new GameObject("Graphics"));
            UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
            UnityEventTools.AddPersistentListener(cont.onClick, menu.Continue);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, menu.OpenSettings);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        static void BuildArena(
            BalanceConfig balance,
            UpgradeDefinition[] upgrades,
            SkillDefinition[] skills,
            GameObject hero,
            GameObject normal,
            GameObject fast,
            GameObject tank,
            GameObject ranged,
            GameObject boss,
            EnemyDefinition[] bossDefs,
            GameObject hostileBolt,
            GameObject lightning,
            Dictionary<string, GameObject> env,
            List<VfxPlayer.Entry> vfxEntries,
            GameObject powerUpPrefab,
            GameObject portalPrefab,
            GameObject[] obstaclePrefabs,
            GameObject rain,
            GameObject snowWx,
            GameObject fireflies,
            GameObject dust,
            GameObject ash,
            GameObject spores,
            GameObject sparks)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var groundInstance = (GameObject)PrefabUtility.InstantiatePrefab(env["ground"]);
            groundInstance.transform.position = Vector3.zero;

            var player = (GameObject)PrefabUtility.InstantiatePrefab(hero);
            player.transform.position = Vector3.zero;
            var camera = MakeCamera("Camera", new Color(0.36f, 0.56f, 0.7f));
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 90f;
            var follow = camera.gameObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            camera.transform.position = follow.offset;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up * follow.lookHeight - follow.offset, Vector3.up);
            player.GetComponent<PlayerController>().followCamera = follow;
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            var sun = Light();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.7f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.5f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.12f);
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.52f, 0.68f, 0.76f);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 80f;
            EventSystem();

            var systems = new GameObject("Systems");
            var flow = systems.AddComponent<GameFlowController>();
            var run = systems.AddComponent<RunManager>();
            var spawner = systems.AddComponent<EnemySpawner>();
            var xp = systems.AddComponent<XPManager>();
            var upgradeManager = systems.AddComponent<UpgradeManager>();
            var audio = systems.AddComponent<AudioService>();
            var vfx = systems.AddComponent<VfxPlayer>();
            var powerUps = systems.AddComponent<PowerUpSpawner>();
            var generator = systems.AddComponent<ArenaGenerator>();
            var stage = systems.AddComponent<StageManager>();
            AttachGraphics(systems);
            flow.balance = balance;
            flow.run = run;
            flow.upgrades = upgradeManager;
            flow.audioService = audio;
            flow.powerUps = powerUps;
            run.balance = balance;
            run.spawner = spawner;
            spawner.balance = balance;
            spawner.run = run;
            spawner.normalPrefab = normal;
            spawner.fastPrefab = fast;
            spawner.tankPrefab = tank;
            spawner.rangedPrefab = ranged;
            spawner.bossPrefab = boss;
            spawner.hostileBolt = hostileBolt;
            spawner.arenaRadius = RunRules.ArenaRadius;
            upgradeManager.pool = upgrades;
            powerUps.skills = skills;
            powerUps.pickupPrefab = powerUpPrefab;
            powerUps.everyLevels = 5;
            generator.props = new ArenaGenerator.PropSet
            {
                treeA = env["Tree_A"],
                treeB = env["Tree_B"],
                rockA = env["Rock_A"],
                rockB = env["Rock_B"],
                mushroom = env["Mushroom"],
                bush = env["Bush"],
                pillar = env["RuinPillar"],
                grass = env["Grass"],
                flower = env["Flower"],
                boundaryStone = env["BoundaryStone"],
                runeRing = env["RuneRing"],
                cactus = env["Cactus"],
                iceSpike = env["IceSpike"],
                thunderSpire = env["ThunderSpire"],
                swampRoot = env["SwampRoot"],
                swampReed = env["SwampReed"],
                shadowObelisk = env["ShadowObelisk"],
                volcanoRock = env["VolcanoRock"],
                volcanoPillar = env["VolcanoPillar"],
                stormArch = env["StormArch"],
                crystalSpire = env["CrystalSpire"],
                royalColumn = env["RoyalColumn"]
            };
            generator.obstaclePrefabs = obstaclePrefabs;
            generator.groundRenderer = groundInstance.GetComponentInChildren<Renderer>();
            generator.forestGround = forestGroundMaterial;
            generator.snowGround = snowGroundMaterial;
            generator.sandGround = sandGroundMaterial;
            generator.thunderGround = thunderGroundMaterial;
            generator.swampGround = swampGroundMaterial;
            generator.shadowGround = shadowGroundMaterial;
            generator.volcanoGround = volcanoGroundMaterial;
            generator.stormGround = stormGroundMaterial;
            generator.crystalGround = crystalGroundMaterial;
            generator.royalGround = royalGroundMaterial;
            generator.rainRig = rain;
            generator.snowRig = snowWx;
            generator.firefliesRig = fireflies;
            generator.dustRig = dust;
            generator.ashRig = ash;
            generator.sporesRig = spores;
            generator.sparksRig = sparks;
            generator.sun = sun;
            generator.arenaCamera = camera;
            generator.arenaRadius = RunRules.ArenaRadius;
            generator.minObstacles = 10;
            generator.maxObstacles = 16;
            generator.generateOnStart = false;
            stage.generator = generator;
            stage.powerUps = powerUps;
            stage.spawner = spawner;
            stage.bossPrefab = boss;
            stage.bossDefinitions = bossDefs;
            stage.hostileBolt = hostileBolt;
            stage.lightningPrefab = lightning;
            stage.minionPrefab = normal;
            stage.portalPrefab = portalPrefab;
            stage.cameraRig = follow;
            audio.music = Clip("Assets/Audio/Music/MUS_Arena.ogg");
            audio.entries = new[]
            {
                Sfx("hit", "SFX_Hit", 0.42f, 0.05f, 0.1f),
                Sfx("bolthit", "SFX_BoltHit", 0.5f, 0.04f, 0.1f),
                Sfx("cast", "SFX_Cast", 0.32f, 0.08f, 0.08f),
                Sfx("enemydeath", "SFX_EnemyDeath", 0.55f, 0.06f, 0.12f),
                Sfx("playerhit", "SFX_PlayerHit", 0.85f, 0.1f),
                Sfx("dash", "SFX_Dash", 0.7f, 0.08f),
                Sfx("pickup", "SFX_Pickup", 0.4f, 0.03f, 0.14f),
                Sfx("levelup", "SFX_LevelUp", 0.9f, 0.2f),
                Sfx("powerupspawn", "SFX_PowerUpSpawn", 0.9f, 0.2f),
                Sfx("powerup", "SFX_PowerUp", 0.95f, 0.2f),
                Sfx("ice", "SFX_Ice", 0.5f, 0.08f),
                Sfx("freeze", "SFX_Freeze", 0.38f, 0.14f, 0.1f),
                Sfx("lightning", "SFX_Lightning", 0.7f, 0.08f),
                Sfx("fire", "SFX_Fire", 0.7f, 0.12f),
                Sfx("arcane", "SFX_Arcane", 0.55f, 0.1f),
                Sfx("arcanehit", "SFX_BoltHit", 0.45f, 0.05f, 0.15f),
                Sfx("bosswarn", "SFX_BossWarn", 0.9f, 0.3f),
                Sfx("slam", "SFX_BossSlam", 0.9f, 0.1f),
                Sfx("ui", "SFX_UI", 0.7f, 0.03f),
                Sfx("victory", "SFX_Victory", 1f, 0.5f, 0f),
                Sfx("defeat", "SFX_Defeat", 1f, 0.5f, 0f),
                Sfx("portal", "SFX_Portal", 0.85f, 0.2f, 0f),
                Sfx("rain", "SFX_Rain", 0.35f, 0f, 0f),
                Sfx("wind", "SFX_Wind", 0.28f, 0f, 0f)
            };
            vfx.entries = vfxEntries.ToArray();

            var ui = BuildHud(player.GetComponent<DashController>(), flow);
            flow.ui = ui;
            stage.fader = ui.fader;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameArena.unity");
            AssetDatabase.ImportAsset("Assets/Scenes/GameArena.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/GameArena.unity", true)
            };
            Debug.Log("Arena built. XP manager " + (xp != null));
        }

        static GraphicsService AttachGraphics(GameObject host)
        {
            var graphics = host.GetComponent<GraphicsService>();
            if (graphics == null)
                graphics = host.AddComponent<GraphicsService>();
            graphics.presets = graphicsPresets;
            return graphics;
        }

        static SettingsPanel BuildSettingsOverlay(RectTransform safe, Font font)
        {
            var host = new GameObject("SettingsHost", typeof(RectTransform));
            host.transform.SetParent(safe, false);
            var panel = host.AddComponent<SettingsPanel>();
            var dim = Panel(safe, "Settings", font, "Pengaturan Grafis");
            var low = Button(dim.transform, "Low", "Low", font, Color.white, buttonSprite);
            Place(low.gameObject, dim.transform, new Vector2(0.28f, 0.46f), new Vector2(280f, 110f));
            var medium = Button(dim.transform, "Medium", "Medium", font, Color.white, buttonSprite);
            Place(medium.gameObject, dim.transform, new Vector2(0.5f, 0.46f), new Vector2(280f, 110f));
            var high = Button(dim.transform, "High", "High", font, Color.white, goldSprite);
            Place(high.gameObject, dim.transform, new Vector2(0.72f, 0.46f), new Vector2(280f, 110f));
            var close = Button(dim.transform, "Close", "Tutup", font, Color.white, dangerSprite);
            Place(close.gameObject, dim.transform, new Vector2(0.5f, 0.22f), new Vector2(300f, 90f));
            var title = dim.GetComponentInChildren<Text>();
            panel.panel = dim;
            panel.lowButton = low;
            panel.mediumButton = medium;
            panel.highButton = high;
            panel.closeButton = close;
            panel.title = title;
            dim.SetActive(false);
            return panel;
        }

        static AudioService.Entry Sfx(string id, string file, float volume, float minInterval, float jitter = 0.06f)
        {
            return new AudioService.Entry
            {
                id = id,
                clip = Clip("Assets/Audio/SFX/" + file + ".ogg"),
                volume = volume,
                minInterval = minInterval,
                pitchJitter = jitter
            };
        }

        static UIController BuildHud(DashController dash, GameFlowController flow)
        {
            var canvas = Canvas(out var safe);
            var font = Font();
            var uiObject = new GameObject("UI");
            var ui = uiObject.AddComponent<UIController>();

            // Top-left: health + experience.
            var hp = Slider(safe, "HP", new Color(0.9f, 0.3f, 0.36f), barSprite);
            Place(hp.gameObject, safe, new Vector2(0.16f, 0.935f), new Vector2(470f, 40f));
            var hpText = Text(hp.transform, "Value", "100 / 100", 22, TextAnchor.MiddleCenter, font);
            Stretch(hpText.rectTransform);
            var xp = Slider(safe, "EXP", new Color(0.45f, 0.92f, 0.45f), barSprite);
            Place(xp.gameObject, safe, new Vector2(0.16f, 0.883f), new Vector2(470f, 22f));
            var xpLabel = Text(xp.transform, "Label", "EXP", 16, TextAnchor.MiddleLeft, font);
            xpLabel.raycastTarget = false;
            Place(xpLabel.gameObject, xp.transform, new Vector2(0.08f, 0.5f), new Vector2(70f, 18f));
            var level = Text(safe, "Level", "Lv 1", 30, TextAnchor.MiddleLeft, font);
            level.fontStyle = FontStyle.Bold;
            Place(level.gameObject, safe, new Vector2(0.33f, 0.948f), new Vector2(140f, 36f));
            var damage = Text(safe, "Damage", "ATK 12", 24, TextAnchor.MiddleLeft, font);
            damage.fontStyle = FontStyle.Bold;
            Place(damage.gameObject, safe, new Vector2(0.33f, 0.905f), new Vector2(160f, 28f));
            var floor = Text(safe, "Floor", "Lantai 1/100", 28, TextAnchor.MiddleCenter, font);
            floor.fontStyle = FontStyle.Bold;
            Place(floor.gameObject, safe, new Vector2(0.52f, 0.935f), new Vector2(260f, 40f));
            var timer = Text(safe, "Timer", "00:00", 34, TextAnchor.MiddleCenter, font);
            Place(timer.gameObject, safe, new Vector2(0.72f, 0.935f), new Vector2(180f, 48f));
            var pause = Button(safe, "Pause", "II", font, Color.white, circleSprite);
            pause.GetComponentInChildren<Text>().fontSize = 34;
            Place(pause.gameObject, safe, new Vector2(0.955f, 0.925f), new Vector2(84f, 84f));

            // Top-right under pause: owned skills.
            var slots = new GameObject[4];
            var slotIcons = new Image[4];
            var slotLevels = new Text[4];
            var slotFrames = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var slot = new GameObject("Skill" + i, typeof(RectTransform), typeof(Image));
                ApplyImage(slot.GetComponent<Image>(), slotSprite, Image.Type.Sliced, Color.white);
                Place(slot, safe, new Vector2(0.955f - i * 0.058f, 0.815f), new Vector2(92f, 92f));
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                Place(iconObject, slot.transform, new Vector2(0.5f, 0.56f), new Vector2(60f, 60f));
                ApplyImage(iconObject.GetComponent<Image>(), whiteSprite, Image.Type.Simple, Color.white);
                iconObject.GetComponent<Image>().preserveAspect = true;
                var levelText = Text(slot.transform, "Level", "Lv 1", 18, TextAnchor.MiddleCenter, font);
                Place(levelText.gameObject, slot.transform, new Vector2(0.5f, 0.14f), new Vector2(90f, 24f));
                slots[i] = slot;
                slotIcons[i] = iconObject.GetComponent<Image>();
                slotLevels[i] = levelText;
                slotFrames[i] = slot.GetComponent<Image>();
                slot.SetActive(false);
            }

            // Bottom-left: joystick.
            var stick = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            ApplyImage(stick.GetComponent<Image>(), joystickBaseSprite, Image.Type.Simple, new Color(1f, 1f, 1f, 0.8f));
            stick.GetComponent<Image>().raycastTarget = true;
            Place(stick, safe, new Vector2(0.13f, 0.2f), new Vector2(250f, 250f));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(stick.transform, false);
            ApplyImage(handle.GetComponent<Image>(), joystickKnobSprite, Image.Type.Simple, new Color(1f, 1f, 1f, 0.95f));
            handle.GetComponent<Image>().raycastTarget = false;
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(104f, 104f);
            handleRect.anchoredPosition = Vector2.zero;
            var joystick = stick.GetComponent<VirtualJoystick>();
            joystick.handle = handleRect;
            joystick.radius = 78f;

            // Bottom-right: dash button with a radial cooldown sweep.
            var dashButton = Button(safe, "Dash", "", font, Color.white, circleSprite);
            Place(dashButton.gameObject, safe, new Vector2(0.875f, 0.2f), new Vector2(190f, 190f));
            var dashFillObject = new GameObject("DashFill", typeof(RectTransform), typeof(Image));
            dashFillObject.transform.SetParent(dashButton.transform, false);
            var dashFill = dashFillObject.GetComponent<Image>();
            ApplyImage(dashFill, circleSprite, Image.Type.Filled, new Color(0.55f, 0.9f, 1f, 0.55f));
            dashFill.fillMethod = Image.FillMethod.Radial360;
            dashFill.fillOrigin = (int)Image.Origin360.Top;
            dashFill.fillClockwise = true;
            dashFill.fillAmount = 1f;
            dashFill.raycastTarget = false;
            Stretch(dashFill.rectTransform);
            dashFill.rectTransform.offsetMin = new Vector2(10f, 10f);
            dashFill.rectTransform.offsetMax = new Vector2(-10f, -10f);
            var dashLabel = Text(dashButton.transform, "Label2", "DASH", 30, TextAnchor.MiddleCenter, font);
            dashLabel.fontStyle = FontStyle.Bold;
            dashLabel.raycastTarget = false;
            Stretch(dashLabel.rectTransform);

            // Toast for power-ups and skill grants.
            var toast = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var toastImage = toast.GetComponent<Image>();
            ApplyImage(toastImage, panelSprite, Image.Type.Sliced, new Color(0.1f, 0.1f, 0.2f, 0.8f));
            toastImage.raycastTarget = false;
            Place(toast, safe, new Vector2(0.5f, 0.73f), new Vector2(760f, 76f));
            var toastText = Text(toast.transform, "Text", "", 32, TextAnchor.MiddleCenter, font);
            toastText.raycastTarget = false;
            Stretch(toastText.rectTransform);
            var toastGroup = toast.GetComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            var warning = new GameObject("BossWarning", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var warningImage = warning.GetComponent<Image>();
            ApplyImage(warningImage, panelSprite, Image.Type.Sliced, new Color(0.18f, 0.05f, 0.04f, 0.88f));
            warningImage.raycastTarget = false;
            Place(warning, safe, new Vector2(0.5f, 0.62f), new Vector2(860f, 160f));
            var warningTitle = Text(warning.transform, "Title", "STAGE BOSS", 42, TextAnchor.MiddleCenter, font);
            warningTitle.fontStyle = FontStyle.Bold;
            warningTitle.color = new Color(1f, 0.82f, 0.35f);
            warningTitle.raycastTarget = false;
            Place(warningTitle.gameObject, warning.transform, new Vector2(0.5f, 0.7f), new Vector2(820f, 60f));
            var warningName = Text(warning.transform, "Name", "", 36, TextAnchor.MiddleCenter, font);
            warningName.fontStyle = FontStyle.Bold;
            warningName.raycastTarget = false;
            Place(warningName.gameObject, warning.transform, new Vector2(0.5f, 0.32f), new Vector2(820f, 56f));
            var warningGroup = warning.GetComponent<CanvasGroup>();
            warningGroup.alpha = 0f;
            warningGroup.blocksRaycasts = false;
            warningGroup.interactable = false;

            // Level-up choice panel.
            var levelPanel = Panel(safe, "LevelUp", font, "Naik level! Pilih satu upgrade");
            var choices = new Button[3];
            var labels = new Text[3];
            var icons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                choices[i] = Button(levelPanel.transform, "Choice" + i, "Upgrade", font, Color.white, buttonSprite);
                Place(choices[i].gameObject, levelPanel.transform, new Vector2(0.25f + i * 0.25f, 0.46f), new Vector2(330f, 320f));
                labels[i] = choices[i].GetComponentInChildren<Text>();
                labels[i].alignment = TextAnchor.UpperCenter;
                var labelRect = labels[i].rectTransform;
                labelRect.anchorMin = new Vector2(0.06f, 0.08f);
                labelRect.anchorMax = new Vector2(0.94f, 0.5f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                Place(iconObject, choices[i].transform, new Vector2(0.5f, 0.74f), new Vector2(120f, 120f));
                icons[i] = iconObject.GetComponent<Image>();
                ApplyImage(icons[i], whiteSprite, Image.Type.Simple, Color.white);
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
            }
            levelPanel.SetActive(false);

            var pausePanel = Panel(safe, "Pause", font, "Jeda");
            var resume = Button(pausePanel.transform, "Resume", "Lanjut", font, Color.white, goldSprite);
            Place(resume.gameObject, pausePanel.transform, new Vector2(0.5f, 0.54f), new Vector2(380f, 96f));
            var settingsButton = Button(pausePanel.transform, "Settings", "Pengaturan", font, Color.white, buttonSprite);
            Place(settingsButton.gameObject, pausePanel.transform, new Vector2(0.5f, 0.38f), new Vector2(380f, 96f));
            var menu = Button(pausePanel.transform, "Menu", "Menu", font, Color.white, dangerSprite);
            Place(menu.gameObject, pausePanel.transform, new Vector2(0.5f, 0.22f), new Vector2(380f, 96f));
            pausePanel.SetActive(false);
            var settings = BuildSettingsOverlay(safe, font);

            var resultPanel = Panel(safe, "Result", font, "Hasil");
            var resultTitle = resultPanel.GetComponentInChildren<Text>();
            var resultBody = Text(resultPanel.transform, "Body", "", 34, TextAnchor.MiddleCenter, font);
            Place(resultBody.gameObject, resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(700f, 160f));
            var retry = Button(resultPanel.transform, "Retry", "Ulangi", font, Color.white, goldSprite);
            Place(retry.gameObject, resultPanel.transform, new Vector2(0.36f, 0.24f), new Vector2(300f, 96f));
            var exit = Button(resultPanel.transform, "Exit", "Menu", font, Color.white, dangerSprite);
            Place(exit.gameObject, resultPanel.transform, new Vector2(0.64f, 0.24f), new Vector2(300f, 96f));
            resultPanel.SetActive(false);

            var finder = uiObject.AddComponent<Wayfinder>();
            var portalArrow = WayArrow(safe, font, "PortalArrow", new Color(0.45f, 0.95f, 1f));
            var powerArrow = WayArrow(safe, font, "PowerArrow", new Color(1f, 0.82f, 0.3f));
            finder.portalArrow = portalArrow;
            finder.powerArrow = powerArrow;
            finder.portalLabel = portalArrow.Find("Label") != null ? portalArrow.Find("Label").GetComponent<Text>() : null;
            finder.powerLabel = powerArrow.Find("Label") != null ? powerArrow.Find("Label").GetComponent<Text>() : null;

            var player = dash.GetComponent<PlayerController>();
            player.joystick = joystick;
            ui.hpSlider = hp;
            ui.hpText = hpText;
            ui.xpSlider = xp;
            ui.levelText = level;
            ui.floorText = floor;
            ui.damageText = damage;
            ui.timerText = timer;
            ui.dashFill = dashFill;
            ui.levelUpPanel = levelPanel;
            ui.choiceButtons = choices;
            ui.choiceLabels = labels;
            ui.choiceIcons = icons;
            ui.pausePanel = pausePanel;
            ui.resultPanel = resultPanel;
            ui.resultTitle = resultTitle;
            ui.resultBody = resultBody;
            ui.pauseButton = pause;
            ui.settings = settings;
            ui.skillSlots = slots;
            ui.skillIcons = slotIcons;
            ui.skillLevels = slotLevels;
            ui.skillFrames = slotFrames;
            ui.toastGroup = toastGroup;
            ui.toastText = toastText;
            ui.toastBackground = toastImage;
            ui.bossWarningGroup = warningGroup;
            ui.bossWarningTitle = warningTitle;
            ui.bossWarningName = warningName;
            var fade = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(ScreenFader));
            fade.transform.SetParent(safe, false);
            var fadeImage = fade.GetComponent<Image>();
            ApplyImage(fadeImage, whiteSprite, Image.Type.Simple, Color.black);
            fadeImage.raycastTarget = false;
            Stretch(fade.GetComponent<RectTransform>());
            var fadeGroup = fade.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            var fader = fade.GetComponent<ScreenFader>();
            fader.group = fadeGroup;
            ui.fader = fader;
            UnityEventTools.AddPersistentListener(pause.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(resume.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(settingsButton.onClick, ui.OpenSettings);
            UnityEventTools.AddPersistentListener(menu.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(retry.onClick, flow.Restart);
            UnityEventTools.AddPersistentListener(exit.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(dashButton.onClick, dash.QueueDash);
            canvas.sortingOrder = 0;
            return ui;
        }

        static RectTransform WayArrow(RectTransform parent, Font font, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(88f, 88f);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var glyph = Text(go.transform, "Glyph", "▲", 56, TextAnchor.MiddleCenter, font);
            glyph.fontStyle = FontStyle.Bold;
            glyph.color = color;
            glyph.raycastTarget = false;
            Stretch(glyph.rectTransform);
            var caption = Text(go.transform, "Label", "", 20, TextAnchor.MiddleCenter, font);
            caption.fontStyle = FontStyle.Bold;
            caption.color = color;
            caption.raycastTarget = false;
            Place(caption.gameObject, go.transform, new Vector2(0.5f, -0.18f), new Vector2(160f, 28f));
            go.SetActive(false);
            return rect;
        }

        static void Ring(GameObject prefab, int count, float radius, bool faceCenter, System.Random rng, float jitter = 0f, float scale = 1f)
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count + (float)(rng.NextDouble() - 0.5) * jitter;
                float r = radius + (float)(rng.NextDouble() - 0.5) * jitter * 2f;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                float yaw = faceCenter ? -angle * Mathf.Rad2Deg - 90f : (float)rng.NextDouble() * 360f;
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
                float s = scale * (1f + (float)(rng.NextDouble() - 0.5) * 0.2f);
                instance.transform.localScale = prefab.transform.localScale * s;
                MarkStatic(instance);
            }
        }

        static void Scatter(GameObject prefab, int count, float minRadius, float maxRadius, System.Random rng, Vector3 center)
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)rng.NextDouble()));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f) * prefab.transform.rotation;
                instance.transform.localScale = prefab.transform.localScale * (0.8f + (float)rng.NextDouble() * 0.5f);
                MarkStatic(instance);
            }
        }

        static void MarkStatic(GameObject instance)
        {
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Gitgitmiko";
            PlayerSettings.productName = "Slime Breaker";
            PlayerSettings.bundleVersion = "0.7.0";
            PlayerSettings.Android.bundleVersionCode = 7;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gitgitmiko.slimebreaker");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        }

        static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                Debug.LogWarning("Audio hilang: " + path);
            return clip;
        }

        static Camera MakeCamera(string name, Color background)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.GetUniversalAdditionalCameraData();
            go.AddComponent<UrpCameraRig>();
            go.AddComponent<AudioListener>();
            return camera;
        }

        static Light Light()
        {
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightObject.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            light.GetUniversalAdditionalLightData();
            return light;
        }

        static void EventSystem()
        {
            var systems = new GameObject("EventSystem");
            systems.AddComponent<EventSystem>();
            systems.AddComponent<InputSystemUIInputModule>();
        }

        static Canvas Canvas(out RectTransform safe)
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(EnsureUiSprites));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var safeObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeArea));
            safeObject.transform.SetParent(canvasObject.transform, false);
            safe = safeObject.GetComponent<RectTransform>();
            Stretch(safe);
            return canvas;
        }

        static GameObject Panel(Transform parent, string name, Font font, string title)
        {
            var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(parent, false);
            ApplyImage(dim.GetComponent<Image>(), whiteSprite, Image.Type.Simple, new Color(0.02f, 0.03f, 0.08f, 0.72f));
            Stretch(dim.GetComponent<RectTransform>());
            var panel = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(dim.transform, false);
            ApplyImage(panel.GetComponent<Image>(), panelSprite, Image.Type.Sliced, Color.white);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.14f, 0.1f);
            rect.anchorMax = new Vector2(0.86f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = Text(dim.transform, "Title", title, 50, TextAnchor.MiddleCenter, font);
            label.fontStyle = FontStyle.Bold;
            Place(label.gameObject, dim.transform, new Vector2(0.5f, 0.79f), new Vector2(1000f, 90f));
            return dim;
        }

        static Text Text(Transform parent, string name, string value, int size, TextAnchor anchor, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        static Button Button(Transform parent, string name, string label, Font font, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ApplyImage(go.GetComponent<Image>(), sprite != null ? sprite : buttonSprite, sprite == circleSprite ? Image.Type.Simple : Image.Type.Sliced, color);
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
            button.colors = colors;
            var text = Text(go.transform, "Label", label, 30, TextAnchor.MiddleCenter, font);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return button;
        }

        static Slider Slider(Transform parent, string name, Color color, Sprite background)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var back = new GameObject("Background", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(root.transform, false);
            ApplyImage(back.GetComponent<Image>(), background != null ? background : whiteSprite, background != null ? Image.Type.Sliced : Image.Type.Simple, background != null ? Color.white : new Color(0f, 0f, 0f, 0.45f));
            Stretch(back.GetComponent<RectTransform>());
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var areaRect = fillArea.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(6f, 6f);
            areaRect.offsetMax = new Vector2(-6f, -6f);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);
            ApplyImage(fill.GetComponent<Image>(), whiteSprite, Image.Type.Simple, color);
            var slider = root.AddComponent<Slider>();
            slider.targetGraphic = fill.GetComponent<Image>();
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            return slider;
        }

        static Font Font()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Roboto-Regular.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                throw new InvalidOperationException("Font UI tidak ditemukan.");
            return font;
        }

        static Sprite UiSprite(string name, Sprite fallback)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/" + name + ".png") ?? fallback;
        }

        static void LoadUiArt()
        {
            whiteSprite = EnsureWhiteSprite();
            panelSprite = UiSprite("UI_Panel", whiteSprite);
            buttonSprite = UiSprite("UI_Button", whiteSprite);
            dangerSprite = UiSprite("UI_Button_Danger", buttonSprite);
            goldSprite = UiSprite("UI_Button_Gold", buttonSprite);
            slotSprite = UiSprite("UI_Slot", panelSprite);
            barSprite = UiSprite("UI_Bar_Back", null);
            joystickBaseSprite = UiSprite("UI_Joystick_Base", panelSprite);
            joystickKnobSprite = UiSprite("UI_Joystick_Knob", whiteSprite);
            circleSprite = UiSprite("UI_Circle", buttonSprite);
            if (panelSprite == null || buttonSprite == null || whiteSprite == null)
                throw new InvalidOperationException("Sprite UI tidak ter-load.");
        }

        static Sprite EnsureWhiteSprite()
        {
            const string path = "Assets/Art/UI/Panels/UI_White.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var pixels = new Color[64];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = Color.white;
                tex.SetPixels(pixels);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets/Art/UI/Panels");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void ApplyImage(Image image, Sprite sprite, Image.Type type, Color color)
        {
            image.sprite = sprite != null ? sprite : whiteSprite;
            image.type = type;
            image.color = color;
        }

        static RectTransform Place(GameObject go, Transform parent, Vector2 anchor, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
