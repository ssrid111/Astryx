using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// =============================================================================
// FateEffectController
// Author: Hamitha Viyyapu
//
// Game-systems side of the Fate Roulette. The UI (AstryxDemo) renders the wheel
// and raises FateSelected(outcomeName) on spin. This controller listens and
// applies each of the eight outcomes as a REAL, functional effect:
//
//   EXTRA TIME        - +30s to the live puzzle timer (shown on the HUD)
//   HAZARD            - -15s from the live puzzle timer
//   RESOURCE LOSS     - locks one inventory slot (slot shows LOCK, dimmed)
//   NEW TOOL          - fills a bonus inventory slot
//   REDUCED VISION    - dims the screen to 50% for 5s, then restores
//   EQUIPMENT FAILURE - disables the Repair Console button for 5s, then restores
//   IMMUNITY / HINT   - set state the puzzle system will read (no puzzle yet)
//
// Oxygen is a separate ambient survival resource; the roulette does not touch it.
// =============================================================================
public class FateEffectController : MonoBehaviour
{
    [Header("UI + systems (drag in the Inspector)")]
    [SerializeField] private AstryxDemo      ui;
    [SerializeField] private PuzzleTimer     puzzleTimer;
    [SerializeField] private InventorySystem inventory;

    [Header("Oxygen = ambient survival resource")]
    [SerializeField] private float startingOxygen       = 78f;
    [SerializeField] private float oxygenDrainPerSecond = 1f;
    [SerializeField] private bool  drainOxygen          = true;
    private float oxygen;
    private bool  paused;   // true while a UI menu/overlay (roulette, inventory, pause) is open

    [Header("Fate outcome values (from the spec)")]
    [SerializeField] private float extraTimeSeconds       = 30f;
    [SerializeField] private float hazardSeconds          = 15f;
    [SerializeField] private float reducedVisionMul       = 0.5f;
    [SerializeField] private float reducedVisionSeconds   = 5f;
    [SerializeField] private float equipmentFailureSeconds = 5f;

    // ---- active modifier state other systems can read ----
    public bool   Immunity             { get; private set; }
    public bool   HintActive           { get; private set; }
    public bool   EquipmentFailed      { get; private set; }
    public float  VisibilityMultiplier { get; private set; } = 1f;
    public string ActiveOutcome        { get; private set; }
    public float  Oxygen               => oxygen;

    // ---- UI handles (UI Toolkit) ----
    private VisualElement root;
    private VisualElement dimOverlay;
    private Label timerLabel;
    private readonly Dictionary<string, string> lockedSlots = new Dictionary<string, string>();

    private VisualElement Root
    {
        get
        {
            if (root == null)
            {
                var doc = GetComponent<UIDocument>();
                if (doc != null) root = doc.rootVisualElement;
            }
            return root;
        }
    }

    private void Awake() => oxygen = startingOxygen;

    private void Start()
    {
        if (puzzleTimer != null) puzzleTimer.StartTimer();   // start the live countdown
    }

    private void OnEnable()
    {
        if (ui != null) { ui.FateSelected += ApplyOutcome; ui.PauseChanged += OnPauseChanged; }
    }

    private void OnDisable()
    {
        if (ui != null) { ui.FateSelected -= ApplyOutcome; ui.PauseChanged -= OnPauseChanged; }
    }

    // UI raises this when any menu/overlay opens (true) or the game resumes (false).
    private void OnPauseChanged(bool isPaused)
    {
        paused = isPaused;
        if (puzzleTimer != null)
        {
            if (isPaused) puzzleTimer.Pause();
            else          puzzleTimer.Resume();
        }
        Debug.Log(isPaused
            ? "[FATE] Menu open -> puzzle timer & oxygen frozen."
            : "[FATE] Resumed -> puzzle timer & oxygen running.");
    }

