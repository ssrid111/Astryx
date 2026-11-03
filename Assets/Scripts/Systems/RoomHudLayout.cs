using UnityEngine;
using UnityEngine.UIElements;

// =============================================================================
// RoomHudLayout
// Author: Hamitha Viyyapu
//
// Adds RoomHud.uss to the HUD in the playable room, so the HUD panels are
// smaller and sit at the screen edges instead of covering the room.
// Only used in DemoRoom; the Fate Roulette demo scene keeps its own layout.
// =============================================================================
[RequireComponent(typeof(UIDocument))]
public class RoomHudLayout : MonoBehaviour
{
    [SerializeField] private StyleSheet roomStyle;

    // Start, so the UI Document has already built its elements.
    private void Start()
    {
        if (roomStyle == null) return;
        var stage = GetComponent<UIDocument>().rootVisualElement.Q("stage");
        if (stage != null && !stage.styleSheets.Contains(roomStyle)) stage.styleSheets.Add(roomStyle);
    }
}
