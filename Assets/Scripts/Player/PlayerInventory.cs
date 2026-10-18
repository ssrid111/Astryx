using UnityEngine;

// Minimal demo inventory. Later, save/load can capture HasRepairCable.
public class PlayerInventory : MonoBehaviour
{
    public bool HasRepairCable { get; private set; }

    public void RestoreState(bool hasRepairCable)
    {
        HasRepairCable = hasRepairCable;
    }

    public bool TryCollectRepairCable()
    {
        if (HasRepairCable)
            return false;

        HasRepairCable = true;
        Debug.Log("Cable recogido. Ya puedes reparar el panel.", this);
        return true;
    }

    public bool TryUseRepairCable()
    {
        if (!HasRepairCable)
            return false;

        HasRepairCable = false;
        return true;
    }
}