    private void Update()
    {
        if (drainOxygen && !paused)   // oxygen freezes while a menu is open
        {
            oxygen = Mathf.Max(0f, oxygen - oxygenDrainPerSecond * Time.deltaTime);
            if (ui != null) ui.SetOxygen(oxygen);
        }

        // live puzzle-timer readout on the HUD
        EnsureTimerLabel();
        if (timerLabel != null && puzzleTimer != null)
            timerLabel.text = $"PUZZLE  {puzzleTimer.TimeRemaining:0}s";
    }

    // Called automatically when the player spins the wheel in the UI.
    private void ApplyOutcome(string outcome)
    {
        ActiveOutcome = outcome;
        Debug.Log($"<color=cyan>[FATE]</color> Outcome received from UI: {outcome}");

        switch (outcome)
        {
            // ---- Helpful (buffs) ----
            case "IMMUNITY":
                Immunity = true;
                Announce("IMMUNITY active - next wrong input shielded");  // state only (needs puzzle)
                break;
            case "EXTRA TIME":
                if (puzzleTimer != null) puzzleTimer.AddTime(extraTimeSeconds);   // real: timer jumps +30
                break;
            case "HINT":
                HintActive = true;
                Announce("HINT unlocked for this puzzle");                // state only (needs puzzle)
                break;
            case "NEW TOOL":
                if (inventory != null) inventory.AddToolSlot();
                UIFillOneSlot("NEW");                                     // real: fills a slot
                break;

            // ---- Harmful (debuffs) ----
            case "RESOURCE LOSS":
                if (inventory != null) inventory.LockSlot();
                UILockOneResource();                                     // real: locks a slot
                break;
            case "EQUIPMENT FAILURE":
                StartCoroutine(EquipmentFailureRoutine());               // real: disables repair button 5s
                break;
            case "REDUCED VISION":
                StartCoroutine(ReducedVisionRoutine());                  // real: dims screen 5s
                break;
            case "HAZARD":
                if (puzzleTimer != null) puzzleTimer.RemoveTime(hazardSeconds);  // real: timer drops -15
                break;

            default:
                Debug.LogWarning($"[FATE] Unknown outcome string: {outcome}");
                break;
        }

        Debug.Log($"<color=yellow>[FATE] Effect applied:</color> {outcome}");
    }

    // ---- timed debuffs -------------------------------------------------------

    private IEnumerator EquipmentFailureRoutine()
    {
        EquipmentFailed = true;
        var btn = Root != null ? Root.Q<Button>("puzzle-open") : null;   // the REPAIR CONSOLE button
        if (btn != null) btn.SetEnabled(false);
        Debug.Log($"[FATE] EQUIPMENT FAILURE -> Repair Console disabled for {equipmentFailureSeconds:0}s.");
        yield return new WaitForSeconds(equipmentFailureSeconds);
        if (btn != null) btn.SetEnabled(true);
        EquipmentFailed = false;
        Debug.Log("[FATE] EQUIPMENT FAILURE expired -> Repair Console re-enabled.");
    }

    private IEnumerator ReducedVisionRoutine()
    {
        VisibilityMultiplier = reducedVisionMul;
        UIDimScreen(true);
        Debug.Log($"[FATE] REDUCED VISION -> screen dimmed for {reducedVisionSeconds:0}s.");
        yield return new WaitForSeconds(reducedVisionSeconds);
        UIDimScreen(false);
        VisibilityMultiplier = 1f;
        Debug.Log("[FATE] REDUCED VISION expired -> visibility restored.");
    }

    // ---- UI reflection helpers ----------------------------------------------

    private void Announce(string message)
    {
        if (ui != null) ui.SetObjective(message);
    }

