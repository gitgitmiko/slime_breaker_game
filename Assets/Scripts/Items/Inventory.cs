using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>
    /// Run inventory: up to <see cref="Capacity"/> items and one equipped item per slot. Equipped gear feeds the
    /// RunStats gear fields (ATK, HP, DEF, move speed). Also drops loot: 5% per slime kill, four items from a boss
    /// (one guaranteed Ultra).
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        public const int Capacity = 40;

        public ItemPickup pickupPrefab;
        /// <summary>Indexed by <see cref="ItemSlot"/>.</summary>
        public Sprite[] slotIcons = new Sprite[ItemCatalog.SlotCount];

        public readonly List<ItemData> Items = new List<ItemData>();
        readonly int[] equipped = { -1, -1, -1, -1, -1 };
        int nextId = 1;

        public event Action Changed;

        void Awake()
        {
            Instance = this;
        }

        void OnEnable()
        {
            EnemyHealth.AnyDied += OnEnemyDied;
        }

        void OnDisable()
        {
            EnemyHealth.AnyDied -= OnEnemyDied;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool IsFull => Items.Count >= Capacity;

        public Sprite IconFor(ItemSlot slot)
        {
            int index = (int)slot;
            return slotIcons != null && index < slotIcons.Length ? slotIcons[index] : null;
        }

        public ItemData Find(int id) => Items.Find(item => item.id == id);

        public ItemData Equipped(ItemSlot slot) => equipped[(int)slot] >= 0 ? Find(equipped[(int)slot]) : null;

        public bool IsEquipped(ItemData item) => item != null && equipped[item.slot] == item.id;

        /// <summary>Adds an item (auto-equips it when that slot is empty). False when the bag is full.</summary>
        public bool Add(ItemData item)
        {
            if (item == null || IsFull)
                return false;
            item.id = nextId++;
            Items.Add(item);
            if (equipped[item.slot] < 0)
                Equip(item.id);
            else
                Changed?.Invoke();
            return true;
        }

        public void Equip(int id)
        {
            var item = Find(id);
            if (item == null)
                return;
            equipped[item.slot] = item.id;
            Recompute();
        }

        public void Unequip(ItemSlot slot)
        {
            equipped[(int)slot] = -1;
            Recompute();
        }

        public void Discard(int id)
        {
            var item = Find(id);
            if (item == null)
                return;
            if (IsEquipped(item))
                equipped[item.slot] = -1;
            Items.Remove(item);
            Recompute();
        }

        /// <summary>Push equipped gear into RunStats and resize the hero's HP bar.</summary>
        public void Recompute()
        {
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            if (stats != null)
            {
                int beforeMax = stats.TotalMaxHealth;
                stats.gearDamage = 0;
                stats.gearHealth = 0;
                stats.gearDefense = 0f;
                stats.gearSpeed = 0f;
                for (int slot = 0; slot < equipped.Length; slot++)
                {
                    var item = equipped[slot] >= 0 ? Find(equipped[slot]) : null;
                    if (item == null)
                    {
                        equipped[slot] = -1;
                        continue;
                    }
                    stats.gearDamage += item.damage;
                    stats.gearHealth += item.health;
                    stats.gearDefense += item.defense;
                    stats.gearSpeed += item.speed;
                }
                int gained = stats.TotalMaxHealth - beforeMax;
                PlayerHealth.Instance?.SetMaxHealth(stats.TotalMaxHealth, Mathf.Max(0, gained));
            }
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ save / resume

        public void Capture(RunSave data)
        {
            data.items = Items.ToArray();
            data.equippedItems = (int[])equipped.Clone();
            data.nextItemId = nextId;
        }

        public void Restore(RunSave data)
        {
            Items.Clear();
            if (data != null && data.items != null)
            {
                foreach (var item in data.items)
                {
                    if (item != null && item.slot >= 0 && item.slot < ItemCatalog.SlotCount)
                        Items.Add(item);
                }
            }
            for (int i = 0; i < equipped.Length; i++)
                equipped[i] = data != null && data.equippedItems != null && i < data.equippedItems.Length ? data.equippedItems[i] : -1;
            nextId = 1;
            foreach (var item in Items)
                nextId = Mathf.Max(nextId, item.id + 1);
            if (data != null)
                nextId = Mathf.Max(nextId, data.nextItemId);
            Recompute();
        }

        // ------------------------------------------------------------------ loot

        void OnEnemyDied(EnemyHealth enemy)
        {
            if (enemy == null || pickupPrefab == null)
                return;
            int floor = StageManager.Instance != null ? StageManager.Instance.Floor : 1;
            Vector3 origin = enemy.transform.position;
            if (enemy.IsBoss)
            {
                var drops = ItemCatalog.RollBossDrops(() => UnityEngine.Random.value, floor);
                for (int i = 0; i < drops.Count; i++)
                {
                    float angle = (i + 0.5f) / drops.Count * Mathf.PI * 2f;
                    Spawn(drops[i], origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f);
                }
                return;
            }
            if (UnityEngine.Random.value < ItemCatalog.DropChance)
                Spawn(ItemCatalog.Roll(() => UnityEngine.Random.value, floor), origin);
        }

        public ItemPickup Spawn(ItemData item, Vector3 position)
        {
            if (pickupPrefab == null || item == null)
                return null;
            position.y = 0f;
            float limit = RunRules.ArenaRadius * 0.5f - 1f;
            if (position.magnitude > limit)
                position = position.normalized * limit;
            var pickup = Instantiate(pickupPrefab, position, Quaternion.identity);
            pickup.Configure(item, IconFor(item.Slot));
            return pickup;
        }
    }
}
