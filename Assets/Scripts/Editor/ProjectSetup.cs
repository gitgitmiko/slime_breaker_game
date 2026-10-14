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
        static Sprite panelSprite;
        static Sprite buttonSprite;
        static Sprite dangerSprite;
        static Sprite whiteSprite;

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
            Directory.CreateDirectory("Assets/Prefabs/Player");
            Directory.CreateDirectory("Assets/Prefabs/Enemies");
            Directory.CreateDirectory("Assets/Prefabs/Bosses");
            Directory.CreateDirectory("Assets/Prefabs/Environment");
            Directory.CreateDirectory("Assets/Prefabs/Pickups");
            Directory.CreateDirectory("Assets/Prefabs/VFX");
            Directory.CreateDirectory("Assets/Prefabs/UI");
            Directory.CreateDirectory("Assets/Animations");
            Directory.CreateDirectory("Assets/ScriptableObjects/Enemies");
            Directory.CreateDirectory("Assets/ScriptableObjects/Upgrades");
            Directory.CreateDirectory("Assets/ScriptableObjects/Balance");
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            ConfigureSprites();
            LoadUiArt();
            var models = AssetDatabase.FindAssets("t:Model", new[] { ArtRoot });
            foreach (var guid in models)
                ConfigureModel(AssetDatabase.GUIDToAssetPath(guid));
            ConvertMaterials();

            var heroClips = LoadClips("Assets/Art/Characters/Hero/CHR_Hero_Main.fbx");
            var normalClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx");
            var fastClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx");
            var tankClips = LoadClips("Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx");
            var bossClips = LoadClips("Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx");

            var heroController = BuildController("Assets/Animations/CHR_Hero_Main.controller", heroClips, "Run", true, false);
            var normalController = BuildController("Assets/Animations/ENM_Slime_Normal.controller", normalClips, "Move", false, false);
            var fastController = BuildController("Assets/Animations/ENM_Slime_Fast.controller", fastClips, "Move", false, false);
            var tankController = BuildController("Assets/Animations/ENM_Slime_Tank.controller", tankClips, "Move", false, false);
            var bossController = BuildController("Assets/Animations/BOSS_SlimeKing.controller", bossClips, "Move", true, true);

            var hitVfx = Burst("Assets/Prefabs/VFX/VFX_Hit_Small.prefab", new Color(1f, 0.86f, 0.35f), 16, 3.5f, 0.18f);
            var dashVfx = Burst("Assets/Prefabs/VFX/VFX_Dash_Trail.prefab", new Color(0.3f, 0.9f, 1f), 20, 2.2f, 0.16f);
            var levelVfx = Burst("Assets/Prefabs/VFX/VFX_LevelUp.prefab", new Color(0.95f, 0.82f, 0.3f), 28, 4f, 0.22f);
            var slamVfx = Burst("Assets/Prefabs/VFX/VFX_Boss_Impact.prefab", new Color(0.95f, 0.4f, 0.18f), 36, 5f, 0.28f);

            var normalDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Normal.asset", "Slime", 24, 2.35f, 8, 1.15f, 0.75f, 3, false);
            var fastDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Fast.asset", "Slime Cepat", 14, 3.9f, 6, 1.0f, 0.6f, 4, false);
            var tankDef = Enemy("Assets/ScriptableObjects/Enemies/ENM_Slime_Tank.asset", "Slime Tank", 64, 1.35f, 14, 1.4f, 0.9f, 8, false);
            var bossDef = Enemy("Assets/ScriptableObjects/Enemies/BOSS_SlimeKing.asset", "Slime King", 460, 2.05f, 16, 1.7f, 0.8f, 0, true);

            var upgrades = new[]
            {
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Damage.asset", UpgradeKind.Damage, "Serangan", "+20% damage", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Damage.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Speed.asset", UpgradeKind.Speed, "Laju", "+12% kecepatan", 0.12f, "Assets/Art/UI/Icons/ICON_Upgrade_Speed.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Health.asset", UpgradeKind.Health, "Nyawa", "+25 nyawa", 25f, "Assets/Art/UI/Icons/ICON_Upgrade_Health.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_AttackRate.asset", UpgradeKind.AttackRate, "Ritme", "Serangan lebih cepat", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_AttackRate.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Dash.asset", UpgradeKind.Dash, "Dash", "Dash lebih sering", 0.15f, "Assets/Art/UI/Icons/ICON_Upgrade_Dash.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Range.asset", UpgradeKind.Range, "Jangkauan", "+20% jangkauan", 0.2f, "Assets/Art/UI/Icons/ICON_Upgrade_Range.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Xp.asset", UpgradeKind.Xp, "Pengalaman", "+25% XP", 0.25f, "Assets/Art/UI/Icons/ICON_Upgrade_Xp.png"),
                Upgrade("Assets/ScriptableObjects/Upgrades/ICON_Upgrade_Armor.asset", UpgradeKind.Armor, "Kulit", "Damage masuk -8%", 0.08f, "Assets/Art/UI/Icons/ICON_Upgrade_Armor.png")
            };

            var balance = Scriptable("Assets/ScriptableObjects/Balance/Balance.asset", ScriptableObject.CreateInstance<BalanceConfig>());
            balance.phases = new[]
            {
                Phase(45f, 2.3f, 1, 0, 0),
                Phase(120f, 1.7f, 3, 2, 0),
                Phase(210f, 1.35f, 3, 2, 1),
                Phase(300f, 1.0f, 2, 3, 2)
            };
            EditorUtility.SetDirty(balance);

            var xpPrefab = ModelPrefab("Assets/Prefabs/Pickups/VFX_XP_Orb.prefab", "Assets/Art/VFX/VFX_XP_Orb.fbx", null, 0.45f);
            var xp = xpPrefab.GetComponent<XPOrb>();
            if (xp == null)
            {
                var temp = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Pickups/VFX_XP_Orb.prefab");
                temp.AddComponent<XPOrb>();
                PrefabUtility.SaveAsPrefabAsset(temp, "Assets/Prefabs/Pickups/VFX_XP_Orb.prefab");
                PrefabUtility.UnloadPrefabContents(temp);
                xpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Pickups/VFX_XP_Orb.prefab");
            }

            var hero = CharacterPrefab("Assets/Prefabs/Player/CHR_Hero_Main.prefab", "Assets/Art/Characters/Hero/CHR_Hero_Main.fbx", heroController, 1.25f, true, null, null);
            var normal = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Normal.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Normal.fbx", normalController, 0.9f, false, normalDef, xpPrefab);
            var fast = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Fast.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Fast.fbx", fastController, 0.7f, false, fastDef, xpPrefab);
            var tank = CharacterPrefab("Assets/Prefabs/Enemies/ENM_Slime_Tank.prefab", "Assets/Art/Enemies/Slime/ENM_Slime_Tank.fbx", tankController, 1.15f, false, tankDef, xpPrefab);
            var boss = BossPrefab("Assets/Prefabs/Bosses/BOSS_SlimeKing.prefab", bossController, bossDef);

            var ground = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Ground.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Ground.fbx", 0.3f, true);
            var treeA = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Tree_A.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Tree_A.fbx", 2.2f, false);
            var treeB = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Tree_B.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Tree_B.fbx", 2.4f, false);
            var rockA = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Rock_A.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Rock_A.fbx", 0.7f, false);
            var rockB = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Rock_B.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Rock_B.fbx", 0.6f, false);
            var mushroom = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Mushroom.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Mushroom.fbx", 0.5f, false);
            var bush = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_Bush.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_Bush.fbx", 0.5f, false);
            var pillar = StaticPrefab("Assets/Prefabs/Environment/ENV_Forest_RuinPillar.prefab", "Assets/Art/Environment/ForestArena/ENV_Forest_RuinPillar.fbx", 1.6f, false);

            BuildBoot();
            BuildMenu();
            BuildArena(balance, upgrades, hero, normal, fast, tank, boss, ground, treeA, treeB, rockA, rockB, mushroom, bush, pillar, hitVfx, dashVfx, levelVfx, slamVfx);
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ConfigureSprites()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/UI"))
                return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer == null)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.spriteMeshType = SpriteMeshType.FullRect;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("UI_Panel"))
                    importer.spriteBorder = new Vector4(48f, 48f, 48f, 48f);
                else if (path.Contains("UI_Button"))
                    importer.spriteBorder = new Vector4(60f, 58f, 60f, 58f);
                importer.SaveAndReimport();
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
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static void ConvertMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Shader URP Lit tidak ditemukan.");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ArtRoot }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null)
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
                mat.SetFloat("_Smoothness", 0.28f);
                string name = mat.name.ToLowerInvariant();
                if (name.Contains("slime") || name.Contains("king") || name.Contains("gem") || name.Contains("xp") || name.Contains("ring"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 0.55f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                EditorUtility.SetDirty(mat);
            }
        }

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
                return state;
            }
            var idle = State("Idle", "Idle");
            machine.defaultState = idle;
            var move = State("Move", moveClip);
            var toMove = idle.AddTransition(move);
            toMove.hasExitTime = false;
            toMove.duration = 0.08f;
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var toIdle = move.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.08f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            Any(machine, State("Hit", "Hit"), "Hit");
            var death = State("Death", "Death");
            Any(machine, death, "Death");
            if (combat)
            {
                var attack = State("Attack", "Attack");
                Any(machine, attack, "Attack");
                var back = attack.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.05f;
                Any(machine, State("Dash", "Dash"), "Dash");
            }
            if (boss)
            {
                Any(machine, State("Telegraph", "Telegraph"), "Telegraph");
                Any(machine, State("Slam", "Slam"), "Slam");
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

        static GameObject Burst(string path, Color color, int count, float speed, float size)
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = count + 4;
            main.playOnAwake = true;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
            var prefab = SavePrefab(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

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
            return Scriptable(path, asset);
        }

        static SpawnPhase Phase(float until, float interval, int normal, int fast, int tank)
        {
            return new SpawnPhase { untilSeconds = until, interval = interval, normal = normal, fast = fast, tank = tank };
        }

        static GameObject ModelPrefab(string path, string modelPath, RuntimeAnimatorController controller, float expectedHeight)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new FileNotFoundException(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = Path.GetFileNameWithoutExtension(path);
            FitHeight(instance, expectedHeight);
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            return SavePrefab(instance, path);
        }

        static GameObject CharacterPrefab(string path, string modelPath, RuntimeAnimatorController controller, float expectedHeight, bool player, EnemyDefinition definition, GameObject xp)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual, expectedHeight);
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            if (player)
            {
                var body = root.AddComponent<CharacterController>();
                body.height = 1.25f;
                body.radius = 0.28f;
                body.center = new Vector3(0f, 0.64f, 0f);
                root.AddComponent<PlayerHealth>();
                root.AddComponent<PlayerController>();
                root.AddComponent<DashController>();
                root.AddComponent<AutoAttackController>();
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

        static GameObject BossPrefab(string path, RuntimeAnimatorController controller, EnemyDefinition definition)
        {
            var root = new GameObject("BOSS_SlimeKing");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/SlimeKing/BOSS_SlimeKing.fbx");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual, 2.1f);
            var animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var ringModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/VFX/VFX_Boss_Ring.fbx");
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(ringModel);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ring.SetActive(false);
            var health = root.AddComponent<EnemyHealth>();
            health.definition = definition;
            var boss = root.AddComponent<BossController>();
            boss.ring = ring.transform;
            return SavePrefab(root, path);
        }

        static GameObject StaticPrefab(string path, string modelPath, float expectedHeight, bool ground)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            instance.name = Path.GetFileNameWithoutExtension(path);
            FitHeight(instance, expectedHeight);
            if (ground)
            {
                var renderer = instance.GetComponentInChildren<Renderer>();
                var box = instance.AddComponent<BoxCollider>();
                if (renderer != null)
                {
                    var bounds = renderer.bounds;
                    box.center = instance.transform.InverseTransformPoint(bounds.center) + Vector3.down * 0.5f;
                    box.size = new Vector3(Mathf.Max(1f, bounds.size.x), 1f, Mathf.Max(1f, bounds.size.z));
                }
            }
            return SavePrefab(instance, path);
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

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera("Camera", new Color(0.08f, 0.16f, 0.18f));
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            new GameObject("Boot").AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        }

        static void BuildMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera("Camera", new Color(0.08f, 0.16f, 0.18f));
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            Light();
            EventSystem();
            var canvas = Canvas(out var safe);
            var menu = new GameObject("Menu").AddComponent<MainMenuController>();
            var font = Font();
            var title = Text(safe, "Title", "Slime Breaker", 72, TextAnchor.MiddleCenter, font);
            Place(title.gameObject, safe, new Vector2(0.5f, 0.78f), new Vector2(900f, 120f));
            var gold = Text(safe, "Gold", "Emas 0", 36, TextAnchor.MiddleCenter, font);
            Place(gold.gameObject, safe, new Vector2(0.5f, 0.64f), new Vector2(500f, 60f));
            var best = Text(safe, "Best", "Rekor 00:00", 28, TextAnchor.MiddleCenter, font);
            Place(best.gameObject, safe, new Vector2(0.5f, 0.57f), new Vector2(500f, 50f));
            var play = Button(safe, "Play", "Main", font, Color.white, buttonSprite);
            Place(play.gameObject, safe, new Vector2(0.5f, 0.42f), new Vector2(420f, 110f));
            var damage = Button(safe, "Damage", "Damage", font, Color.white, buttonSprite);
            Place(damage.gameObject, safe, new Vector2(0.28f, 0.22f), new Vector2(360f, 120f));
            var health = Button(safe, "Health", "Nyawa", font, Color.white, buttonSprite);
            Place(health.gameObject, safe, new Vector2(0.5f, 0.22f), new Vector2(360f, 120f));
            var speed = Button(safe, "Speed", "Laju", font, Color.white, buttonSprite);
            Place(speed.gameObject, safe, new Vector2(0.72f, 0.22f), new Vector2(360f, 120f));
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
            GameObject hero,
            GameObject normal,
            GameObject fast,
            GameObject tank,
            GameObject boss,
            GameObject ground,
            GameObject treeA,
            GameObject treeB,
            GameObject rockA,
            GameObject rockB,
            GameObject mushroom,
            GameObject bush,
            GameObject pillar,
            GameObject hit,
            GameObject dash,
            GameObject levelUp,
            GameObject slam)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var groundInstance = (GameObject)PrefabUtility.InstantiatePrefab(ground);
            groundInstance.transform.position = Vector3.zero;
            Ring(treeA, 8, 14.2f);
            Ring(treeB, 6, 15.4f);
            Ring(rockA, 8, 12.4f);
            Ring(rockB, 5, 9.5f);
            Ring(mushroom, 7, 6.5f);
            Ring(bush, 8, 4.5f);
            Ring(pillar, 4, 13.2f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(hero);
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            var camera = MakeCamera("Camera", new Color(0.38f, 0.58f, 0.68f));
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 80f;
            camera.transform.position = new Vector3(0f, 11.5f, -9.5f);
            var follow = camera.gameObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            player.GetComponent<PlayerController>().followCamera = follow;
            new GameObject("UrpBootstrap").AddComponent<UrpSceneBootstrap>();
            var sun = Light();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.48f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.18f, 0.12f);
            RenderSettings.sun = sun;
            EventSystem();

            var systems = new GameObject("Systems");
            var flow = systems.AddComponent<GameFlowController>();
            var run = systems.AddComponent<RunManager>();
            var spawner = systems.AddComponent<EnemySpawner>();
            var xp = systems.AddComponent<XPManager>();
            var upgradeManager = systems.AddComponent<UpgradeManager>();
            var audio = systems.AddComponent<AudioService>();
            var vfx = systems.AddComponent<VfxPlayer>();
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
            audio.music = Clip("Assets/Audio/Music/MUS_Arena.ogg");
            audio.hit = Clip("Assets/Audio/SFX/SFX_Hit.ogg");
            audio.playerHit = Clip("Assets/Audio/SFX/SFX_PlayerHit.ogg");
            audio.dash = Clip("Assets/Audio/SFX/SFX_Dash.ogg");
            audio.pickup = Clip("Assets/Audio/SFX/SFX_Pickup.ogg");
            audio.levelUp = Clip("Assets/Audio/SFX/SFX_LevelUp.ogg");
            audio.death = Clip("Assets/Audio/SFX/SFX_Death.ogg");
            audio.bossWarn = Clip("Assets/Audio/SFX/SFX_BossWarn.ogg");
            audio.bossSlam = Clip("Assets/Audio/SFX/SFX_BossSlam.ogg");
            audio.ui = Clip("Assets/Audio/SFX/SFX_UI.ogg");
            audio.victory = Clip("Assets/Audio/SFX/SFX_Victory.ogg");
            audio.defeat = Clip("Assets/Audio/SFX/SFX_Defeat.ogg");
            vfx.hit = hit;
            vfx.dash = dash;
            vfx.levelUp = levelUp;
            vfx.slam = slam;

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

        static UIController BuildHud(DashController dash, GameFlowController flow)
        {
            var canvas = Canvas(out var safe);
            var font = Font();
            var uiObject = new GameObject("UI");
            var ui = uiObject.AddComponent<UIController>();
            var hp = Slider(safe, "HP", new Color(0.85f, 0.28f, 0.32f));
            Place(hp.gameObject, safe, new Vector2(0.18f, 0.93f), new Vector2(420f, 28f));
            var xp = Slider(safe, "XP", new Color(0.45f, 0.9f, 0.4f));
            Place(xp.gameObject, safe, new Vector2(0.18f, 0.88f), new Vector2(420f, 16f));
            var level = Text(safe, "Level", "Lv 1", 32, TextAnchor.MiddleLeft, font);
            Place(level.gameObject, safe, new Vector2(0.34f, 0.93f), new Vector2(160f, 40f));
            var timer = Text(safe, "Timer", "00:00", 36, TextAnchor.MiddleCenter, font);
            Place(timer.gameObject, safe, new Vector2(0.5f, 0.93f), new Vector2(220f, 50f));
            var pause = Button(safe, "Pause", "II", font, Color.white, buttonSprite);
            Place(pause.gameObject, safe, new Vector2(0.94f, 0.92f), new Vector2(90f, 70f));

            var stick = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            ApplyImage(stick.GetComponent<Image>(), panelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.55f));
            stick.GetComponent<Image>().raycastTarget = true;
            var stickRect = Place(stick, safe, new Vector2(0.12f, 0.18f), new Vector2(220f, 220f));
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(stick.transform, false);
            ApplyImage(handle.GetComponent<Image>(), whiteSprite, Image.Type.Simple, new Color(1f, 1f, 1f, 0.7f));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(90f, 90f);
            handleRect.anchoredPosition = Vector2.zero;
            var joystick = stick.GetComponent<VirtualJoystick>();
            joystick.handle = handleRect;
            joystick.radius = 70f;

            var dashButton = Button(safe, "Dash", "Dash", font, Color.white, buttonSprite);
            Place(dashButton.gameObject, safe, new Vector2(0.88f, 0.16f), new Vector2(170f, 170f));
            var dashFillObject = new GameObject("DashFill", typeof(RectTransform), typeof(Image));
            dashFillObject.transform.SetParent(dashButton.transform, false);
            var dashFill = dashFillObject.GetComponent<Image>();
            ApplyImage(dashFill, whiteSprite, Image.Type.Filled, new Color(1f, 1f, 0.7f, 0.45f));
            dashFill.fillMethod = Image.FillMethod.Radial360;
            dashFill.fillAmount = 1f;
            Stretch(dashFill.rectTransform);

            var levelPanel = Panel(safe, "LevelUp", font, "Pilih upgrade");
            var choices = new Button[3];
            var labels = new Text[3];
            var icons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                choices[i] = Button(levelPanel.transform, "Choice" + i, "Upgrade", font, Color.white, buttonSprite);
                Place(choices[i].gameObject, levelPanel.transform, new Vector2(0.22f + i * 0.28f, 0.38f), new Vector2(280f, 220f));
                labels[i] = choices[i].GetComponentInChildren<Text>();
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(choices[i].transform, false);
                var iconRect = Place(iconObject, choices[i].transform, new Vector2(0.5f, 0.72f), new Vector2(72f, 72f));
                icons[i] = iconObject.GetComponent<Image>();
                ApplyImage(icons[i], whiteSprite, Image.Type.Simple, Color.white);
                iconRect.anchorMin = new Vector2(0.5f, 0.72f);
                iconRect.anchorMax = new Vector2(0.5f, 0.72f);
            }
            levelPanel.SetActive(false);

            var pausePanel = Panel(safe, "Pause", font, "Jeda");
            var resume = Button(pausePanel.transform, "Resume", "Lanjut", font, Color.white, buttonSprite);
            Place(resume.gameObject, pausePanel.transform, new Vector2(0.5f, 0.48f), new Vector2(360f, 90f));
            var menu = Button(pausePanel.transform, "Menu", "Menu", font, Color.white, dangerSprite);
            Place(menu.gameObject, pausePanel.transform, new Vector2(0.5f, 0.28f), new Vector2(360f, 90f));
            pausePanel.SetActive(false);

            var resultPanel = Panel(safe, "Result", font, "Hasil");
            var resultTitle = resultPanel.GetComponentInChildren<Text>();
            var resultBody = Text(resultPanel.transform, "Body", "", 32, TextAnchor.MiddleCenter, font);
            Place(resultBody.gameObject, resultPanel.transform, new Vector2(0.5f, 0.48f), new Vector2(700f, 140f));
            var retry = Button(resultPanel.transform, "Retry", "Ulangi", font, Color.white, buttonSprite);
            Place(retry.gameObject, resultPanel.transform, new Vector2(0.35f, 0.22f), new Vector2(280f, 90f));
            var exit = Button(resultPanel.transform, "Exit", "Menu", font, Color.white, dangerSprite);
            Place(exit.gameObject, resultPanel.transform, new Vector2(0.65f, 0.22f), new Vector2(280f, 90f));
            resultPanel.SetActive(false);

            var player = dash.GetComponent<PlayerController>();
            player.joystick = joystick;
            ui.hpSlider = hp;
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
            UnityEventTools.AddPersistentListener(pause.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(resume.onClick, flow.TogglePause);
            UnityEventTools.AddPersistentListener(menu.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(retry.onClick, flow.Restart);
            UnityEventTools.AddPersistentListener(exit.onClick, flow.QuitToMenu);
            UnityEventTools.AddPersistentListener(dashButton.onClick, dash.QueueDash);
            canvas.sortingOrder = 0;
            return ui;
        }

        static void Ring(GameObject prefab, int count, float radius)
        {
            if (prefab == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / count;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                instance.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Gitgitmiko";
            PlayerSettings.productName = "Slime Breaker";
            PlayerSettings.bundleVersion = "0.1.0";
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
            return camera;
        }

        static Light Light()
        {
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
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
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            ApplyImage(panel.GetComponent<Image>(), panelSprite, Image.Type.Sliced, Color.white);
            Stretch(panel.GetComponent<RectTransform>());
            var label = Text(panel.transform, "Title", title, 48, TextAnchor.MiddleCenter, font);
            Place(label.gameObject, panel.transform, new Vector2(0.5f, 0.78f), new Vector2(800f, 80f));
            return panel;
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
            ApplyImage(go.GetComponent<Image>(), sprite != null ? sprite : buttonSprite, Image.Type.Sliced, color);
            var button = go.GetComponent<Button>();
            var text = Text(go.transform, "Label", label, 28, TextAnchor.MiddleCenter, font);
            Stretch(text.rectTransform);
            return button;
        }

        static Slider Slider(Transform parent, string name, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            ApplyImage(background.GetComponent<Image>(), whiteSprite, Image.Type.Simple, new Color(0f, 0f, 0f, 0.45f));
            Stretch(background.GetComponent<RectTransform>());
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);
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

        static void LoadUiArt()
        {
            whiteSprite = EnsureWhiteSprite();
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/UI_Panel.png") ?? whiteSprite;
            buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/UI_Button.png") ?? whiteSprite;
            dangerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Panels/UI_Button_Danger.png") ?? buttonSprite;
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
                importer.spriteMeshType = SpriteMeshType.FullRect;
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
