using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// =============================================================================
// FateEffectController
// Author: Hamitha Viyyapu
//
// Game-systems side of the Fate Roulette. The UI (AstryxDemo) renders the wheel
// and raises FateSelected(outcomeName) on spin. This controller listens and
// applies each of the eight outcomes per the Fate Roulette System Logic
// Specification, AND reflects each effect visibly in the UI Toolkit interface.
//
//   Buffs:   IMMUNITY    - shield the next wrong puzzle input
//            EXTRA TIME  - +30s to the PUZZLE timer (shown on objective line)
//            HINT        - reveal a puzzle clue
//            NEW TOOL    - fill a bonus inventory slot
//   Debuffs: RESOURCE LOSS     - lock one inventory slot (slot shows LOCK, dimmed)
//            EQUIPMENT FAILURE - temporarily disable the repair tool
//            REDUCED VISION    - dim the screen to 50%
//            HAZARD            - -15s from the PUZZLE timer
//
// Oxygen is a separate ambient survival resource; the roulette does not touch it.
// =============================================================================
public class FateEffectController : MonoBehaviour
{
    [Header("UI + systems (drag in the Inspector)")]
    [SerializeField] private AstryxDemo      ui;           // raises FateSelected + HUD
    [SerializeField] private PuzzleTimer     puzzleTimer;  // EXTRA TIME / HAZARD target
    [SerializeField] private InventorySystem inventory;    // RESOURCE LOSS / NEW TOOL target

    [Header("Oxygen = ambient survival resource (separate from the puzzle timer)")]
    [SerializeField] private float startingOxygen       = 78f;
    [SerializeField] private float oxygenDrainPerSecond = 1f;
    [SerializeField] private bool  drainOxygen          = true;
    private float oxygen;

    [Header("Fate outcome values (from the spec)")]
    [SerializeField] private float extraTimeSeconds = 30f;  // XTRA TIME -> +30s
    [SerializeField] private float hazardSeconds    = 15f;  // HAZARD    -> -15s
    [SerializeField] private float reducedVisionMul = 0.5f; // RED VIS   -> 50% visibility

    // ---- active modifier state the puzzle / camera / tool systems can read ----
    public bool   Immunity             { get; private set; }
    public bool   HintActive           { get; private set; }
    public bool   EquipmentFailed      { get; private set; }
    public float  VisibilityMultiplier { get; private set; } = 1f;
    public string ActiveOutcome        { get; private set; }
    public float  Oxygen               => oxygen;

    // ---- UI handles (UI Toolkit) ----
    private VisualElement root;
    private VisualElement dimOverlay;
    private readonly Dictionary<string, string> lockedSlots = new Dictionary<string, string>(); // slotName -> original text

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

    private void OnEnable()  { if (ui != null) ui.FateSelected += ApplyOutcome; }
    private void OnDisable() { if (ui != null) ui.FateSelected -= ApplyOutcome; }

    private void Update()
    {
        if (!drainOxygen) return;
        oxygen = Mathf.Max(0f, oxygen - oxygenDrainPerSecond * Time.deltaTime);
        if (ui != null) ui.SetOxygen(oxygen);
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
                Announce("IMMUNITY active - next wrong input shielded");
                break;
            case "EXTRA TIME":
                if (puzzleTimer != null) puzzleTimer.AddTime(extraTimeSeconds);
                Announce($"EXTRA TIME  +{extraTimeSeconds:0}s puzzle time");
                break;
            case "HINT":
                HintActive = true;
                Announce("HINT unlocked for this puzzle");
                break;
            case "NEW TOOL":
                if (inventory != null) inventory.AddToolSlot();
                UIFillOneSlot("NEW");
                Announce("NEW TOOL added to inventory");
                break;

            // ---- Harmful (debuffs) ----
            case "RESOURCE LOSS":
                if (inventory != null) inventory.LockSlot();
                UILockOneResource();
                Announce("RESOURCE LOSS - an inventory slot was locked");
                break;
            case "EQUIPMENT FAILURE":
                EquipmentFailed = true;
                Announce("EQUIPMENT FAILURE - repair tool disabled");
                break;
            case "REDUCED VISION":
                VisibilityMultiplier = reducedVisionMul;
                UIDimScreen(true);
                Announce("REDUCED VISION - visibility at 50%");
                break;
            case "HAZARD":
                if (puzzleTimer != null) puzzleTimer.RemoveTime(hazardSeconds);
                Announce($"HAZARD  -{hazardSeconds:0}s puzzle time");
                break;

            default:
                Debug.LogWarning($"[FATE] Unknown outcome string: {outcome}");
                break;
        }

        Debug.Log($"<color=yellow>[FATE] Effect applied:</color> {outcome}");
    }

    // ---- UI reflection helpers -------------------------------------------------

    private void Announce(string message)
    {
        if (ui != null) ui.SetObjective(message);
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
                lockedSlots[key] = slot.text;     // remember original (e.g. "CELL")
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
                dimOverlay.pickingMode = PickingMode.Ignore; // don't block clicks
                Root.Add(dimOverlay);
            }
            dimOverlay.style.display = DisplayStyle.Flex;
        }
        else if (dimOverlay != null)
        {
            dimOverlay.style.display = DisplayStyle.None;
        }
    }

    // ---- Room completion: clear per-room modifiers (spec: ClearModifier) -------
    public void ClearEffects()
    {
        Immunity        = false;
        HintActive      = false;
        EquipmentFailed = false;
        VisibilityMultiplier = 1f;
        ActiveOutcome   = null;

        if (inventory != null) inventory.UnlockAllSlots();

        // restore any locked UI slots
        if (Root != null)
        {
            foreach (var kv in lockedSlots)
            {
                var slot = Root.Q<Button>(kv.Key);
                if (slot != null) { slot.text = kv.Value; slot.style.opacity = 1f; }
            }
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
