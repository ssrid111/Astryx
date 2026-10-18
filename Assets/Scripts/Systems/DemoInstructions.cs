using TMPro;
using UnityEngine;

// Creates a small runtime HUD without changing the room's scene hierarchy.
public class DemoInstructions : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private RepairPanel repairPanel;
    [SerializeField] private ExitDoor exitDoor;

    private GameObject canvasObject;
    private TextMeshProUGUI objective;
    private TMP_FontAsset runtimeFont;

    private void Start()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            runtimeFont = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            font = runtimeFont;
        }

        canvasObject = new GameObject("DemoInstructionsCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform header = CreatePanel("ObjectivePanel", true, 64f);
        objective = CreateText(header, "Objective", font, 25f);
        RectTransform footer = CreatePanel("ControlsPanel", false, 100f);
        TextMeshProUGUI controls = CreateText(footer, "Controls", font, 23f);
        controls.text = "<b>WASD</b>  Move     <b>E</b>  Pick up / repair / exit (when nearby)\n"
            + "<b>F5</b>  Save     <b>F9</b>  Load     Click the Game view to use the keyboard.";
        UpdateObjective();
    }

    private RectTransform CreatePanel(string panelName, bool top, float height)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.sizeDelta = new Vector2(-32f, height);
        rect.anchoredPosition = new Vector2(0f, top ? -12f : 12f);
        UnityEngine.UI.Image image = panel.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.025f, 0.055f, 0.1f, 0.92f);
        image.raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string textName, TMP_FontAsset font, float size)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20f, 8f);
        rect.offsetMax = new Vector2(-20f, -8f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = new Color(0.77f, 1f, 0.88f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private void Update()
    {
        if (objective != null)
            UpdateObjective();
    }

    private void UpdateObjective()
    {
        string message = "Find the cable and press E to pick it up.";
        if (exitDoor != null && exitDoor.IsDemoCompleted)
            message = "Demo complete. Press F9 to load your saved game.";
        else if (repairPanel != null && repairPanel.IsRepaired)
            message = "Power restored. Approach the door and press E to exit.";
        else if (playerInventory != null && playerInventory.HasRepairCable)
            message = "Cable collected. Approach the panel and press E to repair it.";
        objective.text = "<b>ASTRYX DEMO</b>   |   " + message;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);
        if (runtimeFont != null)
        {
            foreach (Texture2D atlas in runtimeFont.atlasTextures)
                if (atlas != null)
                    Destroy(atlas);
            if (runtimeFont.material != null)
                Destroy(runtimeFont.material);
            Destroy(runtimeFont);
        }
    }
}
