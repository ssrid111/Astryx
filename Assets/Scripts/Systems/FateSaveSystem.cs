using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =============================================================================
// FateSaveSystem
// Author: Hamitha Viyyapu
//
// Saves and loads the Fate / survival side of the game, which the demo save
// (SaveManager) does not cover:
//   - oxygen level and the puzzle timer
//   - every inventory slot (items, RESOURCE LOSS locks, NEW TOOL bonus slots)
//   - the last Fate outcome + lasting modifiers (IMMUNITY, HINT, HAZARD)
//   - whether the one free spin was used, room completion, picked-up canisters
//
// Uses the same keys as SaveManager (F5 save / F9 load), so one key press saves
// both files. Written to its own JSON file so the two saves stay independent.
// =============================================================================
[Serializable]
public class FateSaveData
{
    public int    saveVersion;
    public string sceneName;
    public float  oxygen;
    public float  puzzleTimeRemaining;
    public bool   puzzleTimerStopped;
    public InventorySystem.Slot[] inventorySlots;
    public string activeOutcome;
    public bool   spinUsed;
    public bool   immunity;
    public bool   hintActive;
    public bool   hazardActive;
    public bool   roomComplete;
    public bool[] canistersCollected;
}

public class FateSaveSystem : MonoBehaviour
{
    public const int CurrentVersion = 1;
    private const int MaxSlots = 12;

    [SerializeField] private FateEffectController   fate;
    [SerializeField] private PuzzleTimer            puzzleTimer;
    [SerializeField] private InventorySystem        inventory;
    [SerializeField] private RoomProgressController room;
    [SerializeField] private OxygenCanister[]       canisters = new OxygenCanister[0];

    public string SavePath => Path.Combine(Application.persistentDataPath, "astryx-fate-save.json");

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (!Application.isFocused || keyboard == null) return;
        if (keyboard.f5Key.wasPressedThisFrame)      SaveGame();
        else if (keyboard.f9Key.wasPressedThisFrame) LoadGame();
    }

    // ---- snapshot <-> systems ------------------------------------------------

    public FateSaveData Capture()
    {
        var slots = new List<InventorySystem.Slot>();
        if (inventory != null)
            foreach (var s in inventory.Slots)
                slots.Add(new InventorySystem.Slot { itemName = s.itemName, isLocked = s.isLocked });

        var collected = new bool[canisters.Length];
        for (int i = 0; i < canisters.Length; i++)
            collected[i] = canisters[i] != null && !canisters[i].gameObject.activeSelf;

        return new FateSaveData
        {
            saveVersion         = CurrentVersion,
            sceneName           = SceneManager.GetActiveScene().name,
            oxygen              = fate != null ? fate.Oxygen : 0f,
            puzzleTimeRemaining = puzzleTimer != null ? puzzleTimer.TimeRemaining : 0f,
            puzzleTimerStopped  = puzzleTimer != null && puzzleTimer.IsStopped,
            inventorySlots      = slots.ToArray(),
            activeOutcome       = fate != null ? fate.ActiveOutcome ?? "" : "",
            spinUsed            = fate != null && fate.SpinUsed,
            immunity            = fate != null && fate.Immunity,
            hintActive          = fate != null && fate.HintActive,
            hazardActive        = fate != null && fate.HazardActive,
            roomComplete        = room != null && room.RoomComplete,
            canistersCollected  = collected
        };
    }

    // Order matters: the Fate restore clears modifiers (and unlocks slots) first,
    // then the saved inventory slots are put back on top.
    public void Apply(FateSaveData data)
    {
        if (fate != null)
            fate.RestoreState(data.oxygen, data.activeOutcome, data.spinUsed,
                              data.immunity, data.hintActive, data.hazardActive);
        if (inventory != null)   inventory.RestoreSlots(data.inventorySlots);
        if (puzzleTimer != null) puzzleTimer.RestoreState(data.puzzleTimeRemaining, data.puzzleTimerStopped,
                                                          fate != null && fate.IsPaused);
        if (room != null)        room.RestoreState(data.roomComplete);

        for (int i = 0; i < canisters.Length && i < data.canistersCollected.Length; i++)
            if (canisters[i] != null) canisters[i].gameObject.SetActive(!data.canistersCollected[i]);
    }

    // ---- file I/O ------------------------------------------------------------

    public void SaveGame()
    {
        FateSaveData data = Capture();
        try
        {
            Validate(data);
            Directory.CreateDirectory(Application.persistentDataPath);
            string temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Replace(temporaryPath, SavePath, SavePath + ".bak");
            else                       File.Move(temporaryPath, SavePath);
            Debug.Log($"[SAVE] Fate + survival state saved: {SavePath}");
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException)
        {
            Debug.LogWarning($"[SAVE] Could not save Fate state: {error.Message}");
        }
    }

    public void LoadGame()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SAVE] No Fate save yet. Keeping the current game.");
                return;
            }

            FateSaveData data = JsonUtility.FromJson<FateSaveData>(File.ReadAllText(SavePath));
            Validate(data);   // nothing in the scene changes unless the whole file is valid
            if (data.sceneName != SceneManager.GetActiveScene().name)
                throw new ArgumentException("This save belongs to another scene.");

            Apply(data);
            Debug.Log("[SAVE] Fate + survival state loaded: oxygen, timer, inventory, modifiers.");
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is NotSupportedException)
        {
            Debug.LogWarning($"[SAVE] Could not load Fate state. Keeping the current game. {error.Message}");
        }
    }

    // Throws ArgumentException if the data can't be trusted.
    public static void Validate(FateSaveData data)
    {
        if (data == null || data.saveVersion != CurrentVersion)
            throw new ArgumentException("Unknown or old save format.");
        if (data.inventorySlots == null || data.canistersCollected == null)
            throw new ArgumentException("Save is missing inventory or canister data.");
        if (float.IsNaN(data.oxygen) || data.oxygen < 0f || data.oxygen > 100f)
            throw new ArgumentException("Oxygen must be between 0 and 100.");
        if (float.IsNaN(data.puzzleTimeRemaining) || float.IsInfinity(data.puzzleTimeRemaining)
            || data.puzzleTimeRemaining < 0f)
            throw new ArgumentException("Puzzle time can't be negative.");
        if (data.inventorySlots.Length == 0 || data.inventorySlots.Length > MaxSlots)
            throw new ArgumentException($"Inventory must have 1 to {MaxSlots} slots.");
        foreach (var s in data.inventorySlots)
            if (s == null) throw new ArgumentException("Inventory slot is missing.");
        if (!string.IsNullOrEmpty(data.activeOutcome) && !data.spinUsed)
            throw new ArgumentException("A Fate outcome is saved but the spin is not marked as used.");
    }

    [ContextMenu("Test Save")] private void TestSave() => SaveGame();
    [ContextMenu("Test Load")] private void TestLoad() => LoadGame();
}
