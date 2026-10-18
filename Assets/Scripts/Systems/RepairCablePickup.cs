using UnityEngine;
using UnityEngine.InputSystem;

public class RepairCablePickup : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.5f;

    private bool collected;

    public bool IsCollected => collected;

    public void RestoreState(bool wasCollected)
    {
        collected = wasCollected;
        gameObject.SetActive(!collected);
    }

    private bool CanInteract()
    {
        if (collected || playerInventory == null || playerInventory.HasRepairCable)
            return false;

        Vector2 offset = (Vector2)playerInventory.transform.position - (Vector2)transform.position;
        return offset.sqrMagnitude <= interactionDistance * interactionDistance;
    }

    private void Update()
    {
        if (!Application.isFocused || !CanInteract())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame
            && playerInventory.TryCollectRepairCable())
        {
            collected = true;
            gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
