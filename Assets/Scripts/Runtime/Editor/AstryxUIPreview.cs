using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Review helper only: attach the UI to the open room without changing backend scripts or saving scenes.
public static class AstryxUIPreview
{
    [MenuItem("Tools/Astryx/Preview Phase 2 UI in Current Scene")]
    public static void Attach()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before attaching the review UI."); return;
        }
        if (Object.FindFirstObjectByType<AstryxDemo>() != null)
        {
            Debug.Log("Astryx UI is already in this scene. Press Play to review it."); return;
        }
        var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Runtime/Astryx.uxml");
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/AstryxPanelSettings.asset");
        if (layout == null || panel == null)
        {
            Debug.LogError("Astryx UI layout or Panel Settings asset is missing."); return;
        }
        var ui = new GameObject("Astryx UI • Phase 2 Review");
        Undo.RegisterCreatedObjectUndo(ui, "Attach Astryx UI Review");
        ui.SetActive(false);
        var doc = ui.AddComponent<UIDocument>(); doc.panelSettings = panel; doc.visualTreeAsset = layout;
        ui.AddComponent<AstryxDemo>();
        ui.SetActive(true);
        // Disable only the old instruction HUD so its duplicate header/footer do not overlap this UI.
        foreach (var instructions in Object.FindObjectsByType<DemoInstructions>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(instructions, "Hide old instruction HUD for UI review");
            instructions.enabled = false;
        }
        Selection.activeGameObject = ui;
        Debug.Log("Review UI attached locally. Press Play. The scene has not been saved; Undo removes the review setup.");
    }
}
