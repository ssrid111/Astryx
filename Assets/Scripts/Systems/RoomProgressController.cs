using UnityEngine;

// =============================================================================
// RoomProgressController
// Author: Hamitha Viyyapu
//
// Connects the Fate systems to the room's repair flow:
//   - EQUIPMENT FAILURE: the power panel can't be repaired while tools are down
//   - Panel repaired = room complete: puzzle timer stops, the alien retreats,
//     Fate modifiers clear (ClearEffects) and life support gives back some
//     oxygen.
// =============================================================================
public class RoomProgressController : MonoBehaviour
{
    [SerializeField] private FateEffectController fate;
    [SerializeField] private PuzzleTimer          puzzleTimer;
    [SerializeField] private BlobAlienController  alien;
    [SerializeField] private RepairPanel          repairPanel;

    [SerializeField] private float powerRestoredOxygenBonus = 15f;

    private bool panelBlocked;
    private bool roomComplete;

    public bool RoomComplete => roomComplete;

    private void Update()
    {
        if (repairPanel == null) return;

        // EQUIPMENT FAILURE -> turn off the panel's interaction until it expires.
        bool block = fate != null && fate.EquipmentFailed && !repairPanel.IsRepaired;
        if (block != panelBlocked)
        {
            panelBlocked = block;
            repairPanel.enabled = !block;
            Debug.Log(block
                ? "[ROOM] EQUIPMENT FAILURE -> power panel can't be repaired right now."
                : "[ROOM] Equipment back online -> power panel can be repaired.");
        }

        if (!roomComplete && repairPanel.IsRepaired) CompleteRoom();
    }

    private void CompleteRoom()
    {
        roomComplete = true;
        if (puzzleTimer != null) puzzleTimer.StopTimer();
        if (alien != null)       alien.Retreat();
        if (fate != null)
        {
            fate.ClearEffects();
            fate.AddOxygen(powerRestoredOxygenBonus);
        }
        Debug.Log("[ROOM] Power restored -> room complete. Timer stopped, modifiers cleared.");
    }
}
