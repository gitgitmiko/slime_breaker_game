using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlimeBreaker.Tests
{
    public class ArenaPlaytest
    {
        const float FeetTolerance = 0.12f;

        Keyboard keyboard;
        readonly List<Vector3> groundVertices = new List<Vector3>();

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            SceneManager.LoadScene("GameArena");
            yield return null;
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>();
            keyboard.MakeCurrent();
            CollectGroundVertices();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroAndEnemiesStandOnVisibleGround()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "GameArena has no PlayerController");
            Assert.IsNotEmpty(groundVertices, "GameArena has no ground mesh");

            yield return Hold(0.5f);
            Capture("01_spawn");
            AssertOnGround(player.gameObject, "hero at spawn");

            var route = new[] { Key.W, Key.D, Key.S, Key.A, Key.W };
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (var key in route)
            {
                float t = 0f;
                Press(key);
                while (t < 1.2f)
                {
                    yield return null;
                    t += Time.deltaTime;
                    lowest = Mathf.Min(lowest, player.transform.position.y);
                    highest = Mathf.Max(highest, player.transform.position.y);
                    if (key == Key.D && t > 0.5f && t < 0.6f)
                        Tap(Key.Space);
                }
                AssertOnGround(player.gameObject, "hero after moving " + key);
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
                wait += Time.deltaTime;
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
            while (t < 40f && flow.State != GameState.Defeat)
            {
                if (flow.State == GameState.LevelUp)
                    flow.ChooseUpgrade(0);
                foreach (var enemy in EnemyHealth.Active)
                    tracked.Add(enemy);
                Press(((int)(t / 2f) % 2) == 0 ? Key.A : Key.D);
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
            Assert.Greater(flow.Stats.level, startLevel, "the hero never levelled up");
        }

        IEnumerator Hold(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
            }
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
            groundVertices.Clear();
            var ground = GameObject.Find("ENV_Forest_Ground");
            if (ground == null)
                return;
            foreach (var filter in ground.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                    groundVertices.Add(matrix.MultiplyPoint3x4(v));
            }
        }

        float GroundSurface(Vector3 at)
        {
            float best = float.MinValue;
            float reach = 0.9f;
            foreach (var v in groundVertices)
            {
                float dx = v.x - at.x;
                float dz = v.z - at.z;
                if (dx * dx + dz * dz <= reach * reach && v.y > best)
                    best = v.y;
            }
            return best == float.MinValue ? 0f : best;
        }

        static float Feet(GameObject target)
        {
            float lowest = float.MaxValue;
            var baked = new Mesh();
            foreach (var skinned in target.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(baked, true);
                var t = skinned.transform;
                foreach (var v in baked.vertices)
                    lowest = Mathf.Min(lowest, (t.position + t.rotation * v).y);
            }
            foreach (var filter in target.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                    continue;
                if (filter.GetComponent<MeshRenderer>() == null || !filter.GetComponent<MeshRenderer>().enabled || !filter.gameObject.activeInHierarchy)
                    continue;
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices)
                    lowest = Mathf.Min(lowest, matrix.MultiplyPoint3x4(v).y);
            }
            Object.Destroy(baked);
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
            var rt = new RenderTexture(720, 1280, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            var dir = Path.Combine(Application.dataPath, "..", "Logs", "playtest");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
