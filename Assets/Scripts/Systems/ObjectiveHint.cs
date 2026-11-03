using UnityEngine;

// =============================================================================
// ObjectiveHint
// Author: Hamitha Viyyapu
//
// Makes the HINT outcome do something in the room. While a hint is active,
// the next objective pulses (grows a little and glows mint) so the player
// knows where to go: repair cable -> power panel -> exit door.
// =============================================================================
public class ObjectiveHint : MonoBehaviour
{
    [SerializeField] private FateEffectController fate;
    [SerializeField] private RepairCablePickup    cable;
    [SerializeField] private RepairPanel          panel;
    [SerializeField] private ExitDoor             door;

    [SerializeField] private float pulseSpeed  = 4f;
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private Color hintColor   = new Color(0.55f, 1f, 0.8f);

    private Transform      current;
    private SpriteRenderer currentRenderer;
    private Vector3        baseScale;
    private Color          baseColor;

    public Transform CurrentTarget => current;

    private void Update()
    {
        Transform target = fate != null && fate.HintActive ? NextObjective() : null;
        if (target != current) Highlight(target);
        if (current == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        current.localScale = baseScale * (1f + pulseAmount * t);
        if (currentRenderer != null) currentRenderer.color = Color.Lerp(baseColor, hintColor, t);
    }

    private Transform NextObjective()
    {
        if (cable != null && cable.gameObject.activeInHierarchy && !cable.IsCollected) return cable.transform;
        if (panel != null && !panel.IsRepaired)      return panel.transform;
        if (door  != null && !door.IsDemoCompleted)  return door.transform;
        return null;
    }

    private void Highlight(Transform target)
    {
        // put the old target back to normal
        if (current != null)
        {
            current.localScale = baseScale;
            if (currentRenderer != null) currentRenderer.color = baseColor;
        }

        current = target;
        currentRenderer = null;
        if (current == null) return;

        baseScale       = current.localScale;
        currentRenderer = current.GetComponent<SpriteRenderer>();
        baseColor       = currentRenderer != null ? currentRenderer.color : Color.white;
        Debug.Log($"[HINT] Highlighting next objective: {current.name}");
    }

    private void OnDisable() => Highlight(null);
}
