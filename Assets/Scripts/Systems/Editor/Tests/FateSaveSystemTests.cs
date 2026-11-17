using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// =============================================================================
// FateSaveSystemTests
// Author: Hamitha Viyyapu
//
// Edit Mode tests for FateSaveSystem: capture, restore, JSON round trip and
// validation of bad save files.
// Run: Window > General > Test Runner > EditMode > Run All
// =============================================================================
public class FateSaveSystemTests
{
    GameObject go;
    InventorySystem inventory;
    PuzzleTimer timer;
    FateEffectController fate;
    RoomProgressController room;
    FateSaveSystem save;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestSave");
        inventory = go.AddComponent<InventorySystem>();
        inventory.EnsureSlots();
        timer = go.AddComponent<PuzzleTimer>();
        timer.OnTimeExpired = new UnityEvent();
        fate = go.AddComponent<FateEffectController>();
        room = go.AddComponent<RoomProgressController>();
        save = go.AddComponent<FateSaveSystem>();

        var so = new SerializedObject(fate);
        so.FindProperty("puzzleTimer").objectReferenceValue = timer;
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.ApplyModifiedPropertiesWithoutUndo();

        so = new SerializedObject(save);
        so.FindProperty("fate").objectReferenceValue = fate;
        so.FindProperty("puzzleTimer").objectReferenceValue = timer;
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.FindProperty("room").objectReferenceValue = room;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(go);

    // Builds a "mid-room" state: some oxygen, a canister, a bonus slot, a lock, a fate outcome.
    void MakeMidRoomState()
    {
        fate.AddOxygen(-fate.Oxygen);
        fate.AddOxygen(64f);
        timer.StartTimer(90f);
        timer.Tick(20f);                     // 70s left
        inventory.AddItem("O2 Canister");
        fate.ApplyOutcome("NEW TOOL");       // 5 slots
        fate.ApplyOutcome("RESOURCE LOSS");  // first slot locked
        fate.ApplyOutcome("IMMUNITY");
    }

    [Test]
    public void Capture_RecordsSurvivalAndFateState()
    {
        MakeMidRoomState();
        FateSaveData data = save.Capture();

        Assert.AreEqual(FateSaveSystem.CurrentVersion, data.saveVersion);
        Assert.AreEqual(64f, data.oxygen, 0.001f);
        Assert.AreEqual(70f, data.puzzleTimeRemaining, 0.001f);
        Assert.AreEqual(5, data.inventorySlots.Length);
        Assert.AreEqual("O2 Canister", data.inventorySlots[0].itemName);
        Assert.IsTrue(data.inventorySlots[0].isLocked);
        Assert.AreEqual("IMMUNITY", data.activeOutcome);
        Assert.IsTrue(data.spinUsed);
        Assert.IsTrue(data.immunity);
    }

    [Test]
    public void Apply_RestoresEverythingAfterStateChanges()
    {
        MakeMidRoomState();
        FateSaveData data = save.Capture();

        // play on: oxygen drops, time passes, canister used, room cleared
        fate.AddOxygen(-40f);
        timer.Tick(30f);
        fate.ClearEffects();
        inventory.UseItem("O2 Canister");

        save.Apply(data);

        Assert.AreEqual(64f, fate.Oxygen, 0.001f);
        Assert.AreEqual(70f, timer.TimeRemaining, 0.001f);
        Assert.AreEqual(5, inventory.SlotCount);
        Assert.IsTrue(inventory.HasItem("O2 Canister"));
        Assert.AreEqual(1, inventory.LockedCount);
        Assert.IsTrue(fate.Immunity);
        Assert.AreEqual("IMMUNITY", fate.ActiveOutcome);
    }

    [Test]
    public void Apply_LockedCanisterStaysUnusable()
    {
        MakeMidRoomState();
        FateSaveData data = save.Capture();
        save.Apply(data);
        Assert.IsFalse(inventory.UseItem("O2 Canister"));   // RESOURCE LOSS lock survives the load
    }

    [Test]
    public void Apply_NeverRefundsTheFreeSpin()
    {
        FateSaveData beforeSpin = save.Capture();
        Assert.IsFalse(beforeSpin.spinUsed);

        fate.ApplyOutcome("HINT");
        save.Apply(beforeSpin);

        Assert.IsTrue(fate.SpinUsed);   // can't reload to re-roll fate
    }

    [Test]
    public void Apply_StoppedTimerStaysStopped()
    {
        timer.StartTimer(60f);
        timer.StopTimer();
        save.Apply(save.Capture());
        timer.Tick(10f);
        Assert.IsFalse(timer.IsRunning);
        Assert.AreEqual(60f, timer.TimeRemaining, 0.001f);
    }

    [Test]
    public void Apply_RoomCompleteIsRestored()
    {
        FateSaveData data = save.Capture();
        data.roomComplete = true;
        save.Apply(data);
        Assert.IsTrue(room.RoomComplete);
    }

    [Test]
    public void Json_RoundTripKeepsData()
    {
        MakeMidRoomState();
        string json = JsonUtility.ToJson(save.Capture());
        FateSaveData loaded = JsonUtility.FromJson<FateSaveData>(json);

        Assert.DoesNotThrow(() => FateSaveSystem.Validate(loaded));
        Assert.AreEqual(5, loaded.inventorySlots.Length);
        Assert.IsTrue(loaded.inventorySlots[0].isLocked);
        Assert.AreEqual("IMMUNITY", loaded.activeOutcome);
    }

    [Test]
    public void Validate_RejectsWrongVersion()
    {
        FateSaveData data = save.Capture();
        data.saveVersion = 99;
        Assert.Throws<ArgumentException>(() => FateSaveSystem.Validate(data));
    }

    [Test]
    public void Validate_RejectsOxygenOutOfRange()
    {
        FateSaveData data = save.Capture();
        data.oxygen = 250f;
        Assert.Throws<ArgumentException>(() => FateSaveSystem.Validate(data));
    }

    [Test]
    public void Validate_RejectsEmptyInventory()
    {
        FateSaveData data = save.Capture();
        data.inventorySlots = new InventorySystem.Slot[0];
        Assert.Throws<ArgumentException>(() => FateSaveSystem.Validate(data));
    }

    [Test]
    public void Validate_RejectsOutcomeWithoutSpin()
    {
        FateSaveData data = save.Capture();
        data.activeOutcome = "EXTRA TIME";
        data.spinUsed = false;
        Assert.Throws<ArgumentException>(() => FateSaveSystem.Validate(data));
    }
}
