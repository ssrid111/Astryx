using UnityEngine;

// =============================================================================
// FateEffectController
// Author: Hamitha Viyyapu
//
// Game-systems side of the Fate Roulette.
// The UI (AstryxDemo) renders the wheel, plays the spin animation, and raises
// the event  FateSelected(string outcomeName)  when the player spins.
// This controller listens to that event and turns each outcome into a real
// gameplay effect, and it owns the oxygen resource that the buffs/debuffs modify.
// =============================================================================
public class FateEffectController : MonoBehaviour
{
    [Header("References (drag in the Inspector)")]
    [SerializeField] private AstryxDemo ui;             // raises FateSelected + shows the HUD
    [SerializeField] private PlayerInventory inventory; // existing demo inventory (repair charge)

    [Header("Oxygen = the mission resource / timer")]
    [SerializeField] private float startingOxygen = 78f;
    [SerializeField] private float drainPerSecond = 1f;
    [SerializeField] private bool  drainOverTime  = true;
    private float oxygen;

    // ---- Active effect state that the puzzle / camera / hazard code can read ----
    public bool  Immunity             { get; private set; }
    public bool  HintActive           { get; private set; }
    public bool  EquipmentFailed      { get; private set; }
    public bool  HazardSpawned        { get; private set; }
    public float VisibilityMultiplier { get; private set; } = 1f;
    public float Oxygen               => oxygen;

    private void Awake()
    {
        oxygen = startingOxygen;
    }

    private void OnEnable()
    {
        if (ui != null) ui.FateSelected += ApplyOutcome;   // subscribe to the UI signal
    }

    private void OnDisable()
    {
        if (ui != null) ui.FateSelected -= ApplyOutcome;   // always unsubscribe to avoid leaks
    }

    private void Update()
    {
        if (!drainOverTime) return;
        oxygen = Mathf.Max(0f, oxygen - drainPerSecond * Time.deltaTime); // oxygen ticks down
        if (ui != null) ui.SetOxygen(oxygen);                             // mirror to the HUD
    }

    // -------------------------------------------------------------------------
    // Called automatically by AstryxDemo when the player spins the wheel.
    // The 8 outcome strings match AstryxDemo.outcomes exactly.
    // -------------------------------------------------------------------------
    private void ApplyOutcome(string outcome)
    {
        Debug.Log($"<color=cyan>[FATE]</color> Outcome received from UI: {outcome}");

        switch (outcome)
        {
            // ---- Helpful (buffs) ----
            case "IMMUNITY":          Immunity = true;              break; // ignore next hazard hit
            case "EXTRA TIME":        AddOxygen(15f);               break; // +15 oxygen
            case "HINT":              HintActive = true;            break; // reveal puzzle hint
            case "NEW TOOL":          GrantRepairCharge();          break; // one repair charge

            // ---- Harmful (debuffs) ----
            case "RESOURCE LOSS":     AddOxygen(-10f);              break; // -10 oxygen
            case "EQUIPMENT FAILURE": EquipmentFailed = true;       break; // tool disabled
            case "REDUCED VISION":    VisibilityMultiplier = 0.5f;  break; // dim the scene
            case "HAZARD":            HazardSpawned = true;         break; // spawn a spark hazard

            default:
                Debug.LogWarning($"[FATE] Unknown outcome string: {outcome}");
                break;
        }

        Debug.Log($"<color=yellow>[FATE] Effect applied:</color> {outcome}");
    }

    private void AddOxygen(float delta)
    {
        oxygen = Mathf.Clamp(oxygen + delta, 0f, 100f);
        if (ui != null) ui.SetOxygen(oxygen);
        Debug.Log($"[FATE] Oxygen {(delta >= 0 ? "+" : "")}{delta:0} -> {oxygen:0}%");
    }

    private void GrantRepairCharge()
    {
        if (inventory != null && inventory.TryCollectRepairCable())
            Debug.Log("[FATE] NEW TOOL: granted a repair charge.");
        else
            Debug.Log("[FATE] NEW TOOL rolled, but no inventory reference (or charge already held).");
    }

    // -------------------------------------------------------------------------
    // Call this when a room / puzzle is completed to reset per-room effects.
    // (Hook it to AstryxDemo.PowerRestored or your room-complete event.)
    // -------------------------------------------------------------------------
    public void ClearEffects()
    {
        Immunity        = false;
        HintActive      = false;
        EquipmentFailed = false;
        HazardSpawned   = false;
        VisibilityMultiplier = 1f;
        Debug.Log("[FATE] Per-room effects cleared.");
    }

    // Quick Inspector test: right-click the component -> "Test Apply Random Outcome"
    [ContextMenu("Test Apply Random Outcome")]
    private void TestApplyRandom()
    {
        string[] all = { "IMMUNITY", "EXTRA TIME", "HINT", "NEW TOOL",
                         "RESOURCE LOSS", "EQUIPMENT FAILURE", "REDUCED VISION", "HAZARD" };
        ApplyOutcome(all[Random.Range(0, all.Length)]);
    }
}
