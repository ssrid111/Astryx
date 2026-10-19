using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// InventorySystem
// Author: Hamitha Viyyapu
//
// Slot-based inventory for the Fate Roulette mechanics (RESOURCE LOSS locks a
// slot, NEW TOOL grants a bonus slot), per the Fate Roulette System Logic
// Specification. Separate from the minimal cable-flag demo inventory.
// =============================================================================
public class InventorySystem : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public string itemName = "";    // "" = empty
        public bool   isLocked = false; // RESOURCE LOSS locks a slot
        public bool   IsEmpty => string.IsNullOrEmpty(itemName);
    }

    [SerializeField] private int startingSlotCount = 4;
    [SerializeField] private List<Slot> slots = new List<Slot>();

    public IReadOnlyList<Slot> Slots => slots;
    public int SlotCount => slots.Count;
    public int LockedCount
    {
        get { int c = 0; foreach (var s in slots) if (s.isLocked) c++; return c; }
    }

    private void Awake()
    {
        if (slots.Count == 0)
            for (int i = 0; i < startingSlotCount; i++) slots.Add(new Slot());
    }

    // Place an item in the first empty, unlocked slot.
    public bool AddItem(string itemName)
    {
        foreach (var s in slots)
            if (!s.isLocked && s.IsEmpty)
            {
                s.itemName = itemName;
                Debug.Log($"[INVENTORY] Added: {itemName}");
                return true;
            }
        Debug.Log($"[INVENTORY] No free slot for: {itemName}");
        return false;
    }

    public bool RemoveItem(string itemName)
    {
        foreach (var s in slots)
            if (s.itemName == itemName) { s.itemName = ""; return true; }
        return false;
    }

    // RESOURCE LOSS debuff -> lock the first open slot
    public void LockSlot()
    {
        foreach (var s in slots)
            if (!s.isLocked)
            {
                s.isLocked = true;
                Debug.Log($"[INVENTORY] Slot LOCKED (RESOURCE LOSS). Locked: {LockedCount}/{SlotCount}");
                return;
            }
        Debug.Log("[INVENTORY] No slot available to lock.");
    }

    // Room completion -> undo RESOURCE LOSS
    public void UnlockAllSlots()
    {
        foreach (var s in slots) s.isLocked = false;
        Debug.Log("[INVENTORY] All slots unlocked.");
    }

    // NEW TOOL buff -> add a bonus slot
    public void AddToolSlot()
    {
        slots.Add(new Slot());
        Debug.Log($"[INVENTORY] Bonus tool slot added. Total slots: {SlotCount}");
    }

    [ContextMenu("Test Lock Slot")]   private void TestLock() => LockSlot();
    [ContextMenu("Test Add Tool Slot")] private void TestAddSlot() => AddToolSlot();
}
