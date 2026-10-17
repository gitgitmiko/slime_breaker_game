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
                    if (t.name != name)
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
            Assert.AreEqual("UI_Button_Gold", play.GetComponent<Image>().sprite.name, "Play should be the primary button");
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "Settings")
                {
                    Assert.AreEqual("UI_Circle_Glass", button.GetComponent<Image>().sprite.name, "menu settings button is not glass");
                    Assert.IsNotNull(button.transform.Find("Icon"), "menu settings button has no gear icon");
                }
                if (button.name == "Continue" || button.name == "Damage")
                    Assert.AreEqual("UI_Button_Ghost", button.GetComponent<Image>().sprite.name, button.name + " should be a ghost button");
            }
            var showcaseAnimator = menu.showcase.GetComponentInChildren<Animator>();
            Assert.IsNotNull(showcaseAnimator, "menu hero has no Animator");
            yield return null;
            var menuState = showcaseAnimator.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(menuState.IsName("Menu"), "menu hero is not playing the Menu loop");
            var menuClips = showcaseAnimator.GetCurrentAnimatorClipInfo(0);
            Assert.Greater(menuClips.Length, 0);
            Assert.AreEqual("Menu", menuClips[0].clip.name);
            Assert.IsTrue(menuClips[0].clip.isLooping, "Menu clip should loop");
            Assert.AreEqual(4f, menuClips[0].clip.length, 0.1f, "Menu loop should be about 4 s");
            menu.OpenSettings();
            yield return null;
            Capture("25_menu_settings");
            menu.settings.Hide();
            play.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("GameArena", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>());
        }

        [UnityTest]
        public IEnumerator TouchJoystickMovesHero()
        {
            Assert.IsFalse(new SaveData().freeJoystick, "joystick must default to fixed");
            ControlSettings.FreeJoystick = false;
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return null;
                var player = Object.FindAnyObjectByType<PlayerController>();
                Assert.IsNotNull(player.joystick, "hero has no joystick");
                Assert.IsNotNull(player.joystick.background, "joystick has no base");
                var rect = (RectTransform)player.joystick.transform;
                var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var baseRect = player.joystick.background;
                Vector2 centre = RectTransformUtility.WorldToScreenPoint(cam, baseRect.position);
                Vector3 restBase = baseRect.position;
                Debug.Log("PLAYTEST joystick centre " + centre + " screen " + Screen.width + "x" + Screen.height);

                // Fixed (default): dragging from the base steers.
                var start = player.transform.position;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = centre });
                yield return null;
                Assert.IsTrue(player.joystick.Held, "fixed joystick did not grab a touch on its base");
                Assert.Less(Vector3.Distance(baseRect.position, restBase), 0.01f, "fixed joystick base moved on press");
                yield return Drag(touch, 1, centre, new Vector2(60f, 0f), 1f, began: false);
                var moved = player.transform.position - start;
                Debug.Log("PLAYTEST touch moved " + moved.ToString("F2"));
                Assert.Greater(moved.x, 1.5f, "dragging the joystick right did not move the hero right");
                Assert.AreEqual(Vector2.zero, player.joystick.Direction, "joystick did not reset on release");
                Assert.Less(Vector3.Distance(baseRect.position, restBase), 0.01f, "fixed joystick base moved");

                // Fixed: a touch elsewhere in the left half is ignored.
                Vector2 elsewhere = RectTransformUtility.WorldToScreenPoint(cam, rect.TransformPoint(new Vector3(rect.rect.xMin + rect.rect.width * 0.8f, rect.rect.yMin + rect.rect.height * 0.75f, 0f)));
                start = player.transform.position;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2, phase = UnityEngine.InputSystem.TouchPhase.Began, position = elsewhere });
                yield return null;
                Assert.Less(Vector3.Distance(baseRect.position, restBase), 0.01f, "fixed joystick base jumped to a far touch");
                Assert.IsFalse(player.joystick.Held, "fixed joystick grabbed a touch away from its base");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = elsewhere });
                yield return null;

                // Settings toggle switches to free mode.
                var flow = GameFlowController.Instance;
                Assert.IsNotNull(flow.ui.settings, "HUD has no settings panel");
                Assert.IsNotNull(flow.ui.settings.joystickButton, "settings has no joystick toggle");
                flow.ui.settings.ToggleJoystick();
                Assert.IsTrue(ControlSettings.FreeJoystick, "toggle did not switch to free");
                Assert.AreEqual("Joystick: Free", flow.ui.settings.joystickLabel.text);

                start = player.transform.position;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 3, phase = UnityEngine.InputSystem.TouchPhase.Began, position = elsewhere });
                yield return null;
                Vector2 jumped = RectTransformUtility.WorldToScreenPoint(cam, baseRect.position);
                Assert.Less(Vector2.Distance(jumped, elsewhere), 20f, "free joystick base did not follow the touch");
                Capture("06_hud_touch");
                yield return Drag(touch, 3, elsewhere, new Vector2(0f, -60f), 0.8f, began: false);
                moved = player.transform.position - start;
                Debug.Log("PLAYTEST free joystick moved " + moved.ToString("F2"));
                Assert.Greater(moved.magnitude, 1f, "dragging from a free touch point did not move the hero");
                Assert.Less(Vector3.Distance(baseRect.position, restBase), 0.01f, "free joystick base did not return to rest");

                flow.ui.settings.ToggleJoystick();
                Assert.IsFalse(ControlSettings.FreeJoystick);
                Assert.AreEqual("Joystick: Fixed", flow.ui.settings.joystickLabel.text);
            }
            finally
            {
                ControlSettings.FreeJoystick = false;
                InputSystem.RemoveDevice(touch);
            }
        }

        IEnumerator Drag(Touchscreen touch, int id, Vector2 from, Vector2 delta, float seconds, bool began = true)
        {
            if (began)
            {
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Began, position = from });
                yield return null;
            }
            float t = 0f;
            while (t < seconds)
            {
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = from + delta });
                yield return null;
                SkipLevelUp();
                t += Time.deltaTime;
            }
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = from + delta });
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongPressSkillShowsDescription()
        {
            var flow = GameFlowController.Instance;
            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            SkillDefinition lightning = null;
            foreach (var skill in spawner.skills)
            {
                if (skill.kind == SkillKind.LightningChain)
                    lightning = skill;
            }
            SkillController.Instance.Grant(lightning);
            SkillController.Instance.Grant(lightning);
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                yield return null;
                var slot = (RectTransform)flow.ui.skillSlots[0].transform;
                Assert.IsNotNull(slot.GetComponent<SkillSlotHold>(), "skill slot has no long-press handler");
                var canvas = slot.GetComponentInParent<Canvas>().rootCanvas;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(cam, slot.position);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 5, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                yield return null;
                yield return null;
                Assert.IsFalse(flow.ui.SkillInfoVisible, "a tap should not open the description yet");
                float t = 0f;
                while (t < 0.7f)
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = 5, phase = UnityEngine.InputSystem.TouchPhase.Stationary, position = point });
                    yield return null;
                    SkipLevelUp();
                    t += Time.unscaledDeltaTime;
                }
                Assert.IsTrue(flow.ui.SkillInfoVisible, "holding the skill slot did not show its description");
                StringAssert.Contains(lightning.title, flow.ui.SkillInfo);
                StringAssert.Contains(lightning.description, flow.ui.SkillInfo);
                StringAssert.Contains("Lv 2", flow.ui.SkillInfo);
                StringAssert.Contains("boss", flow.ui.SkillInfo);
                Capture("21_skill_info");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 5, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                yield return null;
                yield return null;
                Assert.IsFalse(flow.ui.SkillInfoVisible, "description did not close on release");
            }
            finally
            {
                InputSystem.RemoveDevice(touch);
            }
        }

        [UnityTest]
        public IEnumerator BossIgnoresPowerUpsUntilBossBreaker()
        {
            var flow = GameFlowController.Instance;
            var stage = Object.FindAnyObjectByType<StageManager>();
            var hero = PlayerHealth.Instance;
            stage.ForceFloor(10);
            float wait = 0f;
            while ((stage.ActiveBoss == BossKind.None || flow.State == GameState.BossIntro) && wait < 5f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.AreEqual(GameState.Playing, flow.State);
            EnemyHealth boss = null;
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy != null && enemy.IsBoss)
                    boss = enemy;
            }
            Assert.IsNotNull(boss);
            var controller = boss.GetComponent<BossController>();
            Assert.IsNotNull(controller.breakerPickupPrefab, "boss has no Boss Breaker drop prefab");
            hero.GiveInvulnerability(60f);
            var attack = hero.GetComponent<AutoAttackController>();
            attack.enabled = false;

            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            foreach (var skill in spawner.skills)
                SkillController.Instance.Grant(skill);

            // Power-ups alone: boss HP must not move.
            Assert.IsTrue(boss.ImmuneToSkills);
            int before = boss.Current;
            Assert.IsFalse(boss.TakeSkillDamage(500, false), "skill damage landed on the boss without Boss Breaker");
            float t = 0f;
            while (t < 2.5f)
            {
                PinNear(boss, hero, 1.6f);
                hero.GiveInvulnerability(60f);
                yield return null;
                t += Time.deltaTime;
            }
            Assert.AreEqual(before, boss.Current, "power-up skills damaged the boss");

            // Basic attack still lands.
            attack.enabled = true;
            t = 0f;
            while (boss.Current == before && t < 3f)
            {
                PinNear(boss, hero, 2.4f);
                hero.GiveInvulnerability(60f);
                yield return null;
                t += Time.deltaTime;
            }
            Assert.Less(boss.Current, before, "basic attack should still damage the boss");
            attack.enabled = false;

            // A summon wave can carry a golden slime that drops Boss Breaker.
            foreach (var enemy in new List<EnemyHealth>(EnemyHealth.Active))
            {
                if (enemy != null && !enemy.IsBoss)
                    enemy.Vanish();
            }
            yield return null;
            controller.breakerDropChance = 1f;
            controller.SpawnAdds(10);
            yield return null;
            var carrier = controller.BreakerCarrier;
            Assert.IsNotNull(carrier, "summon wave had no Boss Breaker carrier");
            carrier.transform.position = hero.transform.position + new Vector3(8f, 0f, 0f);
            carrier.TakeDamage(999999);
            yield return null;
            PowerUpPickup breaker = null;
            foreach (var pickup in PowerUpPickup.Active)
            {
                if (pickup != null && pickup.bossBreaker)
                    breaker = pickup;
            }
            Assert.IsNotNull(breaker, "carrier did not drop Boss Breaker");
            breaker.Collect();
            yield return null;
            Assert.IsTrue(SkillController.BossBreakerActive, "Boss Breaker did not activate");
            Assert.Greater(SkillController.Instance.BreakerLeft, 59f);
            var aura = SkillController.Instance.BreakerAura;
            Assert.IsNotNull(aura, "hero shows no Boss Breaker aura");
            Assert.AreEqual(hero.transform, aura.transform.parent, "aura should ride on the hero");
            Assert.Greater(aura.GetComponentsInChildren<ParticleSystem>().Length, 0, "aura has no particles");
            Assert.IsTrue(flow.ui.breakerText.gameObject.activeSelf, "HUD shows no Boss Breaker timer");
            StringAssert.Contains("BOSS BREAKER", flow.ui.breakerText.text);

            Assert.IsFalse(boss.ImmuneToSkills);
            before = boss.Current;
            t = 0f;
            while (boss.Current == before && t < 4f)
            {
                PinNear(boss, hero, 1.6f);
                hero.GiveInvulnerability(60f);
                yield return null;
                t += Time.deltaTime;
            }
            Assert.Less(boss.Current, before, "power-ups should hurt the boss while Boss Breaker is active");
            Capture("22_boss_breaker");

            SkillController.Instance.ShortenBossBreaker(0.2f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(SkillController.BossBreakerActive, "Boss Breaker did not expire");
            Assert.IsTrue(SkillController.Instance.BreakerAura == null, "aura stayed after Boss Breaker ended");
            Assert.IsFalse(flow.ui.breakerText.gameObject.activeSelf);
            Assert.IsTrue(boss.ImmuneToSkills);
        }

        [UnityTest]
        public IEnumerator HudUsesGlassStyle()
        {
            ControlSettings.FreeJoystick = false;
            var flow = GameFlowController.Instance;
            var ui = flow.ui;
            yield return null;
            foreach (var bar in new[] { ui.hpSlider, ui.xpSlider, ui.floorProgress })
            {
                Assert.AreEqual("UI_Bar_Track", bar.transform.Find("Background").GetComponent<Image>().sprite.name, bar.name + " track is not the boss-bar style");
                Assert.AreEqual("UI_Bar_Fill", bar.fillRect.GetComponent<Image>().sprite.name, bar.name + " fill is not the boss-bar style");
            }
            Assert.AreEqual("UI_Ring", ui.dashFill.sprite.name, "dash cooldown should be a ring");
            Assert.AreEqual("UI_Circle_Glass", ui.dashFill.GetComponentInParent<Button>().GetComponent<Image>().sprite.name);
            Assert.AreEqual("UI_Circle_Glass", ui.pauseButton.GetComponent<Image>().sprite.name);
            Assert.IsNotNull(ui.pauseButton.transform.Find("Icon"), "pause button has no icon");
            StringAssert.Contains("Lv", ui.levelText.text);
            Assert.AreEqual("1 / 100", ui.floorText.text);
            var joystick = Object.FindAnyObjectByType<PlayerController>().joystick;
            Assert.AreEqual("UI_Joystick_Base", joystick.background.GetComponent<Image>().sprite.name);

            // Settings: the active option uses the solid accent face, others the glass face.
            ui.OpenSettings();
            yield return null;
            var settings = ui.settings;
            Assert.IsTrue(settings.panel.activeSelf);
            int level = GraphicsService.Instance != null ? GraphicsService.Instance.Level : GraphicsService.High;
            var buttons = new[] { settings.lowButton, settings.mediumButton, settings.highButton };
            for (int i = 0; i < buttons.Length; i++)
                Assert.AreEqual(i == level ? settings.selectedSprite : settings.normalSprite, buttons[i].GetComponent<Image>().sprite, buttons[i].name + " highlight is wrong");
            Assert.AreEqual(settings.normalSprite, settings.joystickButton.GetComponent<Image>().sprite, "Fixed joystick should show the normal face");
            Capture("24_settings");
            settings.Hide();
            flow.TogglePause();
            yield return null;
            Capture("26_pause");
            flow.TogglePause();
            yield return null;
            Capture("27_hud_glass");
        }

        [UnityTest]
        public IEnumerator HeroHasWitchAnimationsAndEyes()
        {
            var hero = PlayerHealth.Instance;
            var animator = hero.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            var names = new HashSet<string>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                names.Add(clip.name);
            foreach (var wanted in new[] { "Idle", "Run", "Attack", "Dash", "Hit", "Death", "Cast", "Victory" })
                Assert.IsTrue(names.Contains(wanted), "hero controller has no " + wanted + " clip");
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "Attack")
                    Assert.Greater(clip.length, Object.FindAnyObjectByType<AutoAttackController>().releaseDelay, "bolt should leave during the swing");
            }

            // Painted anime eyes on the face decal.
            bool eyes = false;
            foreach (var renderer in hero.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    string materialName = material != null ? material.name.ToLowerInvariant() : string.Empty;
                    if (!materialName.Contains("eye_decal") && !materialName.Contains("hero_eye"))
                        continue;
                    var texture = material.GetTexture("_BaseMap");
                    Assert.IsNotNull(texture, "eye decal has no texture");
                    Assert.AreEqual("TEX_Hero_Eye", texture.name);
                    eyes = true;
                }
            }
            Assert.IsTrue(eyes, "hero has no eye decal material");
            Assert.IsNotNull(SkillController.FindDeep(hero.transform, "StaffTip"), "staff tip bone missing");
            Assert.IsNotNull(SkillController.FindDeep(hero.transform, "Hat"), "hat bone missing");

            yield return null;
            Assert.IsTrue(hero.Emote("Cast"), "Cast emote missing");
            float wait = 0f;
            bool casting = false;
            while (wait < 0.5f && !casting)
            {
                yield return null;
                wait += Time.deltaTime;
                casting = animator.GetCurrentAnimatorStateInfo(0).IsName("Cast") || animator.GetNextAnimatorStateInfo(0).IsName("Cast");
            }
            Assert.IsTrue(casting, "Cast emote did not play");
            Capture("28_hero_cast");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(hero.Emote("Victory"), "Victory emote missing");
            wait = 0f;
            bool cheering = false;
            while (wait < 0.5f && !cheering)
            {
                yield return null;
                wait += Time.deltaTime;
                cheering = animator.GetCurrentAnimatorStateInfo(0).IsName("Victory") || animator.GetNextAnimatorStateInfo(0).IsName("Victory");
            }
            Assert.IsTrue(cheering, "Victory emote did not play");
            yield return new WaitForSeconds(0.4f);
            Capture("29_hero_victory");
            Assert.IsFalse(hero.Emote("Nope"), "unknown emote should be ignored");
        }

        static void PinNear(EnemyHealth boss, PlayerHealth hero, float distance)
        {
            if (boss == null || boss.IsDead)
                return;
            boss.transform.position = hero.transform.position + Vector3.forward * distance;
        }

        [UnityTest]
        public IEnumerator AudioAndFrameRateAreConfigured()
        {
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<AudioListener>(), "GameArena has no AudioListener, so nothing can be heard");
            Assert.AreEqual(60, Application.targetFrameRate, "target frame rate is not 60");
            var audio = AudioService.Instance;
            Assert.IsNotNull(audio, "GameArena has no AudioService");
            foreach (var id in new[] { "hit", "cast", "bolthit", "enemydeath", "playerhit", "dash", "pickup", "levelup", "powerup", "powerupspawn", "freeze", "lightning", "fire", "arcane", "bosswarn", "slam", "ui", "portal" })
                Assert.IsTrue(audio.Has(id), "AudioService has no clip for '" + id + "'");
            Assert.IsNotNull(audio.music, "GameArena has no music clip");
            Assert.AreEqual(10, audio.biomeMusic != null ? audio.biomeMusic.Length : 0, "AudioService needs one music track per biome");
            Assert.AreEqual(10, audio.biomeAmbience != null ? audio.biomeAmbience.Length : 0, "AudioService needs one ambience bed per biome");
            for (int i = 0; i < 10; i++)
            {
                Assert.IsNotNull(audio.biomeMusic[i], "no music for biome " + (Biome)i);
                Assert.IsNotNull(audio.biomeAmbience[i], "no ambience for biome " + (Biome)i);
            }
            Assert.IsNotNull(audio.bossMusic, "AudioService has no boss track");
            Assert.IsNotNull(audio.CurrentBed, "no ambience bed is playing in the arena");

            var stage = Object.FindAnyObjectByType<StageManager>();
            stage.ForceFloor(11);
            yield return null;
            Assert.AreEqual(audio.biomeMusic[(int)stage.Theme.biome], audio.CurrentMusic, "floor music does not follow the biome");
            audio.SetBossMusic(true);
            Assert.AreEqual(audio.bossMusic, audio.CurrentMusic, "boss track did not start");
            audio.SetBossMusic(false);
            Assert.AreEqual(audio.biomeMusic[(int)stage.Theme.biome], audio.CurrentMusic, "biome track did not return after the boss");
            Debug.Log("PLAYTEST audio biome=" + stage.Theme.biome + " music=" + audio.CurrentMusic.name + " bed=" + audio.CurrentBed.name);
        }

        [UnityTest]
        public IEnumerator PowerUpSpawnsEveryTwentyLevelsAndGrantsASkill()
        {
            var flow = GameFlowController.Instance;
            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            Assert.IsNotNull(spawner, "GameArena has no PowerUpSpawner");
            Assert.IsNotNull(spawner.pickupPrefab, "PowerUpSpawner has no pickup prefab");
            Assert.GreaterOrEqual(spawner.skills.Length, 4, "PowerUpSpawner knows fewer than four skills");
            var hero = PlayerHealth.Instance;
            Assert.IsNotNull(SkillController.Instance, "hero has no SkillController");
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up exists before level 20");

            var stage = Object.FindAnyObjectByType<StageManager>();
            Assert.IsNotNull(stage, "GameArena has no StageManager");
            while (flow.Stats.level < 19)
            {
                flow.BeginLevelUp();
                flow.ChooseUpgrade(0);
            }
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up spawned before level 20");
            flow.BeginLevelUp();
            flow.ChooseUpgrade(0);
            yield return null;
            yield return null;
            Assert.AreEqual(20, flow.Stats.level);
            Assert.AreEqual(1, stage.Floor, "power-ups follow hero level, not floor");
            Assert.IsFalse(stage.Intermission, "level 20 is a power-up beat, not a boss stage");
            Assert.AreEqual(BossKind.None, stage.ActiveBoss);
            Assert.AreEqual(1, PowerUpPickup.Active.Count, "exactly one power-up should spawn at level 20");
            var pickup = PowerUpPickup.Active[0];
            Assert.IsNotNull(pickup.skill, "power-up carries no skill");
            Vector3 flat = pickup.transform.position - hero.transform.position;
            flat.y = 0f;
            Assert.Greater(flat.magnitude, 2f, "power-up spawned on top of the hero");
            Assert.Less(new Vector2(pickup.transform.position.x, pickup.transform.position.z).magnitude, RunRules.ArenaRadius, "power-up spawned outside the arena");
            yield return Hold(1.4f);
            Capture("07_powerup");

            pickup.Collect();
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "power-up was not consumed");
            Assert.AreEqual(1, SkillController.Instance.Count, "collecting the power-up did not grant a skill");

            while (flow.Stats.level < 39)
            {
                flow.BeginLevelUp();
                flow.ChooseUpgrade(0);
            }
            yield return null;
            Assert.AreEqual(0, PowerUpPickup.Active.Count, "a power-up spawned between tiers");
            flow.BeginLevelUp();
            flow.ChooseUpgrade(0);
            yield return null;
            yield return null;
            Assert.AreEqual(1, PowerUpPickup.Active.Count, "no power-up at level 40");
            Assert.AreEqual(BossKind.None, stage.ActiveBoss, "hero level 40 must not spawn a floor boss");
            Assert.AreEqual(1, stage.Floor);
        }

        [UnityTest]
        public IEnumerator SkillsReachLevelTenAndShowMax()
        {
            var controller = SkillController.Instance;
            var spawner = Object.FindAnyObjectByType<PowerUpSpawner>();
            var flow = GameFlowController.Instance;
            Assert.IsNotNull(controller);
            SkillDefinition lightning = null;
            foreach (var skill in spawner.skills)
            {
                Assert.AreEqual(10, skill.maxLevel, skill.name + " should cap at level 10");
                if (skill.kind == SkillKind.LightningChain)
                    lightning = skill;
            }
            Assert.IsNotNull(lightning);
            int level = 0;
            for (int i = 0; i < 9; i++)
                level = controller.Grant(lightning);
            Assert.AreEqual(9, level);
            yield return null;
            var slot = flow.ui.skillLevels[0];
            Assert.AreEqual("Lv 9", slot.text, "skill slot should show the level before max");
            Assert.AreEqual(10, controller.Grant(lightning));
            Assert.AreEqual(10, controller.Grant(lightning), "skill level must not pass 10");
            yield return null;
            Assert.AreEqual("MAX", slot.text, "maxed skill slot should read MAX");
            Assert.IsTrue(flow.ui.skillSlots[0].activeSelf);

            // Once every skill is maxed the spawner has nothing left to drop.
            foreach (var skill in spawner.skills)
            {
                while (controller.LevelOf(skill.kind) < skill.maxLevel)
                    controller.Grant(skill);
            }
            yield return null;
            Assert.IsNull(spawner.Spawn(), "no power-up should spawn when every skill is MAX");
            for (int i = 0; i < spawner.skills.Length; i++)
                Assert.AreEqual("MAX", flow.ui.skillLevels[i].text);
            Capture("17_skill_max");
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
        public IEnumerator HeroBoltDoesNotFireWhileMoving()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var hero = PlayerHealth.Instance.transform;
            Press(Key.W);
            yield return null;
            yield return null;
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, hero.position + Vector3.right * 3f, Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            int start = enemy.Current;
            float t = 0f;
            while (t < 2.2f)
            {
                if (enemy != null && !enemy.IsDead)
                    enemyObject.transform.position = hero.position + Vector3.right * 3f;
                yield return null;
                SkipLevelUp();
                t += Time.deltaTime;
            }
            Assert.Greater(player.WishDirection.sqrMagnitude, 0.01f, "hero was not walking during the test");
            Assert.IsNotNull(enemy);
            Assert.IsFalse(enemy.IsDead);
            Assert.AreEqual(start, enemy.Current, "basic attack fired while the hero was walking");
            Release();
            t = 0f;
            while (t < 3f && enemy != null && !enemy.IsDead && enemy.Current == start)
            {
                if (enemy != null && !enemy.IsDead)
                    enemyObject.transform.position = hero.position + Vector3.right * 3f;
                yield return null;
                SkipLevelUp();
                t += Time.deltaTime;
            }
            Assert.IsTrue(enemy == null || enemy.IsDead || enemy.Current < start,
                "basic attack did not fire after the hero stood still");
        }

        [UnityTest]
        public IEnumerator DashDoesNotQueueDuringCooldown()
        {
            var dash = Object.FindAnyObjectByType<DashController>();
            Assert.IsNotNull(dash);
            Assert.IsTrue(dash.CanDash);
            dash.QueueDash();
            yield return null;
            Assert.IsFalse(dash.CanDash, "dash did not start");
            dash.QueueDash();
            dash.QueueDash();
            float wait = 0f;
            while (!dash.CanDash && wait < 2.5f)
            {
                yield return null;
                SkipLevelUp();
                wait += Time.deltaTime;
            }
            Assert.IsTrue(dash.CanDash, "dash cooldown never ended");
            yield return null;
            yield return null;
            Assert.IsFalse(dash.IsDashing, "a second dash fired after cooldown without a new press");
            Assert.IsTrue(dash.CanDash);
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

        [UnityTest]
        public IEnumerator FireNovaDamagesNearbyEnemies()
        {
            var controller = SkillController.Instance;
            var power = Object.FindAnyObjectByType<PowerUpSpawner>();
            SkillDefinition fire = null;
            foreach (var skill in power.skills)
            {
                if (skill != null && skill.kind == SkillKind.FireNova)
                    fire = skill;
            }
            Assert.IsNotNull(fire);
            Assert.AreEqual(1, controller.Grant(fire));
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var hero = PlayerHealth.Instance.transform;
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, hero.position + Vector3.forward * 2.2f, Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            int start = enemy.Current;
            float t = 0f;
            while (t < 1.2f && enemy != null && !enemy.IsDead && enemy.Current == start)
            {
                yield return null;
                SkipLevelUp();
                t += Time.deltaTime;
            }
            Capture("09_fire_nova");
            Assert.IsTrue(enemy == null || enemy.IsDead || enemy.Current < start, "fire nova never damaged a nearby enemy");
        }

        [UnityTest]
        public IEnumerator ObstaclesBlockBolts()
        {
            var generator = Object.FindAnyObjectByType<ArenaGenerator>();
            Assert.IsNotNull(generator);
            Assert.Greater(generator.PlacedObstacles.Count, 0, "arena has no solid obstacles");
            int layer = LayerMask.NameToLayer("Obstacle");
            Assert.Greater(layer, 0, "Obstacle layer is missing");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.layer = layer;
            wall.transform.position = new Vector3(0f, 0.6f, 2f);
            wall.transform.localScale = new Vector3(4f, 1.6f, 0.5f);

            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, new Vector3(0f, 0f, 4.2f), Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            int start = enemy.Current;
            var bolt = Object.FindAnyObjectByType<AutoAttackController>().boltPrefab;
            HomingProjectile.Launch(bolt, new Vector3(0f, 0.7f, 0.2f), enemy, 20, 22f);
            yield return Hold(0.6f);
            Capture("10_obstacle");
            Assert.AreEqual(start, enemy.Current, "the bolt passed through a solid obstacle");
        }

        [UnityTest]
        public IEnumerator RunSaveRoundTripKeepsLevelAndStage()
        {
            var flow = GameFlowController.Instance;
            while (flow.Stats.level < 7)
            {
                flow.BeginLevelUp();
                flow.ChooseUpgrade(0);
            }
            yield return null;
            flow.PersistRun();
            var saved = RunSaveService.Load();
            Assert.IsNotNull(saved, "PersistRun did not write slimebreaker-run.json");
            Assert.AreEqual(flow.Stats.level, saved.level);
            Assert.GreaterOrEqual(saved.stage, 1);
            Assert.AreEqual(1, saved.floor);
            Assert.AreEqual(flow.Stats.damage, saved.stats.damage);
            RunSaveService.Clear();
            Assert.IsFalse(RunSaveService.Exists());
        }

        [UnityTest]
        public IEnumerator EnemiesHaveHealthBars()
        {
            float wait = 0f;
            while (EnemyHealth.Active.Count < 1 && wait < 12f)
            {
                yield return null;
                SkipLevelUp();
                wait += Time.unscaledDeltaTime;
            }
            Assert.Greater(EnemyHealth.Active.Count, 0, "no enemies spawned");
            foreach (var enemy in EnemyHealth.Active.ToArray())
            {
                if (enemy == null || enemy.IsDead)
                    continue;
                var bar = enemy.GetComponentInChildren<EnemyHealthBar>();
                Assert.IsNotNull(bar, enemy.name + " has no health bar");
                Assert.Greater(enemy.Max, 0);
                Assert.LessOrEqual(enemy.Current, enemy.Max);
            }
            Capture("11_healthbars");
        }

        [UnityTest]
        public IEnumerator LifestealHealsOnHit()
        {
            var flow = GameFlowController.Instance;
            var player = PlayerHealth.Instance;
            Assert.IsNotNull(flow);
            Assert.IsNotNull(player);
            flow.Stats.lifesteal = 0.5f;
            player.Restore(20);
            int start = player.Current;
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, player.transform.position + Vector3.forward * 2.5f, Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            enemy.TakeDamage(20);
            Assert.Greater(player.Current, start, "lifesteal did not heal the hero");
            Assert.LessOrEqual(player.Current, player.Max);
            float wait = 0f;
            while (player.ShownLifesteal == 0 && wait < 1f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.Greater(player.ShownLifesteal, 0, "lifesteal heal was not shown over the hero");
            DamagePopup heal = null;
            foreach (var popup in DamagePopup.Live)
            {
                if (popup != null && popup.isActiveAndEnabled && popup.CurrentKind == DamagePopup.Kind.Heal)
                    heal = popup;
            }
            Assert.IsNotNull(heal, "no green lifesteal number");
            Assert.IsTrue(heal.Text.StartsWith("+"), heal.Text);
            var color = heal.GetComponent<TextMesh>().color;
            Assert.Greater(color.g, color.r + 0.3f, "lifesteal number should be green");
            Assert.Greater(heal.transform.position.y, player.transform.position.y + 1.5f, "lifesteal number should float above the hero");
            Capture("18_lifesteal");
        }

        [UnityTest]
        public IEnumerator RangedEnemyShootsProjectile()
        {
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.IsNotNull(enemySpawner.rangedPrefab, "no ranged slime prefab");
            Assert.IsNotNull(enemySpawner.hostileBolt, "no hostile bolt prefab");
            var hero = PlayerHealth.Instance.transform;
            var enemyObject = Object.Instantiate(enemySpawner.rangedPrefab, hero.position + Vector3.forward * 5.2f, Quaternion.identity);
            var ai = enemyObject.GetComponent<EnemyChaseAI>();
            Assert.IsNotNull(ai);
            if (ai.boltPrefab == null)
                ai.boltPrefab = enemySpawner.hostileBolt;
            Assert.IsTrue(enemyObject.GetComponent<EnemyHealth>().definition.isRanged);
            float t = 0f;
            HostileProjectile shot = null;
            while (t < 3.5f && shot == null)
            {
                yield return null;
                SkipLevelUp();
                shot = Object.FindAnyObjectByType<HostileProjectile>();
                t += Time.deltaTime;
            }
            Capture("12_ranged");
            Assert.IsNotNull(shot, "ranged slime never fired a projectile");
        }

        [UnityTest]
        public IEnumerator BossAppearsAtFloorTenWithWarning()
        {
            var flow = GameFlowController.Instance;
            var stage = Object.FindAnyObjectByType<StageManager>();
            Assert.IsNotNull(stage);
            stage.ForceFloor(10);
            float wait = 0f;
            while (stage.ActiveBoss == BossKind.None && wait < 2f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.AreEqual(10, stage.Floor);
            Assert.AreEqual(BossKind.Rock, stage.ActiveBoss);
            Assert.AreEqual("Rock Slime", BossCatalog.DisplayName(stage.ActiveBoss));
            Assert.AreEqual(GameState.BossIntro, flow.State);
            Assert.IsTrue(flow.ui.BossWarningVisible || flow.ui.BossWarningName == "Rock Slime" || flow.ui.bossWarningName.text == "Rock Slime",
                "boss warning did not appear");
            EnemyHealth boss = null;
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy != null && enemy.IsBoss)
                    boss = enemy;
            }
            Assert.IsNotNull(boss, "Rock Slime was not spawned");
            Assert.AreEqual(10000000, boss.Max, "floor 10 boss HP should be 10.000.000");
            Assert.AreEqual(BossCatalog.ContactDamageForFloor(10), boss.ContactDamage, "boss basic attack should scale with the floor");
            yield return null;
            Assert.IsTrue(flow.ui.BossBarVisible, "boss HP bar did not appear");
            Assert.AreEqual("ROCK SLIME", flow.ui.BossBarName);
            Assert.AreEqual("10.000.000 / 10.000.000", flow.ui.BossBarText);
            Assert.AreEqual(1f, flow.ui.BossBarFill01, 0.001f);
            Capture("13_boss_intro");
            wait = 0f;
            while (flow.State == GameState.BossIntro && wait < 3.2f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.AreEqual(GameState.Playing, flow.State, "boss intro never ended");
            Assert.IsTrue(stage.Intermission, "regular spawning should pause during the boss");

            var controller = boss.GetComponent<BossController>();
            Assert.IsNotNull(controller);
            Assert.AreEqual(1, controller.EnrageSkillCount);
            Assert.IsFalse(controller.Enraged);
            Assert.IsNotNull(controller.minionPrefab, "boss has no minion prefab");
            int beforeAdds = NonBossCount();
            controller.SpawnAdds(10);
            yield return null;
            Assert.GreaterOrEqual(NonBossCount() - beforeAdds, 10, "boss should summon 10 small slimes");
            SkyfallHazard.Drop(PlayerHealth.Instance.transform.position + Vector3.forward * 2f, 1, 0.4f, Color.gray, 1f);
            yield return null;
            var skyfall = Object.FindAnyObjectByType<SkyfallHazard>();
            Assert.IsNotNull(skyfall, "rock skyfall hazard did not spawn");
            Assert.IsNotNull(skyfall.transform.Find("Penumbra"), "skyfall has no soft penumbra shadow");
            Assert.IsNotNull(skyfall.transform.Find("Umbra"), "skyfall has no contact umbra shadow");
            Assert.IsNotNull(skyfall.transform.Find("Mark"), "skyfall has no impact mark");
            AssertCapeAndCrown(boss);
            while (boss != null && !boss.IsDead && boss.Current > boss.Max * 0.3f)
                boss.TakeDamage(800000);
            Assert.IsNotNull(boss);
            Assert.IsFalse(boss.IsDead);
            Assert.IsTrue(controller.Enraged, "boss should enrage below 30% HP");
            Assert.LessOrEqual((float)boss.Current / boss.Max, 0.3f);
            yield return null;
            Assert.AreEqual((float)boss.Current / boss.Max, flow.ui.BossBarFill01, 0.001f, "boss HP bar did not follow the damage");
            Assert.AreEqual(UIController.Thousands(boss.Current) + " / 10.000.000", flow.ui.BossBarText);
            Assert.AreEqual("ENRAGE", flow.ui.bossBarTagText.text);
            Capture("23_boss_hp_bar");

            PlayerHealth.Instance.GiveInvulnerability(30f);
            yield return null;
            yield return null;
            Assert.IsTrue(controller.EnrageCutscenePlaying, "enrage should start a cutscene");
            Assert.AreEqual(GameState.BossIntro, flow.State, "the fight should freeze during the enrage cutscene");
            Assert.AreEqual("ENRAGE!", flow.ui.BossWarningTitle);
            var rig = Camera.main.GetComponent<FollowCamera>();
            Assert.IsTrue(rig.IsFocusing, "camera should zoom to the boss");
            Assert.AreEqual(boss.transform, rig.FocusTarget);
            yield return new WaitForSecondsRealtime(0.9f);
            float toBoss = Vector3.Distance(rig.transform.position, boss.transform.position);
            Debug.Log("PLAYTEST enrage camera distance " + toBoss.ToString("F2"));
            Assert.Less(toBoss, 6f, "camera did not zoom in on the boss");
            Capture("19_boss_enrage_cutscene");
            wait = 0f;
            while (controller.EnrageCutscenePlaying && wait < 3f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.IsFalse(controller.EnrageCutscenePlaying, "enrage cutscene never ended");
            Assert.AreEqual(GameState.Playing, flow.State);
            Assert.AreEqual(1f, Time.timeScale, "slow motion should end with the cutscene");
            yield return null;
            Assert.IsFalse(rig.IsFocusing, "camera should return to the hero");

            wait = 0f;
            while (controller.EnrageFlourishes == 0 && wait < 4f)
            {
                yield return null;
                PlayerHealth.Instance.GiveInvulnerability(10f);
                wait += Time.unscaledDeltaTime;
            }
            Assert.Greater(controller.EnrageFlourishes, 0, "enraged skills should fire the flashy flourish");
            Capture("20_boss_enrage_skill");
        }

        [UnityTest]
        public IEnumerator FloorAdvancesAfterKillQuota()
        {
            var stage = Object.FindAnyObjectByType<StageManager>();
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.IsNotNull(stage);
            Assert.IsNotNull(stage.portalPrefab, "StageManager has no portal prefab");
            stage.ForceFloor(1);
            Assert.AreEqual(1, stage.Floor);
            int quota = RunRules.KillQuota(1);
            Assert.AreEqual(10, quota);
            KillSlimes(spawner, quota);
            yield return null;
            Assert.AreEqual(1, stage.Floor, "clearing the quota should open a door, not skip the floor");
            Assert.IsNotNull(ExitPortal.Active, "exit portal did not appear after the kill quota");
            Capture("15_portal");
            var firstTheme = stage.Theme;
            Assert.AreEqual(Biome.Forest, firstTheme.biome);
            for (int i = 0; i < 6; i++)
                Object.Instantiate(spawner.normalPrefab, new Vector3(-4f + i * 0.3f, 0f, 6f), Quaternion.identity);
            ExitPortal.Active.Enter();
            float wait = 0f;
            while (stage.Floor < 2 && wait < 3f)
            {
                yield return null;
                wait += Time.unscaledDeltaTime;
            }
            Assert.AreEqual(2, stage.Floor, "entering the portal should move to lantai 2");
            yield return Hold(0.8f);
            Assert.IsFalse(stage.FloorCleared);
            Assert.IsNull(ExitPortal.Active, "leftover slimes must not open the next floor immediately");
            spawner.Paused = true;
            GameFlowController.Instance.Stats.attackRange = 0.1f;
            EnemyHealth.VanishAll();
            Assert.AreEqual(20, RunRules.KillQuota(2));
            int remaining = Mathf.Max(1, RunRules.KillQuota(2) - stage.FloorKills);
            if (remaining > 1)
                KillSlimes(spawner, remaining - 1);
            yield return null;
            Assert.AreEqual(2, stage.Floor);
            Assert.IsNull(ExitPortal.Active, "floor 2 needs 20 kills before the door opens");
            KillSlimes(spawner, 1);
            yield return null;
            Assert.IsNotNull(ExitPortal.Active, "the door should open after 20 kills on floor 2");
            Assert.AreEqual(Biome.Forest, stage.Theme.biome, "floors 1-10 stay on the grass biome");
            Assert.AreNotEqual(firstTheme.weather, stage.Theme.weather, "weather should change between floors");
            Assert.AreNotEqual(firstTheme.seed, stage.Theme.seed, "layout seed should change every floor");
        }

        [UnityTest]
        public IEnumerator BiomeChangesEveryTenFloors()
        {
            var stage = Object.FindAnyObjectByType<StageManager>();
            var generator = Object.FindAnyObjectByType<ArenaGenerator>();
            Assert.IsNotNull(stage);
            Assert.IsNotNull(generator);
            stage.ForceFloor(11);
            yield return null;
            Assert.AreEqual(Biome.Desert, stage.Theme.biome);
            Assert.AreEqual(generator.sandGround, generator.groundRenderer.sharedMaterial);
            stage.ForceFloor(21);
            yield return null;
            Assert.AreEqual(Biome.Snow, stage.Theme.biome);
            Assert.AreEqual(generator.snowGround, generator.groundRenderer.sharedMaterial);
            stage.ForceFloor(31);
            yield return null;
            Assert.AreEqual(Biome.Thunder, stage.Theme.biome);
            Capture("17_biome_thunder");
        }

        [UnityTest]
        public IEnumerator EveryBiomeBuildsHdEnvironment()
        {
            var stage = Object.FindAnyObjectByType<StageManager>();
            var generator = Object.FindAnyObjectByType<ArenaGenerator>();
            PlayerHealth.Instance.GiveInvulnerability(120f);
            Assert.IsNotNull(PostFx.Instance, "arena has no post-processing volume");
            Assert.IsTrue(PostFx.Instance.Active, "post-processing should be on at the default (High) preset");
            var seen = new HashSet<Biome>();
            foreach (int floor in new[] { 1, 11, 21, 31, 41, 51, 61, 71, 81, 91 })
            {
                stage.ForceFloor(floor);
                yield return null;
                yield return null;
                foreach (var enemy in new List<EnemyHealth>(EnemyHealth.Active))
                {
                    if (enemy != null)
                        enemy.Vanish();
                }
                PlayerHealth.Instance.GiveInvulnerability(120f);
                yield return new WaitForSeconds(0.3f);
                var biome = stage.Theme.biome;
                seen.Add(biome);
                Assert.AreEqual("SlimeBreaker/Ground", generator.groundRenderer.sharedMaterial.shader.name, biome + " ground is not on the HD shader");
                int hd = 0;
                foreach (var renderer in generator.Root.GetComponentsInChildren<Renderer>())
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader.name == "SlimeBreaker/Lit")
                            hd++;
                    }
                }
                Assert.Greater(hd, 0, biome + " has no HD props");
                var groundMat = generator.groundRenderer.sharedMaterial;
                Debug.Log("PLAYTEST ground " + biome + " " + groundMat.name + " base " + (groundMat.GetTexture("_BaseMap") != null ? groundMat.GetTexture("_BaseMap").name : "null")
                    + " low " + Shader.IsKeywordEnabled("SB_QUALITY_LOW") + " sun " + (RenderSettings.sun != null ? RenderSettings.sun.intensity.ToString("0.00") : "-"));
                Debug.Log("PLAYTEST biome " + biome + " hd materials " + hd + " cover " + generator.CoverAmount.ToString("0.00")
                    + " grade T" + PostFx.Instance.Current.temperature.ToString("0") + " sat " + PostFx.Instance.Current.saturation.ToString("0"));
                Capture("hd_" + floor.ToString("00") + "_" + biome);
            }
            Assert.AreEqual(10, seen.Count, "not every biome was visited");
        }

        [UnityTest]
        public IEnumerator RenderBudgetPerBiome()
        {
            var stage = Object.FindAnyObjectByType<StageManager>();
            var generator = Object.FindAnyObjectByType<ArenaGenerator>();
            Assert.IsNotNull(Object.FindAnyObjectByType<AdaptiveResolution>(), "GameArena has no AdaptiveResolution");
            foreach (int floor in new[] { 1, 11, 21, 31, 41, 51, 61, 71, 81, 91 })
            {
                stage.ForceFloor(floor);
                foreach (var enemy in new List<EnemyHealth>(EnemyHealth.Active))
                {
                    if (enemy != null)
                        enemy.Vanish();
                }
                PlayerHealth.Instance.GiveInvulnerability(120f);
                yield return null;
                yield return null;
                int renderers = 0;
                int blocks = 0;
                int casters = 0;
                int cards = 0;
                foreach (var renderer in generator.Root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        continue;
                    renderers++;
                    if (renderer.HasPropertyBlock() && !(renderer is ParticleSystemRenderer))
                        blocks++;
                    if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                        casters++;
                    string n = renderer.name.ToLowerInvariant();
                    if (n.Contains("grass") || n.Contains("flower") || n.Contains("reed"))
                        cards++;
                }
                var biome = stage.Theme.biome;
                Debug.Log("PLAYTEST budget " + biome + " " + stage.Theme.weather + " renderers " + renderers + " casters " + casters + " mpb " + blocks
                    + " scatter meshes " + generator.CombinedScatterMeshes + " loose cards " + cards);
                Assert.Less(renderers, 200, biome + " arena has too many renderers (draw calls)");
                Assert.AreEqual(0, blocks, biome + " props use property blocks, which break SRP batching");
                Assert.Less(casters, 140, biome + " has too many shadow casters");
                foreach (var rig in Object.FindObjectsByType<WeatherRig>(FindObjectsSortMode.None))
                {
                    var main = Camera.main;
                    foreach (var particleRenderer in rig.GetComponentsInChildren<ParticleSystemRenderer>())
                    {
                        if (particleRenderer.renderMode == ParticleSystemRenderMode.Billboard && particleRenderer.name != "Splashes" && !rig.name.Contains("Mist"))
                            Assert.LessOrEqual(particleRenderer.maxParticleSize, 0.05f, rig.name + " particles can blow up into screen-filling blobs near the camera");
                    }
                    if (rig.ambienceId == null && main != null)
                        Assert.Less(rig.height, main.transform.position.y - 3f, rig.name + " drifts at camera height");
                }
            }
        }

        [UnityTest]
        public IEnumerator HudShowsFloorAndHeroDamage()
        {
            var flow = GameFlowController.Instance;
            Assert.IsNotNull(flow.ui);
            Assert.IsNotNull(flow.ui.floorText, "HUD has no floor label");
            Assert.IsNotNull(flow.ui.damageText, "HUD has no damage label");
            yield return null;
            Assert.IsTrue(flow.ui.floorText.text.Contains("1"), flow.ui.floorText.text);
            Assert.IsTrue(flow.ui.floorText.text.Contains("100"), flow.ui.floorText.text);
            Assert.IsNotNull(flow.ui.floorProgressText, "HUD has no floor kill progress");
            Assert.IsTrue(flow.ui.floorProgressText.text.Contains("0"), flow.ui.floorProgressText.text);
            Assert.IsTrue(flow.ui.floorProgressText.text.Contains("10"), flow.ui.floorProgressText.text);
            Assert.IsTrue(flow.ui.damageText.text.Contains(flow.Stats.damage.ToString()), flow.ui.damageText.text);
            Assert.IsTrue(flow.ui.levelText.text.Contains(flow.Stats.level.ToString()), flow.ui.levelText.text);
            Assert.IsNotNull(flow.ui.statValues, "HUD has no status card");
            Assert.AreEqual(UIController.StatKeys.Length, flow.ui.statValues.Length);
            foreach (var cell in flow.ui.statValues)
                Assert.IsNotNull(cell, "status card has an empty cell");
            string summary = flow.ui.StatusSummary;
            foreach (var key in new[] { "DMG", "CRIT", "CRIT RATE", "ATK SPD", "MOVE", "LS", "RANGE", "EXP +", "DEF 0%", "TAKEN 100%" })
                StringAssert.Contains(key, summary);
            Assert.AreEqual(flow.Stats.damage.ToString(), flow.ui.StatValue("DMG"));
            Assert.AreEqual(flow.ui.StatValue("DMG"), flow.ui.statValues[0].text, "status card cell not filled");
            flow.Stats.damageReduction = 0.16f;
            yield return null;
            Assert.AreEqual("16%", flow.ui.StatValue("DEF"));
            Assert.AreEqual("84%", flow.ui.StatValue("TAKEN"));
            Assert.AreEqual("16%", flow.ui.statValues[System.Array.IndexOf(UIController.StatKeys, "DEF")].text);
            Assert.AreEqual("EXP", flow.ui.xpSlider.gameObject.name);
            Assert.IsNotNull(Object.FindAnyObjectByType<Wayfinder>(), "HUD has no wayfinder arrows");
            var upgrades = Object.FindAnyObjectByType<UpgradeManager>();
            Assert.IsNotNull(upgrades);
            bool critRate = false;
            bool critDamage = false;
            foreach (var upgrade in upgrades.pool)
            {
                if (upgrade == null)
                    continue;
                if (upgrade.kind == UpgradeKind.CritChance)
                    critRate = true;
                if (upgrade.kind == UpgradeKind.CritDamage)
                    critDamage = true;
                if (upgrade.kind == UpgradeKind.Xp)
                    Assert.AreEqual("EXP", upgrade.title);
                if (upgrade.kind == UpgradeKind.Armor)
                    Assert.AreEqual("Defender", upgrade.title);
                foreach (var indonesian in new[] { "Perisai", "Serangan", "Laju", "Nyawa", "Jangkauan" })
                    Assert.AreNotEqual(indonesian, upgrade.title, "upgrade names should be English");
            }
            Assert.IsTrue(critRate, "Crit Rate upgrade missing from the pool");
            Assert.IsTrue(critDamage, "Critical upgrade missing from the pool");
            Capture("14_hud_floor_damage");
        }

        [UnityTest]
        public IEnumerator ObstaclesHaveColliders()
        {
            yield return null;
            var generator = Object.FindAnyObjectByType<ArenaGenerator>();
            Assert.IsNotNull(generator);
            Assert.Greater(generator.PlacedObstacles.Count, 0, "arena has no solid obstacles");
            int layer = LayerMask.NameToLayer("Obstacle");
            Assert.Greater(layer, 0);
            foreach (var obstacle in generator.PlacedObstacles)
            {
                if (obstacle == null)
                    continue;
                var collider = obstacle.GetComponentInChildren<Collider>();
                Assert.IsNotNull(collider, obstacle.name + " has no collider");
                Assert.IsFalse(collider.isTrigger, obstacle.name + " collider is a trigger");
                Assert.AreEqual(layer, obstacle.gameObject.layer, obstacle.name + " is not on the Obstacle layer");
            }
        }

        [UnityTest]
        public IEnumerator DamageNumbersAppearForHeroAndEnemy()
        {
            var player = PlayerHealth.Instance;
            var enemySpawner = Object.FindAnyObjectByType<EnemySpawner>();
            var enemyObject = Object.Instantiate(enemySpawner.normalPrefab, player.transform.position + Vector3.forward * 3f, Quaternion.identity);
            var enemy = enemyObject.GetComponent<EnemyHealth>();
            enemy.TakeDamage(7, true, true, false);
            yield return null;
            Assert.Greater(Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length, 0, "enemy hit showed no damage number");
            enemy.TakeDamage(11, true, true, true);
            yield return null;
            bool sawCrit = false;
            foreach (var popup in Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None))
            {
                var mesh = popup.GetComponent<TextMesh>();
                if (mesh != null && mesh.text != null && mesh.text.Contains("CRIT"))
                    sawCrit = true;
            }
            Assert.IsTrue(sawCrit, "critical hit did not use the CRIT damage number");
            player.TakeDamage(4);
            yield return null;
            Assert.Greater(Object.FindObjectsByType<DamagePopup>(FindObjectsSortMode.None).Length, 0, "hero hit showed no damage number");
            Capture("16_damage_numbers");
        }

        [UnityTest]
        public IEnumerator GraphicsPresetsApply()
        {
            yield return null;
            var graphics = Object.FindAnyObjectByType<GraphicsService>();
            Assert.IsNotNull(graphics, "GameArena has no GraphicsService");
            Assert.GreaterOrEqual(graphics.presets.Length, 3);
            graphics.Apply(GraphicsService.Low, persist: false);
            Assert.AreEqual(GraphicsService.Low, graphics.Level);
            graphics.Apply(GraphicsService.High, persist: false);
            Assert.AreEqual(GraphicsService.High, graphics.Level);
            Assert.IsNotNull(QualitySettings.renderPipeline);
        }

        static void AssertCapeAndCrown(EnemyHealth boss)
        {
            Assert.IsNotNull(boss);
            var renderer = boss.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(renderer, "boss has no skinned mesh");
            var materials = renderer.sharedMaterials;
            Assert.IsNotNull(materials);
            bool sawCape = false;
            bool sawCrown = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                    continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                Color color = block.GetColor("_BaseColor");
                string name = materials[i].name.ToLowerInvariant();
                if (name.Contains("cape"))
                {
                    sawCape = true;
                    Assert.Greater(color.r, color.g + 0.3f, "boss cape should be red, got " + color);
                    Assert.Greater(color.r, color.b + 0.3f, "boss cape should be red, got " + color);
                }
                if (name.Contains("gold") || name.Contains("crown"))
                {
                    sawCrown = true;
                    Assert.Greater(color.r, 0.85f, "boss crown should be gold, got " + color);
                    Assert.Greater(color.g, 0.55f, "boss crown should be gold, got " + color);
                }
            }
            Assert.IsTrue(sawCape, "boss mesh has no cape material");
            Assert.IsTrue(sawCrown, "boss mesh has no gold/crown material");
        }

        static void KillSlimes(EnemySpawner spawner, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var enemyObject = Object.Instantiate(spawner.normalPrefab, new Vector3(4f + (i % 8) * 0.25f, 0f, 4f + (i / 8) * 0.25f), Quaternion.identity);
                var health = enemyObject.GetComponent<EnemyHealth>();
                health.TakeDamage(9999);
            }
        }

        static int NonBossCount()
        {
            int count = 0;
            foreach (var enemy in EnemyHealth.Active)
            {
                if (enemy != null && !enemy.IsBoss)
                    count++;
            }
            return count;
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
            float attackRange = 4.4f;
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            if (stats != null)
                attackRange = stats.attackRange;
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
                else if (nearestDistance <= attackRange * 0.9f)
                    wish = Vector3.zero;
                else
                    wish = toEnemy.normalized + crowd * 0.5f;
            }
            if (flat.magnitude > 16f)
                wish -= flat.normalized;
            if (wish.sqrMagnitude < 0.0001f)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                return;
            }
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
            // Gameplay lives on y = 0. The authored ground mesh rises a little at the rim of the 22 m arena.
            float expected = surface > 0.15f && Mathf.Abs(target.transform.position.y) < 0.08f ? 0f : surface;
            Assert.That(feet, Is.EqualTo(expected).Within(FeetTolerance), label + ": feet " + feet + " vs ground " + surface);
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
