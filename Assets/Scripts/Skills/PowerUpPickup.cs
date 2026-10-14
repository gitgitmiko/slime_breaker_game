using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Floating crystal that grants a skill when the hero walks into it.</summary>
    public class PowerUpPickup : MonoBehaviour
    {
        public static readonly List<PowerUpPickup> Active = new List<PowerUpPickup>();

        public SkillDefinition skill;
        public float pickupRadius = 1.15f;
        public Transform crystal;
        public Transform beam;
        public float bobHeight = 0.18f;
        public float spinSpeed = 90f;

        float age;
        Vector3 crystalHome;
        static MaterialPropertyBlock block;

        void OnEnable()
        {
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        void Start()
        {
            if (crystal != null)
                crystalHome = crystal.localPosition;
            Tint();
        }

        public void Configure(SkillDefinition definition)
        {
            skill = definition;
            Tint();
        }

        void Tint()
        {
            if (skill == null)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                // Keep the material's own alpha so translucent parts (the light beam) stay translucent.
                Color tint = skill.color;
                var material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_BaseColor"))
                    tint.a = material.GetColor("_BaseColor").a;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                block.SetColor("_EmissionColor", skill.color * 1.2f);
                renderer.SetPropertyBlock(block);
            }
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.startColor = skill.color;
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (crystal != null)
            {
                crystal.localPosition = crystalHome + Vector3.up * Mathf.Sin(age * 2.4f) * bobHeight;
                crystal.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }
            if (beam != null)
                beam.Rotate(0f, -spinSpeed * 0.4f * Time.deltaTime, 0f, Space.World);

            if (!GameFlowController.IsPlaying)
                return;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= pickupRadius * pickupRadius)
                Collect();
        }

        public void Collect()
        {
            var controller = SkillController.Instance;
            int level = controller != null ? controller.Grant(skill) : 0;
            var flow = GameFlowController.Instance;
            if (flow != null && flow.ui != null && skill != null)
            {
                string label = level > 1 ? skill.title + " Lv " + level : "Skill baru: " + skill.title;
                flow.ui.ShowToast(label, skill.color);
            }
            AudioService.Instance?.Play("powerup");
            VfxPlayer.Instance?.Play("powerup", transform.position + Vector3.up * 0.8f, skill != null ? skill.color : Color.white);
            Destroy(gameObject);
        }
    }
}
