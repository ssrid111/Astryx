using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AstryxDemo : MonoBehaviour
{
    // UI demonstration only. World collection, oxygen timing and fate effects belong to game systems.
    public event Action<string> FateSelected;
    public event Action PowerRestored;
    public event Action ContinueRequested;
    public event Action ExitRequested;
    public event Action<bool> PauseChanged;
    VisualElement root;
    // Read-only scene bindings: the UI observes existing systems and delegates save/load.
    [Header("Optional backend bindings (auto-detected in the same scene)")]
    [SerializeField] PlayerInventory playerInventory;
    [SerializeField] InventorySystem inventorySystem;
    [SerializeField] RepairPanel repairPanel;
    [SerializeField] ExitDoor exitDoor;
    [SerializeField] PuzzleTimer puzzleTimer;
    [SerializeField] FateEffectController fateEffects;
    [SerializeField] SaveManager saveManager;
    bool backendConnected, roomConnected, completedShown, failureShown;
    float nextRefresh;
    string inventorySnapshot;
    string noticeText, controlHint;
    float noticeUntil;
    bool timerSubscribed, initialPauseSent;

    string screen;
    bool spun;
    float oxygen = 78;
    readonly int[] turns = new int[9];
    readonly string[] glyphs = { "┗", "┏", "┓", "┛" };
    readonly string[] outcomes = { "IMMUNITY", "EXTRA TIME", "HINT", "NEW TOOL", "RESOURCE LOSS", "EQUIPMENT FAILURE", "REDUCED VISION", "HAZARD" };
    // Presentation text mirrors FateEffectController. Outcome selection and effect application stay unchanged.
    readonly string[] details = {
        "Immunity is available to shield the next wrong puzzle input.",
        "Add 30 seconds to the puzzle timer. Oxygen is unchanged.",
        "A hint is available for the current puzzle.",
        "Add a bonus inventory slot, ready to hold an item.",
        "Lock an inventory slot. Locked quick slots are marked LOCK.",
        "Disable the Repair Console for 5 seconds, then restore it.",
        "Dim the screen to 50% visibility for 5 seconds, then restore it.",
        "Remove 15 seconds from the puzzle timer. Oxygen is unchanged."
    };
    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        Application.logMessageReceived += OnBackendLog;
        Bind("start", () => Show(null));
        Bind("pause-open", () => Show("pause")); Bind("resume", () => Show(null));
        Bind("inventory-open", () => { if (backendConnected) RefreshInventory(); Show("inventory"); }); Bind("inventory-close", () => Show(null));
        Bind("fate-open", () => Show("roulette")); Bind("fate-close", () => Show(null));
        Bind("puzzle-open", () => Show("puzzle")); Bind("puzzle-close", () => Show(null));
        Bind("restart", () => Show("confirm")); Bind("cancel-restart", () => Show("pause"));
        Bind("confirm-restart", ResetMission); Bind("retry", ResetMission); Bind("replay", ResetMission);
        Bind("exit", Exit); Bind("gameover-exit", Exit);
        Bind("continue", () => { ContinueRequested?.Invoke(); root.Q<Label>("summary").text = "Connect ContinueRequested to your Level 2 scene loader."; });
        Bind("spin", Spin); Bind("test-circuit", TestCircuit);
        Bind("save-game", () => RunSaveAction(false));
        Bind("load-game", () => RunSaveAction(true));
        foreach (var item in new[] { "tool", "fuse", "cell" })
        {
            string selected = item;
            Bind("inspect-" + item, () => root.Q<Label>("item-detail").text = selected == "tool" ? "REPAIR TOOL • Opens the circuit console." : selected == "fuse" ? "POWER FUSE • Bridges the damaged power supply." : "ENERGY CELL • Supplies power to the circuit.");
        }
        for (int i = 0; i < 5; i++) Bind("slot-" + i, () => { if (backendConnected) RefreshInventory(); Show("inventory"); });
        for (int i = 0; i < 9; i++) { int tile = i; Bind("tile-" + i, () => { turns[tile] = (turns[tile] + 1) % 4; PaintTile(tile); }); }
        root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { Show(screen == null ? "pause" : null); e.StopPropagation(); } });
        root.focusable = true;
        ResetMission();
        root.Q<Button>("save-game").SetEnabled(false);
        root.Q<Button>("load-game").SetEnabled(false);
    }
    void Start()
    {
        ResolveBackend();
        ConfigureBackendView();
        RefreshBackend();
    }
    T FindInScene<T>() where T : Component
    {
        foreach (var candidate in FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (candidate.gameObject.scene == gameObject.scene) return candidate;
        return null;
    }
    void ResolveBackend()
    {
        if (playerInventory == null) playerInventory = FindInScene<PlayerInventory>();
        if (inventorySystem == null) inventorySystem = FindInScene<InventorySystem>();
        if (repairPanel == null) repairPanel = FindInScene<RepairPanel>();
        if (exitDoor == null) exitDoor = FindInScene<ExitDoor>();
        if (puzzleTimer == null) puzzleTimer = FindInScene<PuzzleTimer>();
        if (fateEffects == null) fateEffects = FindInScene<FateEffectController>();
        if (saveManager == null) saveManager = FindInScene<SaveManager>();
        roomConnected = playerInventory != null || repairPanel != null || exitDoor != null;
        backendConnected = roomConnected || inventorySystem != null || puzzleTimer != null || fateEffects != null;
        if (puzzleTimer != null && puzzleTimer.OnTimeExpired != null && !timerSubscribed)
        {
            puzzleTimer.OnTimeExpired.AddListener(OnPuzzleTimeExpired);
            timerSubscribed = true;
        }
    }
    void OnDisable()
    {
        Application.logMessageReceived -= OnBackendLog;
        if (timerSubscribed && puzzleTimer != null && puzzleTimer.OnTimeExpired != null)
            puzzleTimer.OnTimeExpired.RemoveListener(OnPuzzleTimeExpired);
        timerSubscribed = false;
    }
    void ConfigureBackendView()
    {
        root.Q("stage").EnableInClassList("live-world", roomConnected);
        root.Q("stage").EnableInClassList("backend-connected", backendConnected);
        root.Q<Label>("mission-heading").text = backendConnected && !roomConnected ? "FATE CHALLENGE" : "RESTORE THE POWER";
        root.Q("backend-hud").EnableInClassList("hidden", puzzleTimer == null && fateEffects == null);
        root.Q("preview-inventory").EnableInClassList("hidden", backendConnected);
        root.Q("backend-inventory").EnableInClassList("hidden", !backendConnected);
        root.Q<Button>("fate-open").SetEnabled(fateEffects != null || !backendConnected);
        foreach (string id in new[] { "restart", "retry", "replay", "continue" })
            root.Q<Button>(id).SetEnabled(!backendConnected);
        // The room demo repairs in the world. Do not present the sample tile puzzle as its backend.
        if (roomConnected)
        {
            root.Q<Button>("puzzle-open").style.display = DisplayStyle.None;
            root.Q<Label>("intro-description").text = "Find the repair cable, restore the panel, then reach the exit.";
            root.Q<Label>("intro-step-0").text = "01   Collect the repair cable with E";
            root.Q<Label>("intro-step-1").text = "02   Repair the power panel with E";
            root.Q<Label>("intro-step-2").text = "03   Reach the exit and press E";
        }
        else if (backendConnected)
        {
            root.Q<Label>("intro-description").text = "Manage the puzzle timer, inventory slots and Fate Roulette modifiers.";
            root.Q<Label>("intro-step-0").text = "01   Spin Fate Roulette once";
            root.Q<Label>("intro-step-1").text = "02   Check your timer and inventory";
            root.Q<Label>("intro-step-2").text = "03   Review the circuit UI preview";
            root.Q<Button>("puzzle-open").text = "CIRCUIT UI PREVIEW";
        }
        root.Q<Label>("pause-heading").text = backendConnected ? "MISSION MENU" : "UI PREVIEW PAUSED";
        root.Q<Label>("pause-detail").text = fateEffects != null
            ? "Oxygen and puzzle time pause while a menu is open. Timed fate effects still expire."
            : roomConnected ? "Room movement and interactions continue while this menu is open."
            : "Sample UI state only. No gameplay systems are connected.";
        root.Q("backend-banner").EnableInClassList("hidden", !backendConnected);
        controlHint = roomConnected
            ? "WASD Move • E Interact • F5 Save • F9 Load"
            : "Live fate systems • Circuit is a UI preview";
        root.Q<Label>("backend-banner").text = controlHint;
        root.Q<Button>("exit").text = backendConnected ? "MISSION BRIEF" : "EXIT TO TITLE";
        root.Q<Button>("gameover-exit").text = backendConnected ? "MISSION BRIEF" : "EXIT TO TITLE";
        UpdateSaveAvailability();
    }
    void LateUpdate()
    {
        if (!backendConnected || Time.unscaledTime < nextRefresh) return;
        if (!initialPauseSent)
        {
            // All scene Start methods have now run, so initialize existing pause subscribers reliably.
            PauseChanged?.Invoke(screen != null);
            initialPauseSent = true;
        }
        nextRefresh = Time.unscaledTime + 0.1f;
        RefreshBackend();
    }
    void RefreshBackend()
    {
        if (!backendConnected) return;
        root.Q<Label>("backend-banner").text = Time.unscaledTime < noticeUntil ? noticeText : controlHint;
        // FateEffectController adds its original timer label at the document root.
        // Keep that presentation hidden because this UI owns the combined timer/modifier HUD.
        foreach (var child in root.Children())
            if (child is Label legacy && string.IsNullOrEmpty(legacy.name) && legacy.text.StartsWith("PUZZLE  "))
                legacy.style.display = DisplayStyle.None;
        if (fateEffects != null) SetOxygen(fateEffects.Oxygen);
        else root.Q("oxygen-fill").style.width = Length.Percent(0);
        if (fateEffects == null)
        {
            root.Q<Label>("oxygen-label").text = "OXYGEN —";
            root.Q<Label>("oxygen-state").text = "Not tracked in this room";
        }
        UpdateTimerLabel();
        string effect = "No active fate modifier";
        if (fateEffects != null)
        {
            var active = new List<string>();
            if (fateEffects.Immunity) active.Add("Immunity ready");
            if (fateEffects.HintActive) active.Add("Hint available");
            if (fateEffects.EquipmentFailed) active.Add("Repair Console unavailable");
            if (fateEffects.VisibilityMultiplier < 1f) active.Add("Vision " + Mathf.RoundToInt(fateEffects.VisibilityMultiplier * 100f) + "%");
            effect = active.Count > 0 ? string.Join(" • ", active) : string.IsNullOrEmpty(fateEffects.ActiveOutcome)
                ? effect : "Last fate: " + fateEffects.ActiveOutcome;
        }
        root.Q<Label>("backend-effect").text = effect;
        if (roomConnected)
        {
            bool done = exitDoor != null && exitDoor.IsDemoCompleted;
            bool repaired = repairPanel != null && repairPanel.IsRepaired;
            bool cable = playerInventory != null && playerInventory.HasRepairCable;
            SetObjective(done ? "Mission complete • Exit reached" : repaired ? "Power restored • Reach the exit and press E"
                : cable ? "Cable collected • Repair the panel with E" : "Find the repair cable and press E");
            root.Q<Label>("console-prompt").text = done ? "Exit reached" : repaired ? "E • Use the exit when nearby"
                : cable ? "E • Repair the panel when nearby" : "E • Collect the cable when nearby";
            if (done && !completedShown)
            {
                completedShown = true;
                root.Q<Label>("complete-title").text = "Mission complete!";
                root.Q<Label>("complete-detail").text = "Power restored. You reached the exit.";
                root.Q<Label>("summary").text = "Repair cable used • Panel repaired • Exit reached";
                Show("complete");
            }
            else if (!done && completedShown)
            {
                completedShown = false;
                if (screen == "complete") Show(null);
            }
        }
        if (!roomConnected)
            SetObjective("Spin Fate Roulette • Inspect timer and inventory modifiers");
        RefreshInventory();
    }
    void UpdateTimerLabel()
    {
        root.Q<Label>("backend-timer").text = puzzleTimer != null
            ? "PUZZLE " + Mathf.CeilToInt(puzzleTimer.TimeRemaining) + "s" + (puzzleTimer.IsRunning ? "" : " • PAUSED")
            : "PUZZLE TIMER —";
    }
    void RefreshInventory()
    {
        var names = new List<string>(); var locked = new List<bool>();
        if (playerInventory != null)
        {
            names.Add(playerInventory.HasRepairCable ? "Repair cable" : ""); locked.Add(false);
        }
        if (inventorySystem != null)
            foreach (var slot in inventorySystem.Slots) { names.Add(slot.itemName); locked.Add(slot.isLocked); }
        string snapshot = string.Join("|", names) + ":" + string.Join(",", locked);
        // Also refresh quick slots, since the existing effect controller can write them directly.
        int limit = Math.Min(names.Count, 5), used = 0, locks = 0;
        for (int i = 0; i < names.Count; i++) { if (!string.IsNullOrEmpty(names[i])) used++; if (locked[i]) locks++; }
        for (int i = 0; i < 5; i++)
        {
            var button = root.Q<Button>("slot-" + i);
            button.style.display = i < limit ? DisplayStyle.Flex : DisplayStyle.None;
            if (i >= limit) continue;
            button.text = locked[i] ? "LOCK" : string.IsNullOrEmpty(names[i]) ? "—" : names[i];
            button.tooltip = "Slot " + (i + 1) + ": " + (locked[i] ? "Locked" : string.IsNullOrEmpty(names[i]) ? "Empty" : names[i]);
            button.style.opacity = 1f; button.EnableInClassList("locked", locked[i]);
        }
        root.Q<Label>("quick-heading").text = "INVENTORY • " + used + " ITEMS / " + names.Count + " SLOTS";
        root.Q<Label>("inventory-heading").text = used + " ITEMS / " + names.Count + " SLOTS / " + locks + " LOCKED";
        root.Q<Label>("item-detail").text = names.Count > 5 ? "First five slots shown on the HUD. All slots are listed here."
            : roomConnected ? "The repair cable is consumed by repairing the panel."
            : "Slots reflect the current inventory. Bonus slots begin empty.";
        if (snapshot == inventorySnapshot) return;
        inventorySnapshot = snapshot;
        var list = root.Q<ScrollView>("backend-inventory"); list.Clear();
        if (names.Count == 0) list.Add(new Label("No inventory system is available in this scene."));
        for (int i = 0; i < names.Count; i++)
        {
            var row = new VisualElement(); row.AddToClassList("backend-item"); row.EnableInClassList("locked", locked[i]);
            row.Add(new Label("SLOT " + (i + 1) + " • " + (locked[i] ? "LOCKED" : string.IsNullOrEmpty(names[i]) ? "EMPTY" : names[i])));
            var detail = new Label(locked[i] ? "Unavailable: " + (string.IsNullOrEmpty(names[i]) ? "empty slot" : names[i]) + ". Resource-loss modifier active." : string.IsNullOrEmpty(names[i]) ? "Ready to hold an item." : names[i]);
            detail.AddToClassList("small"); row.Add(detail); list.Add(row);
        }
    }
    void UpdateSaveAvailability()
    {
        if (root == null) return;
        root.Q<Button>("save-game").SetEnabled(saveManager != null);
        // Loading a room save restores room state only; it cannot recover expired survival resources.
        root.Q<Button>("retry").text = backendConnected ? "RETRY UNAVAILABLE" : "TRY AGAIN";
        root.Q<Button>("replay").text = backendConnected ? "REPLAY UNAVAILABLE" : "REPLAY";
        root.Q<Button>("continue").text = backendConnected ? "NEXT LEVEL UNAVAILABLE" : "CONTINUE TO LEVEL 2";
        root.Q<Button>("load-game").SetEnabled(saveManager != null && File.Exists(saveManager.SavePath));
        root.Q<Label>("save-status").text = saveManager == null
            ? "Save/load unavailable in this scene. Restart and next-level actions are unavailable."
            : "Save includes room position, cable, panel and exit progress. Oxygen, timer and fate effects are not saved.";
    }
    void OnBackendLog(string message, string trace, LogType type)
    {
        if (root == null || saveManager == null) return;
        string status = null;
        if (message.StartsWith("Partida guardada:")) status = "Room progress saved.";
        else if (message.StartsWith("Partida cargada:")) status = "Room progress loaded.";
        else if (message.StartsWith("No hay partida guardada.")) status = "No saved room progress found.";
        else if (message.StartsWith("No se pudo guardar:")) status = "Unable to save room progress.";
        else if (message.StartsWith("No se pudo cargar.")) status = "Unable to load. Current progress was kept.";
        if (status == null) return;
        // Covers the existing F5/F9 shortcuts as well as the menu buttons.
        noticeText = status; noticeUntil = Time.unscaledTime + 4f;
        root.Q<Label>("save-status").text = status;
        root.Q<Label>("backend-banner").text = status;
    }
    void RunSaveAction(bool load)
    {
        if (saveManager == null) return;
        bool success = false; string warning = null;
        // SaveManager has void commands. Observe its synchronous result rather than claim a request succeeded.
        Application.LogCallback observe = (message, trace, type) =>
        {
            if (message.StartsWith(load ? "Partida cargada:" : "Partida guardada:")) success = true;
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception) warning = message;
        };
        Application.logMessageReceived += observe;
        try { if (load) saveManager.LoadGame(); else saveManager.SaveGame(); }
        finally { Application.logMessageReceived -= observe; }
        UpdateSaveAvailability();
        root.Q<Label>("save-status").text = success ? (load ? "Room progress loaded." : "Room progress saved.")
            : warning != null ? "Unable to " + (load ? "load" : "save") + ". Check the room setup and save file."
            : "No saved room progress was loaded.";
        if (success && load)
        {
            completedShown = false; RefreshBackend();
            if (exitDoor == null || !exitDoor.IsDemoCompleted) Show(null);
        }
    }
    void OnPuzzleTimeExpired()
    {
        if (failureShown) return;
        failureShown = true;
        root.Q<Label>("failure-title").text = "Puzzle time expired";
        root.Q<Label>("failure-detail").text = "The puzzle countdown reached zero. A new attempt needs the gameplay reset flow.";
        Show("gameover");
    }
    void Bind(string name, Action action) { root.Q<Button>(name).clicked += action; }
    public void Show(string name)
    {
        screen = name;
        foreach (string id in new[] { "intro", "inventory", "roulette", "puzzle", "pause", "complete", "gameover", "confirm" }) root.Q(id).EnableInClassList("hidden", id != name);
        // Subscribe to this signal to freeze oxygen, player input and hazards. No global timeScale changes.
        PauseChanged?.Invoke(name != null);
        UpdateTimerLabel();
        if (name == "pause") UpdateSaveAvailability();
        var panel = name == null ? root : root.Q(name);
        var first = panel.Q<Button>(); if (first != null) first.Focus(); else root.Focus();
    }
    public void SetOxygen(float percent)
    {
        oxygen = Mathf.Clamp(percent, 0, 100);
        root.Q<Label>("oxygen-label").text = "OXYGEN  " + Mathf.RoundToInt(oxygen) + "%";
        root.Q("oxygen-fill").style.width = Length.Percent(oxygen);
        root.Q<Label>("oxygen-state").text = oxygen <= 25 ? "LOW OXYGEN" : "STABLE • O₂";
        if (oxygen <= 0 && !failureShown)
        {
            failureShown = true;
            root.Q<Label>("failure-title").text = "Out of oxygen";
            root.Q<Label>("failure-detail").text = backendConnected
                ? "Oxygen has run out. Load a saved room state if one is available; survival resources are not included in that save."
                : "The ship still needs you. Collect both parts before repairing.";
            Show("gameover");
        }
    }
    public void SetObjective(string message) { root.Q<Label>("objective").text = message; }
    void ResetMission()
    {
        spun = false; failureShown = false; SetOxygen(78); SetObjective("Demo: fuse + energy cell collected");
        root.Q<Button>("spin").SetEnabled(true); root.Q<Button>("fate-close").SetEnabled(true);
        root.Q<Label>("result-type").text = "One free spin per mission.";
        root.Q<Label>("result-title").text = "Feeling lucky?";
        root.Q<Label>("result-detail").text = "4 helpful and 4 harmful outcomes. Spin to apply one effect.";
        root.Q("wheel").style.rotate = new Rotate(new Angle(0f));
        root.Q<Button>("fate-close").text = "BACK TO SHIP";
        root.Q<Label>("circuit-status").text = "Join one unbroken path.";
        for (int i = 0; i < 9; i++) { turns[i] = 0; PaintTile(i); }
        Show("intro");
    }
    void Exit()
    {
        ExitRequested?.Invoke();
        if (backendConnected) Show("intro"); else ResetMission();
    }
    int Mask(int i) { return i == 4 ? (turns[i] % 2 == 0 ? 10 : 5) : new[] { 3, 6, 12, 9 }[turns[i]]; }
    void PaintTile(int i) { root.Q<Button>("tile-" + i).text = i == 4 ? (turns[i] % 2 == 0 ? "━" : "┃") : glyphs[turns[i]]; }
    void Spin()
    {
        if (spun) return;
        spun = true; int pick = UnityEngine.Random.Range(0, 8);
        root.Q<Button>("spin").SetEnabled(false);
        root.Q<Label>("result-type").text = pick < 4 ? "HELPFUL / SPIN USED" : "HARMFUL / SPIN USED";
        root.Q<Button>("fate-close").text = "RETURN TO SHIP";
        root.Q<Label>("result-title").text = outcomes[pick];
        root.Q<Label>("result-detail").text = details[pick];
        // Segment 0 is at twelve o'clock; clockwise segment indices match outcomes.
        root.Q("wheel").style.rotate = new Rotate(new Angle(-pick * 45f));
        FateSelected?.Invoke(outcomes[pick]);
    }
    void TestCircuit()
    {
        // Each elbow has two ports: up=1, right=2, down=4, left=8.
        // Glyphs ┗ ┏ ┓ ┛ correspond to up/right, right/down, down/left, left/up.
        int[] dr = { -1, 0, 1, 0 }, dc = { 0, 1, 0, -1 }, bits = { 1, 2, 4, 8 }, opposite = { 4, 8, 1, 2 };
        bool ok = (Mask(0) & 8) != 0 && (Mask(8) & 2) != 0;
        var visited = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(); if (!visited.Add(i)) continue;
            for (int d = 0; d < 4; d++)
            {
                int r = i / 3 + dr[d], c = i % 3 + dc[d]; if (r < 0 || r > 2 || c < 0 || c > 2) continue;
                int j = r * 3 + c;
                if ((Mask(i) & bits[d]) != 0 && (Mask(j) & opposite[d]) != 0) queue.Enqueue(j);
            }
        }
        if (ok && visited.Contains(8))
        {
            SetObjective("Power restored • Exit unlocked");
            root.Q<Label>("summary").text = "Oxygen remaining: " + Mathf.RoundToInt(oxygen) + "% • Circuit repaired ✓";
            PowerRestored?.Invoke(); Show("complete");
        }
        else root.Q<Label>("circuit-status").text = "Open circuit. Check the source, core and tile connections.";
    }
}
