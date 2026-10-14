using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SlimeBreaker.Tests
{
    public class ArenaPlaytest
    {
        const float FeetTolerance = 0.12f;

        Keyboard keyboard;
        InputSettings previousSettings;
        float dashTimer = 0.3f;
        readonly List<MeshCollider> groundColliders = new List<MeshCollider>();

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            SceneManager.LoadScene("GameArena");
            yield return null;
            yield return null;
            previousSettings = InputSystem.settings;
            var settings = Object.Instantiate(previousSettings);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            CollectGroundVertices();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            if (previousSettings != null)
                InputSystem.settings = previousSettings;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroAndEnemiesStandOnVisibleGround()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "GameArena has no PlayerController");
            Assert.IsNotEmpty(groundColliders, "GameArena has no ground mesh");
            Assert.Greater(GroundSurface(Vector3.zero), -5f, "ground mesh does not lie under the arena centre");
            Assert.Greater(GroundSurface(new Vector3(26f, 0f, 26f)), -5f, "ground mesh ends inside the camera view");

            yield return Hold(0.5f);
            Capture("01_spawn");
            AssertOnGround(player.gameObject, "hero at spawn");

            var route = new[] { Key.W, Key.D, Key.S, Key.A, Key.W };
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (var key in route)
            {
                var before = player.transform.position;
                float t = 0f;
                Press(key);
                while (t < 1.2f)
                {
                    yield return null;
                    SkipLevelUp();
                    t += Time.deltaTime;
                    lowest = Mathf.Min(lowest, player.transform.position.y);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    if (key == Key.D && t > 0.5f && t < 0.6f)
                        Tap(Key.Space);
                }
                AssertOnGround(player.gameObject, "hero after moving " + key);
                Assert.Greater(Vector3.Distance(before, player.transform.position), 1f, "hero did not move with " + key);
            }
            Release();
            Debug.Log("PLAYTEST hero y range " + lowest.ToString("F3") + " .. " + highest.ToString("F3"));
            Assert.Greater(lowest, -0.05f, "hero dropped below the floor while moving");
            Assert.Less(highest, 0.3f, "hero floated above the floor while moving");
            Capture("02_after_moving");

            float wait = 0f;
            while (EnemyHealth.Active.Count < 3 && wait < 15f)
            {
                yield return null;
                SkipLevelUp();
                wait += Time.unscaledDeltaTime;
            }
            Assert.GreaterOrEqual(EnemyHealth.Active.Count, 1, "no enemies spawned");
            yield return Hold(1.5f);
            Capture("03_enemies");
            foreach (var enemy in EnemyHealth.Active.ToArray())
            {
                if (enemy != null && !enemy.IsDead)
                    AssertOnGround(enemy.gameObject, "enemy " + enemy.name);
            }
        }

        [UnityTest]
        public IEnumerator RunProgressesWithoutErrors()
        {
            var flow = GameFlowController.Instance;
            Assert.IsNotNull(flow);
            int startLevel = flow.Stats.level;
            float t = 0f;
            int kills = 0;
            var tracked = new HashSet<EnemyHealth>();
            var player = PlayerHealth.Instance;
            float nextLog = 0f;
            while (t < 60f && flow.State != GameState.Defeat)
            {
                if (flow.State == GameState.LevelUp)
                    flow.ChooseUpgrade(0);
                foreach (var enemy in EnemyHealth.Active)
                    tracked.Add(enemy);
                Steer(player.transform.position);
                if (t >= nextLog)
                {
                    Debug.Log("PLAYTEST t=" + t.ToString("F0") + " hp=" + player.Current + "/" + player.Max + " level=" + flow.Stats.level + " alive=" + EnemyHealth.Active.Count);
                    nextLog += 5f;
                }
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Release();
            foreach (var enemy in tracked)
            {
                if (enemy == null || enemy.IsDead)
                    kills++;
            }
            Debug.Log("PLAYTEST run state=" + flow.State + " level=" + flow.Stats.level + " kills=" + kills + " alive=" + EnemyHealth.Active.Count);
            Capture("04_run");
            Assert.Greater(kills, 0, "auto attack never killed an enemy");
            Assert.AreNotEqual(GameState.Defeat, flow.State, "a kiting player died in the first minute");
            Assert.Greater(flow.Stats.level, startLevel, "the hero never levelled up");
        }

        [UnityTest]
        public IEnumerator PropsStandUpright()
        {
            yield return null;
            foreach (var name in new[] { "ENV_Forest_Tree_A", "ENV_Forest_Tree_B", "ENV_Forest_RuinPillar", "ENV_Forest_Mushroom" })
            {
                int found = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.name != name || t.parent != null)
                        continue;
                    found++;
                    var bounds = t.GetComponentInChildren<Renderer>().bounds;
                    Debug.Log("PLAYTEST prop " + name + " size=" + bounds.size.ToString("F2") + " bottom=" + bounds.min.y.ToString("F2"));
                    Assert.Greater(Vector3.Dot(t.forward, Vector3.up), 0.99f, name + " is lying on its side");
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.2f), name + " is not resting on the ground");
                }
                Assert.Greater(found, 0, name + " missing from GameArena");
            }
            Capture("05_props");
        }

        [UnityTest]
        public IEnumerator MenuPlayButtonOpensArena()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Capture("00_menu");
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(menu, "MainMenu has no MainMenuController");
            Button play = null;
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "Play")
                    play = button;
            }
            Assert.IsNotNull(play, "MainMenu has no Play button");
            play.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("GameArena", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>());
        }

        [UnityTest]
        public IEnumerator TouchJoystickMovesHero()
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return null;
                var player = Object.FindAnyObjectByType<PlayerController>();
                Assert.IsNotNull(player.joystick, "hero has no joystick");
                var rect = (RectTransform)player.joystick.transform;
                var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(rect.rect.center));
                Debug.Log("PLAYTEST joystick centre " + centre + " screen " + Screen.width + "x" + Screen.height);
                var start = player.transform.position;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = centre });
                yield return null;
                float t = 0f;
                while (t < 1f)
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = centre + new Vector2(60f, 0f) });
                    yield return null;
                    SkipLevelUp();
                    t += Time.deltaTime;
                }
                Debug.Log("PLAYTEST joystick direction " + player.joystick.Direction);
                Capture("06_hud_touch");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = centre + new Vector2(60f, 0f) });
                yield return null;
                var moved = player.transform.position - start;
                Debug.Log("PLAYTEST touch moved " + moved.ToString("F2"));
                Assert.Greater(moved.x, 1.5f, "dragging the joystick right did not move the hero right");
                Assert.AreEqual(Vector2.zero, player.joystick.Direction, "joystick did not reset on release");
            }
            finally
            {
                InputSystem.RemoveDevice(touch);
            }
        }

        [UnityTest]
        public IEnumerator AudioAndFrameRateAreConfigured()
        {
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>(), "GameArena has no AudioListener, so nothing can be heard");
            Assert.AreEqual(60, Application.targetFrameRate, "target frame rate is not 60");
            var audio = AudioService.Instance;
            Assert.IsNotNull(audio, "GameArena has no AudioService");
            foreach (var id in new[] { "hit", "cast", "bolthit", "enemydeath", "playerhit", "dash", "pickup", "levelup", "powerup", "powerupspawn", "freeze", "lightning", "fire", "arcane", "bosswarn", "slam", "ui" })
                Assert.IsTrue(audio.Has(id), "AudioService has no clip for '" + id + "'");
            Assert.IsNotNull(audio.music, "GameArena has no music clip");
        }

        [UnityTest]
        public IEnumerator PowerUpSpawnsEveryFiveLevelsAndGrantsASkill()
        {
            var flow = GameFlowController.Instance;
            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            Assert.IsNotNull(spawner, "GameArena has no PowerUpSpawner");
            Assert.IsNotNull(spawner.pickupPrefab, "PowerUpSpawner has no pickup prefab");
            Assert.GreaterOrEqual(spawner.skills.Length, 4, "PowerUpSpawner knows fewer than four skills");
            var hero = PlayerHealth.Instance;
            Assert.IsNotNull(SkillController.Instance, "hero has no SkillController");
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up exists before level 5");

            while (flow.Stats.level < 4)
            {
                flow.BeginLevelUp();
                flow.ChooseUpgrade(0);
            }
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up spawned before level 5");
            flow.BeginLevelUp();
            flow.ChooseUpgrade(0);
            yield return null;
            yield return null;
            Assert.AreEqual(5, flow.Stats.level);
            Assert.AreEqual(1, PowerUpPickup.Active.Count, "exactly one power-up should spawn at level 5");
            var pickup = PowerUpPickup.Active[0];
            Assert.IsNotNull(pickup.skill, "power-up carries no skill");
            Vector3 flat = pickup.transform.position - hero.transform.position;
            flat.y = 0f;
            Assert.Greater(flat.magnitude, 2f, "power-up spawned on top of the hero");
            Assert.Less(new Vector2(pickup.transform.position.x, pickup.transform.position.z).magnitude, 11.2f, "power-up spawned outside the arena");
            yield return Hold(0.4f);
            Capture("07_powerup");

            pickup.Collect();
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "power-up was not consumed");
            Assert.AreEqual(1, SkillController.Instance.Count, "collecting the power-up did not grant a skill");

            // Levels 6..9 must not spawn more; level 10 spawns the second tier.
            while (flow.Stats.level < 9)
            {
                flow.BeginLevelUp();
                flow.ChooseUpgrade(0);
            }
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up spawned between tiers");
            flow.BeginLevelUp();
            flow.ChooseUpgrade(0);
            yield return null;
            Assert.AreEqual(1, PowerUpPickup.Active.Count, "no power-up at level 10");
        }

        [UnityTest]
        public IEnumerator IceOrbitDamagesAndSlowsEnemies()
        {
            var controller = SkillController.Instance;
            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            Assert.IsNotNull(controller);
            SkillDefinition ice = null;
            foreach (var skill in spawner.skills)
            {
                if (skill != null && skill.kind == SkillKind.IceOrbit)
                    ice = skill;
            }
            Assert.IsNotNull(ice, "no IceOrbit skill definition");
            Assert.AreEqual(1, controller.Grant(ice));
            Assert.AreEqual(2, controller.Grant(ice), "granting twice should raise the level");
            var active = controller.Find(SkillKind.IceOrbit);
            Assert.IsNotNull(active);
            yield return null;
            Assert.AreEqual(ice.CountAt(2), active.shards.Count, "shard count does not follow the skill level");

            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var hero = PlayerHealth.Instance.transform;
            var enemyObject = Object.Instantiate(enemySpawner.tankPrefab, hero.position + hero.forward * ice.RadiusAt(2), Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            int start = enemy.Current;
            bool slowed = false;
            float t = 0f;
            while (t < 3f && !(enemy.Current < start && slowed))
            {
                enemyObject.transform.position = hero.position + hero.forward * ice.RadiusAt(2);
                yield return null;
                SkipLevelUp();
                if (enemy != null && !enemy.IsDead && enemy.IsSlowed)
                    slowed = true;
                t += Time.deltaTime;
            }
            Capture("08_ice_orbit");
            Assert.IsTrue(enemy == null || enemy.IsDead || enemy.Current < start, "ice shards never damaged the enemy");
            Assert.IsTrue(slowed, "ice shards never slowed the enemy");
            if (enemy != null && !enemy.IsDead)
                Assert.Less(enemy.SpeedMultiplier, 1f, "slowed enemy still moves at full speed");
        }

        [UnityTest]
        public IEnumerator HeroBoltHitsEnemies()
        {
            var attack = Object.FindAnyObjectByType<AutoAttackController>();
            Assert.IsNotNull(attack);
            Assert.IsNotNull(attack.boltPrefab, "hero has no bolt prefab");
            Assert.IsNotNull(attack.StaffTip, "hero model has no StaffTip bone");
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var hero = PlayerHealth.Instance.transform;
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, hero.position + Vector3.forward * 3f, Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            int start = enemy.Current;
            float t = 0f;
            while (t < 3f && enemy != null && !enemy.IsDead && enemy.Current == start)
            {
                yield return null;
                SkipLevelUp();
                t += Time.deltaTime;
            }
            Assert.IsTrue(enemy == null || enemy.IsDead || enemy.Current < start, "the bolt never damaged a nearby enemy");
        }

        [UnityTest]
        public IEnumerator EveryRendererHasAWorkingMaterial()
        {
            yield return null;
            var vfx = VfxPlayer.Instance;
            Assert.IsNotNull(vfx);
            var roots = new List<GameObject> { vfx.hit, vfx.dash, vfx.levelUp, vfx.slam };
            foreach (var entry in vfx.entries)
                roots.Add(entry.prefab);
            var skills = SkillController.Instance;
            roots.Add(skills.iceShardPrefab);
            roots.Add(skills.missilePrefab);
            roots.Add(skills.fireRingPrefab);
            roots.Add(skills.lightningPrefab);
            roots.Add(Object.FindAnyObjectByType<AutoAttackController>().boltPrefab);
            roots.Add(Object.FindAnyObjectByType<PowerUpSpawner>().pickupPrefab);
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                roots.Add(renderer.gameObject);
            foreach (var root in roots)
            {
                Assert.IsNotNull(root, "VfxPlayer has an empty slot");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, renderer.name + " has no material");
                        Assert.IsTrue(material.shader != null && material.shader.isSupported && material.shader.name != "Hidden/InternalErrorShader",
                            renderer.name + " uses a broken shader " + (material.shader != null ? material.shader.name : "null"));
                    }
                }
            }
        }

        IEnumerator Hold(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                SkipLevelUp();
                t += Time.unscaledDeltaTime;
            }
        }

        static void SkipLevelUp()
        {
            var flow = GameFlowController.Instance;
            if (flow != null && flow.State == GameState.LevelUp)
                flow.ChooseUpgrade(0);
        }

        void Steer(Vector3 hero)
        {
            EnemyHealth nearest = null;
            float nearestDistance = float.MaxValue;
            Vector3 crowd = Vector3.zero;
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy == null || enemy.IsDead)
                    continue;
                Vector3 d = hero - enemy.transform.position;
                d.y = 0f;
                float m = Mathf.Max(0.3f, d.magnitude);
                if (m < nearestDistance)
                {
                    nearestDistance = m;
                    nearest = enemy;
                }
                if (m < 3f)
                    crowd += d / (m * m);
            }
            Vector3 flat = new Vector3(hero.x, 0f, hero.z);
            Vector3 wish;
            if (nearest == null)
            {
                wish = -flat;
            }
            else
            {
                Vector3 toEnemy = nearest.transform.position - hero;
                toEnemy.y = 0f;
                Vector3 tangent = Vector3.Cross(Vector3.up, toEnemy.normalized);
                if (nearestDistance < 1.6f)
                    wish = crowd.normalized * 1.5f + tangent * 0.5f;
                else if (nearestDistance > 2f)
                    wish = toEnemy.normalized + crowd * 0.5f;
                else
                    wish = tangent + crowd * 0.5f;
            }
            if (flat.magnitude > 8f)
                wish -= flat.normalized;
            if (wish.sqrMagnitude < 0.0001f)
                wish = Vector3.forward;
            if (nearest != null && nearestDistance < 1.3f)
                dashTimer -= Time.unscaledDeltaTime;
            var keys = new List<Key>();
            if (wish.z > 0.38f * wish.magnitude) keys.Add(Key.W);
            if (wish.z < -0.38f * wish.magnitude) keys.Add(Key.S);
            if (wish.x > 0.38f * wish.magnitude) keys.Add(Key.D);
            if (wish.x < -0.38f * wish.magnitude) keys.Add(Key.A);
            if (dashTimer <= 0f)
            {
                keys.Add(Key.Space);
                dashTimer = 0.3f;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
            InputSystem.Update();
        }

        void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
        }

        void Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key, Key.D));
            InputSystem.Update();
        }

        void Release()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        void CollectGroundVertices()
        {
            groundColliders.Clear();
            var ground = GameObject.Find("ENV_Forest_Ground");
            if (ground == null)
                return;
            foreach (var filter in ground.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var probe = filter.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                groundColliders.Add(probe);
            }
        }

        float GroundSurface(Vector3 at)
        {
            float best = float.MinValue;
            var origin = new Vector3(at.x, 20f, at.z);
            foreach (var probe in groundColliders)
            {
                if (probe.Raycast(new Ray(origin, Vector3.down), out var hit, 60f))
                    best = Mathf.Max(best, hit.point.y);
            }
            return best;
        }

        static float Feet(GameObject target)
        {
            float lowest = float.MaxValue;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer)
                    continue;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
                lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            }
            return lowest;
        }

        void AssertOnGround(GameObject target, string label)
        {
            float surface = GroundSurface(target.transform.position);
            float feet = Feet(target);
            Debug.Log("PLAYTEST " + label + " pos=" + target.transform.position.ToString("F3") + " feet=" + feet.ToString("F3") + " ground=" + surface.ToString("F3"));
            Assert.That(feet, Is.EqualTo(surface).Within(FeetTolerance), label + ": feet " + feet + " vs ground " + surface);
        }

        static void Capture(string name)
        {
            var camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            var rt = new RenderTexture(1280, 720, 24);
            var previous = camera.targetTexture;
            var overlays = new List<Canvas>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.1f;
                overlays.Add(canvas);
            }
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            foreach (var canvas in overlays)
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "playtest");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
