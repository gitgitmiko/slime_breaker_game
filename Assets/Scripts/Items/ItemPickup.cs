using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Dropped equipment: a rarity-coloured gem and beam with the item icon floating above it.</summary>
    public class ItemPickup : MonoBehaviour
    {
        public static readonly List<ItemPickup> Active = new List<ItemPickup>();

        public float pickupRadius = 1.1f;
        public Transform gem;
        public Transform beam;
        public SpriteRenderer icon;

        public ItemData Item { get; private set; }

        float age;
        float blockedUntil;
        Vector3 gemHome;
        static MaterialPropertyBlock block;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start()
        {
            if (gem != null)
                gemHome = gem.localPosition;
        }

        public void Configure(ItemData item, Sprite sprite)
        {
            Item = item;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (item == null)
                return;
            Color tint = UpgradeRarities.Color(item.Rarity);
            if (block == null)
                block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer == icon)
                    continue;
                Color c = tint;
                var material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_BaseColor"))
                    c.a = material.GetColor("_BaseColor").a;
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                block.SetColor("_EmissionColor", tint * 1.1f);
                renderer.SetPropertyBlock(block);
            }
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.startColor = tint;
            }
            // Rarer loot stands taller so it reads from across the arena.
            if (beam != null)
            {
                float height = 0.8f + item.rarity * 0.5f;
                beam.localScale = new Vector3(beam.localScale.x, height, beam.localScale.z);
                beam.localPosition = new Vector3(0f, height, 0f);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (gem != null)
            {
                gem.localPosition = gemHome + Vector3.up * Mathf.Sin(age * 2.6f) * 0.12f;
                gem.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            }
            if (icon != null && Camera.main != null)
                icon.transform.rotation = Camera.main.transform.rotation;
            if (!GameFlowController.IsPlaying || Time.time < blockedUntil)
                return;
            var player = PlayerHealth.Instance;
            if (player == null || player.IsDead)
                return;
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= pickupRadius * pickupRadius)
                Collect();
        }

        public bool Collect()
        {
            var inventory = Inventory.Instance;
            var ui = GameFlowController.Instance != null ? GameFlowController.Instance.ui : null;
            if (inventory == null || Item == null)
                return false;
            if (!inventory.Add(Item))
            {
                ui?.ShowToast("Tas penuh (" + Inventory.Capacity + ") — buang item di Inventori", new Color(1f, 0.5f, 0.4f));
                blockedUntil = Time.time + 3f;
                return false;
            }
            Color tint = UpgradeRarities.Color(Item.Rarity);
            string equippedNote = inventory.IsEquipped(Item) ? " · dipakai" : string.Empty;
            ui?.ShowToast(UpgradeRarities.Name(Item.Rarity) + " " + Item.Name + "  Lv " + Item.band + "  " + Item.Stats + equippedNote, tint);
            AudioService.Instance?.Play(Item.rarity >= (int)UpgradeRarity.Legend ? "powerup" : "pickup");
            VfxPlayer.Instance?.Play("powerup", transform.position + Vector3.up * 0.6f, tint);
            GameFlowController.Instance?.PersistRun();
            Destroy(gameObject);
            return true;
        }

        /// <summary>Leaving a floor sweeps up the loot still lying around, so nothing is lost to the portal.</summary>
        public static void CollectAll()
        {
            foreach (var pickup in Active.ToArray())
            {
                if (pickup != null && !pickup.Collect() && pickup != null)
                    Destroy(pickup.gameObject);
            }
        }
    }
}
