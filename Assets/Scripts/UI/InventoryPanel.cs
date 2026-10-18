using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeBreaker
{
    /// <summary>
    /// Inventory screen: five equipment slots on the left (Staff, Topi, Baju, Celana, Sepatu), the bag grid on the
    /// right (rarity-coloured cells, "E" on worn items), and a detail strip to wear, take off or throw away the
    /// selected item with a comparison against what is worn now.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        public Button openButton;
        public GameObject panel;
        public Button closeButton;

        public Button[] slotButtons;
        public Image[] slotFrames;
        public Image[] slotIcons;
        public Text[] slotTexts;

        public Button[] cellButtons;
        public Image[] cellFrames;
        public Image[] cellIcons;
        public Text[] cellMarks;

        public Text capacityText;
        public Text totalsText;
        public Text detailTitle;
        public Text detailBody;
        public Button equipButton;
        public Text equipLabel;
        public Button discardButton;

        static readonly Color EmptyFrame = new Color(1f, 1f, 1f, 0.12f);

        readonly List<ItemData> ordered = new List<ItemData>();
        int selectedId = -1;
        bool wired;

        public bool IsOpen => panel != null && panel.activeSelf;
        public IReadOnlyList<ItemData> Ordered => ordered;
        public int SelectedId => selectedId;

        void Start()
        {
            Wire();
            if (panel != null)
                panel.SetActive(false);
        }

        void OnEnable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.Changed += Refresh;
        }

        void OnDisable()
        {
            if (Inventory.Instance != null)
                Inventory.Instance.Changed -= Refresh;
        }

        void Wire()
        {
            if (wired)
                return;
            wired = true;
            if (Inventory.Instance != null)
            {
                Inventory.Instance.Changed -= Refresh;
                Inventory.Instance.Changed += Refresh;
            }
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            equipButton?.onClick.AddListener(ToggleEquip);
            discardButton?.onClick.AddListener(DiscardSelected);
            for (int i = 0; slotButtons != null && i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i]?.onClick.AddListener(() => SelectSlot(slot));
            }
            for (int i = 0; cellButtons != null && i < cellButtons.Length; i++)
            {
                int cell = i;
                cellButtons[i]?.onClick.AddListener(() => SelectCell(cell));
            }
        }

        public void Open()
        {
            Wire();
            var flow = GameFlowController.Instance;
            if (flow == null || !flow.OpenInventory())
                return;
            AudioService.Instance?.Play("ui");
            selectedId = -1;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null)
                panel.SetActive(false);
            AudioService.Instance?.Play("ui");
            GameFlowController.Instance?.CloseInventory();
        }

        public void SelectSlot(int slot)
        {
            var item = Inventory.Instance != null ? Inventory.Instance.Equipped((ItemSlot)slot) : null;
            selectedId = item != null ? item.id : -1;
            Refresh();
        }

        public void SelectCell(int cell)
        {
            selectedId = cell < ordered.Count ? ordered[cell].id : -1;
            Refresh();
        }

        public void ToggleEquip()
        {
            var inventory = Inventory.Instance;
            var item = inventory != null ? inventory.Find(selectedId) : null;
            if (item == null)
                return;
            if (inventory.IsEquipped(item))
                inventory.Unequip(item.Slot);
            else
                inventory.Equip(item.id);
            AudioService.Instance?.Play("ui");
        }

        public void DiscardSelected()
        {
            var inventory = Inventory.Instance;
            if (inventory == null || inventory.Find(selectedId) == null)
                return;
            inventory.Discard(selectedId);
            selectedId = -1;
            AudioService.Instance?.Play("ui");
            Refresh();
        }

        public void Refresh()
        {
            var inventory = Inventory.Instance;
            if (inventory == null || panel == null || !panel.activeSelf)
                return;

            for (int slot = 0; slotFrames != null && slot < slotFrames.Length; slot++)
            {
                var item = inventory.Equipped((ItemSlot)slot);
                slotFrames[slot].color = item != null ? Rarity(item) : EmptyFrame;
                slotIcons[slot].sprite = inventory.IconFor((ItemSlot)slot);
                slotIcons[slot].color = item != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                slotTexts[slot].text = item != null
                    ? "<b>" + item.Name + "</b> <size=20>Lv " + item.band + "</size>\n<size=20>" + item.Stats + "</size>"
                    : "<b>" + ItemCatalog.SlotLabels[slot] + "</b>\n<size=20><color=#ffffff80>kosong</color></size>";
            }

            ordered.Clear();
            ordered.AddRange(inventory.Items);
            ordered.Sort((a, b) =>
            {
                int bySlot = a.slot.CompareTo(b.slot);
                if (bySlot != 0)
                    return bySlot;
                int byRarity = b.rarity.CompareTo(a.rarity);
                return byRarity != 0 ? byRarity : b.Power.CompareTo(a.Power);
            });
            for (int i = 0; cellButtons != null && i < cellButtons.Length; i++)
            {
                bool filled = i < ordered.Count;
                var item = filled ? ordered[i] : null;
                cellFrames[i].color = filled ? Rarity(item) : EmptyFrame;
                cellIcons[i].enabled = filled;
                if (filled)
                    cellIcons[i].sprite = inventory.IconFor(item.Slot);
                cellMarks[i].text = filled ? (inventory.IsEquipped(item) ? "E " : string.Empty) + "L" + item.band : string.Empty;
                cellButtons[i].interactable = filled;
                cellButtons[i].transform.localScale = filled && item.id == selectedId ? Vector3.one * 1.12f : Vector3.one;
            }
            if (capacityText != null)
                capacityText.text = inventory.Items.Count + " / " + Inventory.Capacity;

            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            if (totalsText != null && stats != null)
            {
                totalsText.text = "Bonus item\n<size=22>ATK +" + UIController.Thousands(stats.gearDamage) + "   HP +" + UIController.Thousands(stats.gearHealth)
                    + "\nDEF +" + (stats.gearDefense * 100f).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)
                    + "%   MOVE +" + (stats.gearSpeed * 100f).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + "%</size>";
            }

            var selected = inventory.Find(selectedId);
            bool has = selected != null;
            equipButton.interactable = has;
            discardButton.interactable = has;
            if (!has)
            {
                detailTitle.text = "Pilih item";
                detailTitle.color = Color.white;
                detailBody.text = "Ketuk item di tas untuk melihat dan memakainya.";
                equipLabel.text = "Pakai";
                return;
            }
            bool worn = inventory.IsEquipped(selected);
            detailTitle.text = UpgradeRarities.Name(selected.Rarity) + "  " + selected.Name + "  <size=22>Lv " + selected.band + "</size>";
            detailTitle.color = Color.Lerp(Rarity(selected), Color.white, 0.35f);
            var current = inventory.Equipped(selected.Slot);
            string compare = worn ? "Sedang dipakai" : current == null ? "Slot " + ItemCatalog.SlotLabels[selected.slot] + " kosong" : "Dipakai: " + current.Name + " (" + current.Stats + ")";
            detailBody.text = selected.Stats + "\n<size=22><color=#ffffffb0>" + compare + "</color></size>";
            equipLabel.text = worn ? "Lepas" : "Pakai";
        }

        static Color Rarity(ItemData item) => UpgradeRarities.Color(item.Rarity);
    }
}