    private void EnsureTimerLabel()
    {
        if (timerLabel != null || Root == null) return;
        timerLabel = new Label("PUZZLE  --");
        var s = timerLabel.style;
        s.position = Position.Absolute;
        s.top = 36; s.left = Length.Percent(43);
        s.paddingLeft = 18; s.paddingRight = 18; s.paddingTop = 8; s.paddingBottom = 8;
        s.backgroundColor = new Color(1f, 0.956f, 0.82f);     // cream
        s.color = new Color(0.141f, 0.137f, 0.294f);          // ink
        s.borderTopLeftRadius = 14; s.borderTopRightRadius = 14;
        s.borderBottomLeftRadius = 14; s.borderBottomRightRadius = 14;
        s.unityFontStyleAndWeight = FontStyle.Bold;
        s.fontSize = 22;
        Root.Add(timerLabel);
    }

    // RESOURCE LOSS: dim + relabel the right-most filled inventory slot.
    private void UILockOneResource()
    {
        if (Root == null) return;
        for (int i = 4; i >= 0; i--)
        {
            string key = "slot-" + i;
            var slot = Root.Q<Button>(key);
            if (slot != null && slot.text != "—" && slot.text != "LOCK" && !lockedSlots.ContainsKey(key))
            {
                lockedSlots[key] = slot.text;
                slot.text = "LOCK";
                slot.style.opacity = 0.35f;
                Debug.Log($"[FATE/UI] Locked {key} (was {lockedSlots[key]}).");
                return;
            }
        }
        Debug.Log("[FATE/UI] No filled slot available to lock.");
    }

    // NEW TOOL: fill the left-most empty slot.
    private void UIFillOneSlot(string label)
    {
        if (Root == null) return;
        for (int i = 0; i <= 4; i++)
        {
            var slot = Root.Q<Button>("slot-" + i);
            if (slot != null && slot.text == "—")
            {
                slot.text = label;
                slot.style.opacity = 1f;
                Debug.Log($"[FATE/UI] Filled slot-{i} with {label}.");
                return;
            }
        }
        Debug.Log("[FATE/UI] No empty slot to fill.");
    }

    // REDUCED VISION: translucent black overlay across the whole UI.
    private void UIDimScreen(bool dim)
    {
        if (Root == null) return;
        if (dim)
        {
            if (dimOverlay == null)
            {
                dimOverlay = new VisualElement();
                dimOverlay.style.position = Position.Absolute;
                dimOverlay.style.left = 0; dimOverlay.style.right = 0;
                dimOverlay.style.top = 0;  dimOverlay.style.bottom = 0;
                dimOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
                dimOverlay.pickingMode = PickingMode.Ignore;
                Root.Add(dimOverlay);
            }
            dimOverlay.style.display = DisplayStyle.Flex;
        }
        else if (dimOverlay != null)
        {
            dimOverlay.style.display = DisplayStyle.None;
        }
    }

    // ---- Room completion: clear per-room modifiers (spec: ClearModifier) -----
    public void ClearEffects()
    {
        Immunity        = false;
        HintActive      = false;
        EquipmentFailed = false;
        VisibilityMultiplier = 1f;
        ActiveOutcome   = null;

        if (inventory != null) inventory.UnlockAllSlots();

        if (Root != null)
        {
            foreach (var kv in lockedSlots)
            {
                var slot = Root.Q<Button>(kv.Key);
                if (slot != null) { slot.text = kv.Value; slot.style.opacity = 1f; }
            }
            var btn = Root.Q<Button>("puzzle-open");
            if (btn != null) btn.SetEnabled(true);
        }
        lockedSlots.Clear();
        UIDimScreen(false);

        Debug.Log("[FATE] Modifiers cleared for the next room.");
    }

    // Quick Inspector test (Play mode): right-click -> "Test Apply Random Outcome"
    [ContextMenu("Test Apply Random Outcome")]
    private void TestApplyRandom()
    {
        string[] all = { "IMMUNITY", "EXTRA TIME", "HINT", "NEW TOOL",
                         "RESOURCE LOSS", "EQUIPMENT FAILURE", "REDUCED VISION", "HAZARD" };
        ApplyOutcome(all[Random.Range(0, all.Length)]);
    }
}
