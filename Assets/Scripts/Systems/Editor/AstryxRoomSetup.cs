using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// =============================================================================
// AstryxRoomSetup
// Author: Hamitha Viyyapu
//
// Tools > Astryx > Add Fate Systems To DemoRoom
// Adds the HUD, Fate Roulette systems, oxygen canister, blob
// alien and objective hint to the playable room and wires all references.
// Safe to run again: if the room is already set up, it only adds what's missing
// (for example the compact room HUD layout).
// =============================================================================
public static class AstryxRoomSetup
{
    const string ScenePath     = "Assets/Scenes/DemoRoom.unity";
    const string PanelSettings = "Assets/Settings/AstryxPanelSettings.asset";
    const string UxmlPath      = "Assets/Scripts/Runtime/Astryx.uxml";
    const string OxygenIcon    = "Assets/Sprites/UI/Art/oxygen.png";
    const string AlienSprite   = "Assets/Sprites/Enemies/BlobAlien/BlobAlien.png";
    const string RoomHudStyle  = "Assets/Scripts/Systems/RoomHud.uss";

    [MenuItem("Tools/Astryx/Add Fate Systems To DemoRoom")]
    public static void SetUpDemoRoom()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existing = GameObject.Find("FateSystems");
        if (existing != null)
        {
            if (existing.GetComponent<RoomHudLayout>() == null)
            {
                AddRoomHudLayout(existing);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[SETUP] Added the compact room HUD layout to FateSystems.");
            }
            else Debug.Log("[SETUP] DemoRoom is already up to date. Nothing to do.");
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerInventory>();
        var cable  = Object.FindFirstObjectByType<RepairCablePickup>(FindObjectsInactive.Include);
        var panel  = Object.FindFirstObjectByType<RepairPanel>();
        var door   = Object.FindFirstObjectByType<ExitDoor>();
        var cam    = Camera.main;
        if (player == null || panel == null || door == null)
        {
            Debug.LogError("[SETUP] DemoRoom is missing the player, repair panel or exit door.");
            return;
        }

        // ---- HUD + Fate systems on one object (same layout as FateRouletteDemo) ----
        var systems = new GameObject("FateSystems");
        var doc = systems.AddComponent<UIDocument>();
        doc.panelSettings   = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettings);
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

        var ui        = systems.AddComponent<AstryxDemo>();
        var timer     = systems.AddComponent<PuzzleTimer>();
        var inventory = systems.AddComponent<InventorySystem>();
        var fate      = systems.AddComponent<FateEffectController>();
        var progress  = systems.AddComponent<RoomProgressController>();
        var hint      = systems.AddComponent<ObjectiveHint>();
        AddRoomHudLayout(systems);

        // ---- oxygen canister in the bottom-right corner ----
        var canisterGo = new GameObject("OxygenCanister");
        canisterGo.transform.position = new Vector3(3.8f, -2.2f, 0f);
        var canisterSr = canisterGo.AddComponent<SpriteRenderer>();
        canisterSr.sortingOrder = 1;   // same layer as the repair cable
        var canister = canisterGo.AddComponent<OxygenCanister>();

        // ---- blob alien starts in the top-left corner ----
        var alienGo = new GameObject("BlobAlien");
        alienGo.transform.position = new Vector3(-5f, 2.3f, 0f);
        var alienSr = alienGo.AddComponent<SpriteRenderer>();
        alienSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AlienSprite);
        alienSr.sortingOrder = 9;      // just under the player (10)
        if (alienSr.sprite != null)
        {
            float height = alienSr.sprite.bounds.size.y;
            if (height > 0f) alienGo.transform.localScale = Vector3.one * (1.2f / height);   // about 1.2 units tall
        }
        var alien = alienGo.AddComponent<BlobAlienController>();

        // ---- wire references ----
        Set(timer, "startingTime", 120f);   // the room takes longer than the roulette demo
        Set(fate, "ui", ui); Set(fate, "puzzleTimer", timer); Set(fate, "inventory", inventory);
        Set(progress, "fate", fate); Set(progress, "puzzleTimer", timer);
        Set(progress, "alien", alien); Set(progress, "repairPanel", panel);
        Set(hint, "fate", fate); Set(hint, "cable", cable); Set(hint, "panel", panel); Set(hint, "door", door);
        Set(canister, "player", player); Set(canister, "inventory", inventory); Set(canister, "fate", fate);
        Set(canister, "icon", AssetDatabase.LoadAssetAtPath<Texture2D>(OxygenIcon));
        Set(alien, "player", player); Set(alien, "fate", fate); Set(alien, "ui", ui); Set(alien, "cam", cam);
        Set(alien, "roomMax", new Vector2(5.5f, 2.5f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SETUP] DemoRoom updated: HUD, Fate systems, oxygen canister, blob alien, objective hint.");
    }

    static void AddRoomHudLayout(GameObject systems)
    {
        var layout = systems.AddComponent<RoomHudLayout>();
        Set(layout, "roomStyle", AssetDatabase.LoadAssetAtPath<StyleSheet>(RoomHudStyle));
    }

    static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[SETUP] {target.GetType().Name} has no field '{field}'"); return; }
        switch (value)
        {
            case float f:   prop.floatValue = f; break;
            case Vector2 v: prop.vector2Value = v; break;
            default:        prop.objectReferenceValue = value as Object; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
