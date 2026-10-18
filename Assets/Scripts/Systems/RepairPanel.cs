using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class RepairPanel : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private Sprite repairedSprite;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;

    public bool IsRepaired { get; private set; }

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (IsRepaired || playerInventory == null || !Application.isFocused)
            return;

        Vector2 offset = (Vector2)playerInventory.transform.position - (Vector2)transform.position;
        if (offset.sqrMagnitude > interactionDistance * interactionDistance)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        // Validate the visual setup before consuming the player's item.
        if (repairedSprite == null)
        {
            Debug.LogWarning("Asigna PanelRepaired al campo Repaired Sprite del panel.", this);
            return;
        }

        if (!playerInventory.TryUseRepairCable())
        {
            Debug.Log("Necesitas recoger el cable para reparar el panel.", this);
            return;
        }

        IsRepaired = true;
        spriteRenderer.sprite = repairedSprite;
        Debug.Log("Panel reparado. Energia restaurada.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
