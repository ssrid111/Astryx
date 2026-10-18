using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public class ExitDoor : MonoBehaviour
{
    [SerializeField] private RepairPanel repairPanel;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private Sprite openSprite;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;

    public bool IsOpen { get; private set; }
    public bool IsDemoCompleted { get; private set; }

    private SpriteRenderer spriteRenderer;
    private Sprite closedSprite;

    public bool CanRestore => openSprite != null;

    public void RestoreState(bool completed)
    {
        IsDemoCompleted = completed;
        IsOpen = repairPanel.IsRepaired;
        spriteRenderer.sprite = IsOpen ? openSprite : closedSprite;
        PlayerMovement movement = playerInventory.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = !completed;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        closedSprite = spriteRenderer.sprite;
    }

    private void Update()
    {
        if (IsDemoCompleted || repairPanel == null || playerInventory == null)
            return;

        if (!IsOpen && repairPanel.IsRepaired && openSprite != null)
        {
            IsOpen = true;
            spriteRenderer.sprite = openSprite;
            Debug.Log("Salida desbloqueada. Acercate a la puerta y pulsa E.", this);
        }

        if (!Application.isFocused)
            return;

        Vector2 offset = (Vector2)playerInventory.transform.position - (Vector2)transform.position;
        if (offset.sqrMagnitude > interactionDistance * interactionDistance)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        if (!repairPanel.IsRepaired)
        {
            Debug.Log("La salida necesita energia. Repara el panel primero.", this);
            return;
        }

        if (openSprite == null)
        {
            Debug.LogWarning("Asigna DoorOpen al campo Open Sprite de ExitDoor.", this);
            return;
        }

        IsDemoCompleted = true;
        PlayerMovement movement = playerInventory.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        Debug.Log("Demo completada. Has restaurado la energia y alcanzado la salida.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
