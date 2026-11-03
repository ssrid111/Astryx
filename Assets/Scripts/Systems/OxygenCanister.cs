using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// OxygenCanister
// Author: Hamitha Viyyapu
//
// Survival resource pickup. Press E nearby to store it in the inventory.
// FateEffectController uses it automatically when oxygen gets low. If the
// inventory is full it is used right away instead of being wasted.
// =============================================================================
[RequireComponent(typeof(SpriteRenderer))]
public class OxygenCanister : MonoBehaviour
{
    [SerializeField] private PlayerInventory      player;     // used for the player's position
    [SerializeField] private InventorySystem      inventory;
    [SerializeField] private FateEffectController fate;
    [SerializeField] private string itemName = "O2 Canister";
    [SerializeField] private float  oxygenAmount = 25f;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;

    [Header("Art (uses the UI oxygen icon if no sprite is set)")]
    [SerializeField] private Texture2D icon;
    [SerializeField] private float worldSize = 0.6f;

    private void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null && icon != null)
            sr.sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height),
                                      new Vector2(0.5f, 0.5f), icon.width / worldSize);
    }

    private void Update()
    {
        if (player == null || !Application.isFocused) return;

        Vector2 offset = (Vector2)player.transform.position - (Vector2)transform.position;
        if (offset.sqrMagnitude > interactionDistance * interactionDistance) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;

        if (inventory != null && inventory.AddItem(itemName))
            Debug.Log("[OXYGEN] Canister stored. It will be used when oxygen is low.");
        else if (fate != null)
            fate.AddOxygen(oxygenAmount);   // no room in the inventory -> use it now

        gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
