using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// =============================================================================
// BackendSystemsTests
// Author: Hamitha Viyyapu
//
// Edit Mode tests for the Fate Roulette backend: inventory, puzzle timer,
// Fate outcomes and oxygen.
// Run: Window > General > Test Runner > EditMode > Run All
// =============================================================================
public class BackendSystemsTests
{
    GameObject go;
    InventorySystem inventory;
    PuzzleTimer timer;
    FateEffectController fate;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestSystems");
        inventory = go.AddComponent<InventorySystem>();
        inventory.EnsureSlots();
        timer = go.AddComponent<PuzzleTimer>();
        timer.OnTimeExpired = new UnityEvent();
        fate = go.AddComponent<FateEffectController>();

        var so = new SerializedObject(fate);
        so.FindProperty("puzzleTimer").objectReferenceValue = timer;
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    // ---- InventorySystem ----

    [Test]
    public void Inventory_StartsWithFourEmptySlots()
    {
        Assert.AreEqual(4, inventory.SlotCount);
        foreach (var slot in inventory.Slots) Assert.IsTrue(slot.IsEmpty);
    }

    [Test]
    public void Inventory_AddItem_FillsFirstFreeSlot()
    {
        Assert.IsTrue(inventory.AddItem("O2 Canister"));
        Assert.AreEqual("O2 Canister", inventory.Slots[0].itemName);
    }

    [Test]
    public void Inventory_AddItem_FailsWhenFull()
    {
        for (int i = 0; i < 4; i++) inventory.AddItem("Item" + i);
        Assert.IsFalse(inventory.AddItem("Extra"));
    }

    [Test]
    public void Inventory_UseItem_RemovesItem()
    {
        inventory.AddItem("O2 Canister");
        Assert.IsTrue(inventory.UseItem("O2 Canister"));
        Assert.IsFalse(inventory.HasItem("O2 Canister"));
    }

    [Test]
    public void Inventory_LockedSlot_ItemCannotBeUsed()
    {
        inventory.AddItem("O2 Canister");   // goes into slot 0
        inventory.LockSlot();               // RESOURCE LOSS locks slot 0
        Assert.IsFalse(inventory.UseItem("O2 Canister"));
        Assert.IsTrue(inventory.HasItem("O2 Canister"));
    }

    [Test]
    public void Inventory_UnlockAllSlots_ClearsLocks()
    {
        inventory.LockSlot(); inventory.LockSlot();
        Assert.AreEqual(2, inventory.LockedCount);
        inventory.UnlockAllSlots();
        Assert.AreEqual(0, inventory.LockedCount);
    }

    [Test]
    public void Inventory_AddToolSlot_AddsBonusSlot()
    {
        inventory.AddToolSlot();
        Assert.AreEqual(5, inventory.SlotCount);
    }

    // ---- PuzzleTimer ----

    [Test]
    public void Timer_Tick_CountsDown()
    {
        timer.StartTimer(60f);
        timer.Tick(10f);
        Assert.AreEqual(50f, timer.TimeRemaining, 0.001f);
    }

    [Test]
    public void Timer_Expires_InvokesEventOnce()
    {
        int calls = 0;
        timer.OnTimeExpired.AddListener(() => calls++);
        timer.StartTimer(5f);
        timer.Tick(6f);
        timer.Tick(1f);
        Assert.AreEqual(0f, timer.TimeRemaining);
        Assert.IsFalse(timer.IsRunning);
        Assert.AreEqual(1, calls);
    }

    [Test]
    public void Timer_Paused_DoesNotCountDown()
    {
        timer.StartTimer(60f);
        timer.Pause();
        timer.Tick(10f);
        Assert.AreEqual(60f, timer.TimeRemaining, 0.001f);
        timer.Resume();
        Assert.IsTrue(timer.IsRunning);
    }

    [Test]
    public void Timer_Stopped_StaysStoppedAfterResume()
    {
        timer.StartTimer(60f);
        timer.StopTimer();   // room complete
        timer.Resume();      // a menu closes
        Assert.IsFalse(timer.IsRunning);
    }

    [Test]
    public void Timer_RemoveTime_NeverGoesBelowZero()
    {
        timer.StartTimer(10f);
        timer.RemoveTime(15f);
        Assert.AreEqual(0f, timer.TimeRemaining);
    }

    // ---- FateEffectController ----

    [Test]
    public void Fate_ExtraTime_AddsThirtySeconds()
    {
        timer.StartTimer(60f);
        fate.ApplyOutcome("EXTRA TIME");
        Assert.AreEqual(90f, timer.TimeRemaining, 0.001f);
    }

    [Test]
    public void Fate_Hazard_RemovesFifteenSecondsAndSpeedsUpAlien()
    {
        timer.StartTimer(60f);
        fate.ApplyOutcome("HAZARD");
        Assert.AreEqual(45f, timer.TimeRemaining, 0.001f);
        Assert.IsTrue(fate.HazardActive);
    }

    [Test]
    public void Fate_ResourceLoss_LocksASlot()
    {
        fate.ApplyOutcome("RESOURCE LOSS");
        Assert.AreEqual(1, inventory.LockedCount);
    }

    [Test]
    public void Fate_NewTool_AddsASlot()
    {
        fate.ApplyOutcome("NEW TOOL");
        Assert.AreEqual(5, inventory.SlotCount);
    }

    [Test]
    public void Fate_Immunity_CanBeUsedOnlyOnce()
    {
        fate.ApplyOutcome("IMMUNITY");
        Assert.IsTrue(fate.Immunity);
        Assert.IsTrue(fate.ConsumeImmunity());    // alien repelled
        Assert.IsFalse(fate.Immunity);
        Assert.IsFalse(fate.ConsumeImmunity());   // nothing left
    }

    [Test]
    public void Fate_NoImmunity_NothingToConsume()
    {
        Assert.IsFalse(fate.ConsumeImmunity());
    }

    [Test]
    public void Fate_ClearEffects_ResetsModifiers()
    {
        fate.ApplyOutcome("IMMUNITY");
        fate.ApplyOutcome("HINT");
        fate.ApplyOutcome("RESOURCE LOSS");
        fate.ClearEffects();
        Assert.IsFalse(fate.Immunity);
        Assert.IsFalse(fate.HintActive);
        Assert.IsFalse(fate.HazardActive);
        Assert.AreEqual(0, inventory.LockedCount);
    }

    [Test]
    public void Oxygen_AddOxygen_CapsAtHundred()
    {
        fate.AddOxygen(150f);
        Assert.AreEqual(100f, fate.Oxygen, 0.001f);
    }
}
