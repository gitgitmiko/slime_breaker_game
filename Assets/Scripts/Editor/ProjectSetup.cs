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
                "Assets/ScriptableObjects/Balance", "Assets/Scenes", "Assets/Art/Textures"
            })
                Directory.CreateDirectory(dir);

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
            var bossDef = Enemy("Assets/ScriptableObjects/Enemies/BOSS_SlimeKing.asset", "Slime King", 520, 2.05f, 16, 1.7f, 0.8f, 0, true);

            var upgrades = new[]
            {
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Damage.asset", UpgradeKind.Damage, "Serangan", "+20% damage sihir", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Damage.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Speed.asset", UpgradeKind.Speed, "Laju", "+12% kecepatan gerak", 0.12f, "Assets/Art/UI/Icons/ICON_Upgrade_Speed.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Health.asset", UpgradeKind.Health, "Nyawa", "+25 nyawa maksimum", 25f, "Assets/Art/UI/Icons/ICON_Upgrade_Health.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_AttackRate.asset", UpgradeKind.AttackRate, "Ritme", "Mantra lebih cepat", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_AttackRate.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Dash.asset", UpgradeKind.Dash, "Dash", "Dash lebih sering", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_Dash.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Range.asset", UpgradeKind.Range, "Jangkauan", "+20% jangkauan mantra", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Range.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Xp.asset", UpgradeKind.Xp, "Pengalaman", "+25% XP", 0.25f, "Assets/Art/UI/Icons/ICON_Upgrade_Xp.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Armor.asset", UpgradeKind.Armor, "Perisai", "Damage masuk -8%", 0.08f, "Assets/Art/UI/Icons/ICON_Upgrade_Armor.png")
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
            balance.phases = new[]
            {
                Phase(45f, 2.3f, 1, 0, 0),
                Phase(120f, 1.7f, 3, 2, 0),
                Phase(210f, 1.35f, 3, 2, 1),
                Phase(300f, 1.0f, 2, 3, 2)
            };
            EditorUtility.SetDirty(balance);

            var xpPrefab = PickupPrefab<XPOrb>("Assets/Prefabs/Pickups/VFX_XP_Orb.prefab", "Assets/Art/VFX/VFX_XP_Orb.fbx");
            var bolt = ProjectilePrefab("Assets/Prefabs/Skills/PRJ_Magic_Bolt.prefab", "Assets/Art/VFX/VFX_Magic_Bolt.fbx", new Color(0.85f, 0.6f, 1f), 0.16f, 0.22f);
            var missile = ProjectilePrefab("Assets/Prefabs/Skills/PRJ_Arcane_Missile.prefab", "Assets/Art/VFX/VFX_Arcane_Missile.fbx", new Color(0.7f, 0.35f, 1f), 0.14f, 0.3f);
            var shard = ModelOnlyPrefab("Assets/Prefabs/Skills/SKL_Ice_Shard.prefab", "Assets/Art/VFX/VFX_Ice_Shard.fbx");
            var fireRing = FireRingPrefab("Assets/Prefabs/Skills/SKL_Fire_Ring.prefab");
            var lightning = LightningPrefab("Assets/Prefabs/Skills/SKL_Lightning.prefab");
            var powerUp = PowerUpPrefab("Assets/Prefabs/Pickups/PWR_Crystal.prefab");

            var hero = CharacterPrefab("Assets/Prefabs/Player/CHR_Hero_Main.prefab", "Assets/Art/Characters/Hero/CHR_Hero_Main.fbx", heroController, 1.35f, true, null, null);
            ConfigureHero(hero, bolt, shard, missile, fireRing, lightning);
            var normal = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Normal.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx", normalController, 0.9f, false, normalDef, xpPrefab);
            var fast = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Fast.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx", fastController, 0.7f, false, fastDef, xpPrefab);
            var tank = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Tank.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx", tankController, 1.15f, false, tankDef, xpPrefab);
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

            BuildBoot();
            BuildMenu(hero, heroController, env);
            BuildArena(balance, upgrades, skills, hero, normal, fast, tank, boss, env, vfx, powerUp);
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
                else if (name.Contains("ice") || name.Contains("arcane") || name.Contains("fire") || name.Contains("powerup") || name.Contains("rune") || name.Contains("xp") || name.Contains("ring"))
                {
                    mat.SetFloat("_Smoothness", 0.85f);
                    Emissive(1.3f);
                }
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
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            additiveParticleMaterial = AdditiveMaterial("Assets/Art/VFX/Materials/MAT_Particle_Additive.mat", shader, true);
            trailMaterial = AdditiveMaterial("Assets/Art/VFX/Materials/MAT_Trail_Additive.mat", shader, false);
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
            foreach (var path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null)
                    continue;
                bool mobile = path.Contains("Mobile");
                asset.renderScale = 1f;
                asset.msaaSampleCount = 2;
                asset.supportsHDR = false;
                asset.shadowDistance = mobile ? 28f : 34f;
                asset.shadowCascadeCount = 1;
                asset.mainLightShadowmapResolution = mobile ? 1024 : 2048;
                asset.supportsCameraDepthTexture = false;
                asset.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(asset);
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

        static EnemyDefinition Enemy(string path, string displayName, int health, float speed, int damage, float range, float cooldown, int xp, bool boss)
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

        static SpawnPhase Phase(float until, float interval, int normal, int fast, int tank)
        {
            return new SpawnPhase { untilSeconds = until, interval = interval, normal = normal, fast = fast, tank = tank };
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

        static GameObject FireRingPrefab(string path)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel("Assets/Art/VFX/VFX_Fire_Ring.fbx"));
            instance.name = Path.GetFileNameWithoutExtension(path);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            var pulse = instance.AddComponent<RingPulse>();
            pulse.startRadius = 0.3f;
            pulse.targetRadius = 2.6f;
            pulse.duration = 0.45f;
            return SavePrefab(instance, path);
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
            crystal.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            beam.transform.localScale = new Vector3(0.45f, 1.6f, 0.45f);
            var beamRenderer = beam.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = TranslucentMaterial("Assets/Art/VFX/Materials/MAT_PowerUp_Beam.mat", new Color(1f, 1f, 1f, 0.22f));
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
                root.AddComponent<PlayerController>();
                root.AddComponent<DashController>();
                root.AddComponent<AutoAttackController>();
                root.AddComponent<SkillController>();
            }
            else
            {
                var health = root.AddComponent<EnemyHealth>();
                health.definition = definition;
                health.xpOrbPrefab = xp;
                root.AddComponent<EnemyChaseAI>();
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
            box.size = new Vector3(80f, 1f, 80f);
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
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        }

        static void BuildMenu(GameObject hero, RuntimeAnimatorController heroController, Dictionary<string, GameObject> env)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = MakeCamera("Camera", new Color(0.09f, 0.12f, 0.2f));
            camera.fieldOfView = 34f;
            camera.transform.position = new Vector3(1.1f, 1.25f, -4.1f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(1.55f, 0.78f, 0f) - camera.transform.position, Vector3.up);
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            var sun = Light();
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
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
            showcase.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
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
            var subtitle = Text(safe, "Subtitle", "Penyihir vs gerombolan slime", 30, TextAnchor.MiddleLeft, font);
            subtitle.color = new Color(0.8f, 0.85f, 1f);
            Place(subtitle.gameObject, safe, new Vector2(0.3f, 0.715f), new Vector2(760f, 50f));
            var gold = Text(safe, "Gold", "Emas 0", 36, TextAnchor.MiddleLeft, font);
            Place(gold.gameObject, safe, new Vector2(0.3f, 0.62f), new Vector2(500f, 60f));
            var best = Text(safe, "Best", "Rekor 00:00", 28, TextAnchor.MiddleLeft, font);
            Place(best.gameObject, safe, new Vector2(0.3f, 0.565f), new Vector2(500f, 50f));
            var play = Button(safe, "Play", "Main", font, Color.white, goldSprite);
            play.GetComponentInChildren<Text>().fontSize = 40;
            Place(play.gameObject, safe, new Vector2(0.3f, 0.43f), new Vector2(440f, 116f));
            var upgradesLabel = Text(safe, "UpgradesLabel", "Peningkatan permanen", 26, TextAnchor.MiddleLeft, font);
            Place(upgradesLabel.gameObject, safe, new Vector2(0.3f, 0.3f), new Vector2(760f, 40f));
            var damage = Button(safe, "Damage", "Damage", font, Color.white, buttonSprite);
            Place(damage.gameObject, safe, new Vector2(0.155f, 0.19f), new Vector2(300f, 118f));
            var health = Button(safe, "Health", "Nyawa", font, Color.white, buttonSprite);
            Place(health.gameObject, safe, new Vector2(0.325f, 0.19f), new Vector2(300f, 118f));
            var speed = Button(safe, "Speed", "Laju", font, Color.white, buttonSprite);
            Place(speed.gameObject, safe, new Vector2(0.495f, 0.19f), new Vector2(300f, 118f));
            menu.goldText = gold;
            menu.bestText = best;
            menu.damageButton = damage;
            menu.healthButton = health;
            menu.speedButton = speed;
            menu.damageLabel = damage.GetComponentInChildren<Text>();
            menu.healthLabel = health.GetComponentInChildren<Text>();
            menu.speedLabel = speed.GetComponentInChildren<Text>();
            UnityEventTools.AddPersistentListener(play.onClick, menu.Play);
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
            GameObject boss,
            Dictionary<string, GameObject> env,
            List<VfxPlayer.Entry> vfxEntries,
            GameObject powerUpPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var groundInstance = (GameObject)PrefabUtility.InstantiatePrefab(env["ground"]);
            groundInstance.transform.position = Vector3.zero;
            var rune = (GameObject)PrefabUtility.InstantiatePrefab(env["RuneRing"]);
            rune.transform.position = new Vector3(0f, 0.012f, 0f);
            MarkStatic(rune);

            var rng = new System.Random(2024);
            Ring(env["BoundaryStone"], 18, 12.0f, true, rng);
            Ring(env["Tree_A"], 8, 14.6f, false, rng);
            Ring(env["Tree_B"], 7, 15.9f, false, rng, 0.5f);
            Ring(env["Tree_A"], 10, 19.5f, false, rng, 0.25f, 1.2f);
            Ring(env["Tree_B"], 9, 23f, false, rng, 0.1f, 1.35f);
            Ring(env["Rock_A"], 8, 12.9f, false, rng, 0.2f);
            Ring(env["Rock_B"], 5, 9.6f, false, rng, 0.4f);
            Ring(env["RuinPillar"], 4, 13.4f, true, rng);
            Ring(env["Mushroom"], 7, 6.8f, false, rng, 0.3f);
            Ring(env["Bush"], 8, 4.8f, false, rng, 0.35f);
            Ring(env["Bush"], 10, 13.8f, false, rng, 0.3f);
            Scatter(env["Grass"], 54, 2.2f, 12.4f, rng, Vector3.zero);
            Scatter(env["Flower"], 20, 2.6f, 11.2f, rng, Vector3.zero);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(hero);
            player.transform.position = Vector3.zero;
            var camera = MakeCamera("Camera", new Color(0.36f, 0.56f, 0.7f));
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 70f;
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
            RenderSettings.fogEndDistance = 60f;
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
            flow.balance = balance;
            flow.run = run;
            flow.upgrades = upgradeManager;
            flow.audioService = audio;
            run.balance = balance;
            run.spawner = spawner;
            spawner.balance = balance;
            spawner.run = run;
            spawner.normalPrefab = normal;
            spawner.fastPrefab = fast;
            spawner.tankPrefab = tank;
            spawner.bossPrefab = boss;
            upgradeManager.pool = upgrades;
            powerUps.skills = skills;
            powerUps.pickupPrefab = powerUpPrefab;
            powerUps.everyLevels = 5;
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
                Sfx("defeat", "SFX_Defeat", 1f, 0.5f, 0f)
            };
            vfx.entries = vfxEntries.ToArray();

            var ui = BuildHud(player.GetComponent<DashController>(), flow);
            flow.ui = ui;
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
            var xp = Slider(safe, "XP", new Color(0.45f, 0.92f, 0.45f), barSprite);
            Place(xp.gameObject, safe, new Vector2(0.16f, 0.883f), new Vector2(470f, 22f));
            var level = Text(safe, "Level", "Lv 1", 34, TextAnchor.MiddleLeft, font);
            level.fontStyle = FontStyle.Bold;
            Place(level.gameObject, safe, new Vector2(0.335f, 0.935f), new Vector2(160f, 44f));
            var timer = Text(safe, "Timer", "00:00", 40, TextAnchor.MiddleCenter, font);
            Place(timer.gameObject, safe, new Vector2(0.5f, 0.935f), new Vector2(240f, 54f));
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
            Place(resume.gameObject, pausePanel.transform, new Vector2(0.5f, 0.48f), new Vector2(380f, 96f));
            var menu = Button(pausePanel.transform, "Menu", "Menu", font, Color.white, dangerSprite);
            Place(menu.gameObject, pausePanel.transform, new Vector2(0.5f, 0.3f), new Vector2(380f, 96f));
            pausePanel.SetActive(false);

            var resultPanel = Panel(safe, "Result", font, "Hasil");
            var resultTitle = resultPanel.GetComponentInChildren<Text>();
            var resultBody = Text(resultPanel.transform, "Body", "", 34, TextAnchor.MiddleCenter, font);
            Place(resultBody.gameObject, resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(700f, 160f));
            var retry = Button(resultPanel.transform, "Retry", "Ulangi", font, Color.white, goldSprite);
            Place(retry.gameObject, resultPanel.transform, new Vector2(0.36f, 0.24f), new Vector2(300f, 96f));
            var exit = Button(resultPanel.transform, "Exit", "Menu", font, Color.white, dangerSprite);
            Place(exit.gameObject, resultPanel.transform, new Vector2(0.64f, 0.24f), new Vector2(300f, 96f));
            resultPanel.SetActive(false);

            var player = dash.GetComponent<PlayerController>();
            player.joystick = joystick;
            ui.hpSlider = hp;
            ui.hpText = hpText;
            ui.xpSlider = xp;
            ui.levelText = level;
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
            ui.skillSlots = slots;
            ui.skillIcons = slotIcons;
            ui.skillLevels = slotLevels;
            ui.skillFrames = slotFrames;
            ui.toastGroup = toastGroup;
            ui.toastText = toastText;
            ui.toastBackground = toastImage;
            UnityEventTools.AddPersistentListener(pause.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(resume.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(menu.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(retry.onClick, flow.Restart);
            UnityEventTools.AddPersistentListener(exit.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(dashButton.onClick, dash.QueueDash);
            canvas.sortingOrder = 0;
            return ui;
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
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.Android.bundleVersionCode = 2;
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
