using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class BlobThreatPreview
{
    [MenuItem("Tools/Astryx/Preview Blob Alien Warning UI")]
    public static void Attach()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before adding the preview UI."); return; }
        var existing = Object.FindFirstObjectByType<BlobThreatUI>();
        if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
        var host = Object.FindFirstObjectByType<AstryxDemo>();
        if (host != null)
        {
            var warning = Undo.AddComponent<BlobThreatUI>(host.gameObject);
            Selection.activeGameObject = warning.gameObject;
            Debug.Log("The blob warning is part of this Astryx HUD. Press Play, start the mission, then use the BlobThreatUI component's Preview menu.");
            return;
        }
        var source = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/Runtime/BlobUI/BlobThreat.uxml");
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/AstryxPanelSettings.asset");
        if (source == null || settings == null) { Debug.LogError("Import the BlobUI files into Astryx before previewing."); return; }
        var go = new GameObject("Dark Blob • Warning UI Review");
        Undo.RegisterCreatedObjectUndo(go, "Add Blob Warning UI"); go.SetActive(false);
        var doc = go.AddComponent<UIDocument>(); doc.panelSettings = settings; doc.visualTreeAsset = source; doc.sortingOrder = 10;
        go.AddComponent<BlobThreatUI>();go.SetActive(true);Selection.activeGameObject = go;
        Debug.Log("Press Play. In the BlobThreatUI component menu choose Preview/Nearby, Following or Danger. Scene changes are unsaved and reversible with Undo.");
    }
}
