using UnityEngine;

// =============================================================================
// BlobAlienController
// Author: Hamitha Viyyapu
//
// Enemy backend for the dark blob alien. Swetha's BlobThreatUI only shows the
// warning; this script decides the actual threat state and sends it to the UI:
//
//   Clear     - alien hasn't appeared yet or is far away
//   Nearby    - alien is within notice range (shorter under REDUCED VISION)
//   Following - alien is chasing the player
//   Danger    - alien is touching the player -> oxygen drains faster
//
// Fate effects: HAZARD makes the alien faster for the room, IMMUNITY repels
// it once on contact. The alien freezes while any menu is open and retreats
// when the room is complete.
// =============================================================================
[RequireComponent(typeof(SpriteRenderer))]
public class BlobAlienController : MonoBehaviour
{
    [SerializeField] private PlayerInventory      player;   // used for the player's position
    [SerializeField] private FateEffectController fate;
    [SerializeField] private AstryxDemo           ui;
    [SerializeField] private Camera               cam;

    [Header("Room bounds (world units)")]
    [SerializeField] private Vector2 roomMin = new Vector2(-5.5f, -2.8f);
    [SerializeField] private Vector2 roomMax = new Vector2( 5.5f,  3.0f);

    [Header("Senses")]
    [SerializeField] private float noticeRange = 5f;
    [SerializeField] private float followRange = 3.5f;
    [SerializeField] private float dangerRange = 1.2f;
    [SerializeField] private float escapeMargin = 0.6f;   // must get this much farther away to lose a state (stops flicker)
    [SerializeField] private float contactDistance = 0.5f; // stops here instead of sitting on the player

    [Header("Movement")]
    [SerializeField] private float appearDelay           = 15f;   // seconds of gameplay before it shows up
    [SerializeField] private float wanderSpeed           = 0.8f;
    [SerializeField] private float followSpeed           = 1.6f;  // player walks at 3, so it can be outrun
    [SerializeField] private float hazardSpeedMultiplier = 1.5f;
    [SerializeField] private float repelCooldown         = 4f;

    [Header("Damage")]
    [SerializeField] private float dangerOxygenDrain = 2f;        // extra % per second while touching

    private SpriteRenderer sr;
    private BlobThreatUI.ThreatState shownState;
    private bool    shownArrow;
    private Vector2 shownDirection;
    private Vector2 wanderTarget;
    private float   appearTimer;
    private float   cooldown;
    private bool    appeared;
    private bool    retreated;

    public BlobThreatUI.ThreatState State => shownState;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.enabled  = false;              // hidden until it appears
        appearTimer = appearDelay;
        PickWanderTarget();
    }

    private void Update()
    {
        if (retreated || player == null) return;
        if (fate != null && fate.IsPaused) return;   // menus freeze the alien too

        if (!appeared)
        {
            appearTimer -= Time.deltaTime;
            if (appearTimer > 0f) return;
            appeared   = true;
            sr.enabled = true;
            Debug.Log("[ALIEN] The blob alien has appeared.");
        }

        if (cooldown > 0f) cooldown -= Time.deltaTime;

        Vector2 pos       = transform.position;
        Vector2 playerPos = player.transform.position;
        float   dist      = Vector2.Distance(pos, playerPos);
        float   vision    = fate != null ? fate.VisibilityMultiplier : 1f;
        bool    wasChasing = shownState == BlobThreatUI.ThreatState.Following || shownState == BlobThreatUI.ThreatState.Danger;
        bool    wasDanger  = shownState == BlobThreatUI.ThreatState.Danger;
        bool    chasing    = cooldown <= 0f && dist <= followRange + (wasChasing ? escapeMargin : 0f);

        var state = BlobThreatUI.ThreatState.Clear;
        if (chasing && dist <= dangerRange + (wasDanger ? escapeMargin : 0f)) state = BlobThreatUI.ThreatState.Danger;
        else if (chasing)                     state = BlobThreatUI.ThreatState.Following;
        else if (dist <= noticeRange * vision) state = BlobThreatUI.ThreatState.Nearby;

        // IMMUNITY repels the alien once instead of taking damage.
        if (state == BlobThreatUI.ThreatState.Danger && fate != null && fate.ConsumeImmunity())
        {
            Repel(playerPos);
            return;
        }

        // Move: chase the player or wander around the room.
        float speed = chasing ? followSpeed : wanderSpeed;
        if (fate != null && fate.HazardActive) speed *= hazardSpeedMultiplier;
        Vector2 away   = dist > 0.001f ? (pos - playerPos) / dist : Vector2.up;
        Vector2 target = chasing ? playerPos + away * contactDistance : wanderTarget;
        pos = Vector2.MoveTowards(pos, target, speed * Time.deltaTime);
        pos.x = Mathf.Clamp(pos.x, roomMin.x, roomMax.x);
        pos.y = Mathf.Clamp(pos.y, roomMin.y, roomMax.y);
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
        if (!chasing && Vector2.Distance(pos, wanderTarget) < 0.1f) PickWanderTarget();

        if (fate != null)
            fate.SetExtraOxygenDrain(state == BlobThreatUI.ThreatState.Danger ? dangerOxygenDrain : 0f);

        SendWarning(state, (pos - playerPos).normalized);
    }

    // Only talks to the UI when something actually changed.
    private void SendWarning(BlobThreatUI.ThreatState state, Vector2 direction)
    {
        bool arrow = state != BlobThreatUI.ThreatState.Clear && IsOffScreen();
        bool changed = state != shownState || arrow != shownArrow
                       || (arrow && Vector2.Angle(direction, shownDirection) > 5f);
        if (!changed) return;

        if (state != shownState) Debug.Log($"[ALIEN] Threat: {shownState} -> {state}");
        shownState = state; shownArrow = arrow; shownDirection = direction;
        if (ui != null) ui.SetAlienThreat(state, direction, arrow);
    }

    private bool IsOffScreen()
    {
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        return v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f;
    }

    // Jump to the room corner farthest from the player and stop chasing for a bit.
    private void Repel(Vector2 playerPos)
    {
        Vector2[] corners = { roomMin, roomMax, new Vector2(roomMin.x, roomMax.y), new Vector2(roomMax.x, roomMin.y) };
        Vector2 best = corners[0];
        foreach (var c in corners)
            if (Vector2.Distance(c, playerPos) > Vector2.Distance(best, playerPos)) best = c;

        transform.position = new Vector3(best.x, best.y, transform.position.z);
        cooldown = repelCooldown;
        if (fate != null) fate.SetExtraOxygenDrain(0f);
        SendWarning(BlobThreatUI.ThreatState.Clear, Vector2.zero);
        Debug.Log("[ALIEN] IMMUNITY repelled the alien (immunity used up).");
    }

    // Room complete -> alien leaves and the warning clears.
    public void Retreat()
    {
        retreated = true;
        if (fate != null) fate.SetExtraOxygenDrain(0f);
        SendWarning(BlobThreatUI.ThreatState.Clear, Vector2.zero);
        gameObject.SetActive(false);
        Debug.Log("[ALIEN] Power restored -> the alien retreats.");
    }

    private void PickWanderTarget()
    {
        wanderTarget = new Vector2(Random.Range(roomMin.x, roomMax.x), Random.Range(roomMin.y, roomMax.y));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, noticeRange);
        Gizmos.color = new Color(1f, 0.5f, 0f); Gizmos.DrawWireSphere(transform.position, followRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, dangerRange);
    }

    [ContextMenu("Test Appear Now")]
    private void TestAppearNow() => appearTimer = 0f;
}
