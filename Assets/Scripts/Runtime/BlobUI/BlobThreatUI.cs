using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Presentation only. The enemy system decides the state and optional screen-space direction.</summary>
[RequireComponent(typeof(UIDocument))]
public class BlobThreatUI : MonoBehaviour
{
    public enum ThreatState { Clear, Nearby, Following, Danger }
    VisualElement warning;
    Label status, message, direction;
    ThreatState currentState;
    Vector2 currentDirection;
    bool directionVisible, menuOpen;

    void OnEnable()
    {
        if (!Bind(GetComponent<UIDocument>().rootVisualElement))
        {
            Debug.LogError("Assign a UI layout containing BlobThreat.uxml to this UI Document.", this);
            enabled = false;
        }
    }

    // Supports both the standalone preview and the shared Astryx HUD template.
    public bool Bind(VisualElement root)
    {
        if (root == null) return false;
        warning = root.Q("blob-warning");
        status = root.Q<Label>("blob-status");
        message = root.Q<Label>("blob-message");
        direction = root.Q<Label>("blob-direction");
        if (warning == null || status == null || message == null || direction == null) return false;
        ApplyState();
        return true;
    }

    /// <param name="screenDirection">Direction from player to alien in screen space: right = +X, up = +Y.</param>
    /// <param name="showDirection">Show the arrow only when the gameplay system requests an off-screen cue.</param>
    public void SetThreat(ThreatState state, Vector2 screenDirection = default, bool showDirection = false)
    {
        currentState = state; currentDirection = screenDirection; directionVisible = showDirection;
        if (warning != null) ApplyState();
    }

    public void SetMenuOpen(bool open)
    {
        menuOpen = open;
        if (warning != null) ApplyState();
    }

    void ApplyState()
    {
        warning.EnableInClassList("hidden", menuOpen || currentState == ThreatState.Clear);
        warning.EnableInClassList("following", currentState == ThreatState.Following);
        warning.EnableInClassList("danger", currentState == ThreatState.Danger);
        switch (currentState)
        {
            case ThreatState.Nearby:
                status.text = "STRANGE PRESENCE"; message.text = "Something is nearby."; break;
            case ThreatState.Following:
                status.text = "YOU ARE BEING FOLLOWED"; message.text = "Keep an eye on the shadows."; break;
            case ThreatState.Danger:
                status.text = "DANGER — KEEP MOVING"; message.text = "The dark blob is closing in."; break;
            default:
                status.text = ""; message.text = ""; break;
        }
        bool showArrow = directionVisible && currentState != ThreatState.Clear && currentDirection.sqrMagnitude > 0.0001f;
        direction.EnableInClassList("hidden", !showArrow);
        if (showArrow)
        {
            // UI rotation is clockwise; the base arrow points upward.
            float angle = Mathf.Atan2(currentDirection.x, currentDirection.y) * Mathf.Rad2Deg;
            direction.style.rotate = new Rotate(new Angle(angle));
        }
    }

    // Inspector previews change only presentation. They never control an enemy or player.
    [ContextMenu("Preview/Clear")] void PreviewClear() => SetThreat(ThreatState.Clear);
    [ContextMenu("Preview/Nearby")] void PreviewNearby() => SetThreat(ThreatState.Nearby);
    [ContextMenu("Preview/Following")] void PreviewFollowing() => SetThreat(ThreatState.Following, Vector2.left, true);
    [ContextMenu("Preview/Danger")] void PreviewDanger() => SetThreat(ThreatState.Danger, Vector2.right, true);
}
