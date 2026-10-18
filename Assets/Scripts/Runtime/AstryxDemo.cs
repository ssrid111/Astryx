using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class AstryxDemo : MonoBehaviour
{
    // UI demonstration only. World collection, oxygen timing and fate effects belong to game systems.
    public event Action<string> FateSelected;
    public event Action PowerRestored;
    public event Action ContinueRequested;
    public event Action ExitRequested;
    public event Action<bool> PauseChanged;
    VisualElement root;
    string screen;
    bool spun;
    float oxygen = 78;
    readonly int[] turns = new int[9];
    readonly string[] glyphs = { "┗", "┏", "┓", "┛" };
    readonly string[] outcomes = { "IMMUNITY", "EXTRA TIME", "HINT", "NEW TOOL", "RESOURCE LOSS", "EQUIPMENT FAILURE", "REDUCED VISION", "HAZARD" };
    readonly string[] details = { "Ignore the next hazard hit.", "Add 15 seconds of oxygen.", "Follow left → down → right across the middle → down → right.", "Receive one repair charge.", "Lose 10 seconds of oxygen.", "Disable the repair tool for 5 seconds.", "Dim the scene for 5 seconds.", "Spawn one avoidable spark hazard." };
    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        Bind("start", () => Show(null));
        Bind("pause-open", () => Show("pause")); Bind("resume", () => Show(null));
        Bind("inventory-open", () => Show("inventory")); Bind("inventory-close", () => Show(null));
        Bind("fate-open", () => Show("roulette")); Bind("fate-close", () => Show(null));
        Bind("puzzle-open", () => Show("puzzle")); Bind("puzzle-close", () => Show(null));
        Bind("restart", () => Show("confirm")); Bind("cancel-restart", () => Show("pause"));
        Bind("confirm-restart", ResetMission); Bind("retry", ResetMission); Bind("replay", ResetMission);
        Bind("exit", Exit); Bind("gameover-exit", Exit);
        Bind("continue", () => { ContinueRequested?.Invoke(); root.Q<Label>("summary").text = "Connect ContinueRequested to your Level 2 scene loader."; });
        Bind("spin", Spin); Bind("test-circuit", TestCircuit);
        foreach (var item in new[] { "tool", "fuse", "cell" })
        {
            string selected = item;
            Bind("inspect-" + item, () => root.Q<Label>("item-detail").text = selected == "tool" ? "REPAIR TOOL • Opens the circuit console." : selected == "fuse" ? "POWER FUSE • Bridges the damaged power supply." : "ENERGY CELL • Supplies power to the circuit.");
        }
        for (int i = 0; i < 5; i++) Bind("slot-" + i, () => Show("inventory"));
        for (int i = 0; i < 9; i++) { int tile = i; Bind("tile-" + i, () => { turns[tile] = (turns[tile] + 1) % 4; PaintTile(tile); }); }
        root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { Show(screen == null ? "pause" : null); e.StopPropagation(); } });
        root.focusable = true;
        ResetMission();
    }
    void Bind(string name, Action action) { root.Q<Button>(name).clicked += action; }
    public void Show(string name)
    {
        screen = name;
        foreach (string id in new[] { "intro", "inventory", "roulette", "puzzle", "pause", "complete", "gameover", "confirm" }) root.Q(id).EnableInClassList("hidden", id != name);
        // Subscribe to this signal to freeze oxygen, player input and hazards. No global timeScale changes.
        PauseChanged?.Invoke(name != null);
        var panel = name == null ? root : root.Q(name);
        var first = panel.Q<Button>(); if (first != null) first.Focus(); else root.Focus();
    }
    public void SetOxygen(float percent)
    {
        oxygen = Mathf.Clamp(percent, 0, 100);
        root.Q<Label>("oxygen-label").text = "OXYGEN  " + Mathf.RoundToInt(oxygen) + "%";
        root.Q("oxygen-fill").style.width = Length.Percent(oxygen);
        if (oxygen <= 0) Show("gameover");
    }
    public void SetObjective(string message) { root.Q<Label>("objective").text = message; }
    void ResetMission()
    {
        spun = false; SetOxygen(78); SetObjective("Demo: fuse + energy cell collected");
        root.Q<Button>("spin").SetEnabled(true); root.Q<Button>("fate-close").SetEnabled(true);
        root.Q<Label>("result-type").text = "One free spin per mission.";
        root.Q<Label>("result-title").text = "Feeling lucky?";
        root.Q<Label>("result-detail").text = "4 helpful and 4 harmful outcomes.";
        root.Q<Label>("circuit-status").text = "Join one unbroken path.";
        for (int i = 0; i < 9; i++) { turns[i] = 0; PaintTile(i); }
        Show("intro");
    }
    void Exit() { ExitRequested?.Invoke(); ResetMission(); }
    int Mask(int i) { return i == 4 ? (turns[i] % 2 == 0 ? 10 : 5) : new[] { 3, 6, 12, 9 }[turns[i]]; }
    void PaintTile(int i) { root.Q<Button>("tile-" + i).text = i == 4 ? (turns[i] % 2 == 0 ? "━" : "┃") : glyphs[turns[i]]; }
    void Spin()
    {
        if (spun) return;
        spun = true; int pick = UnityEngine.Random.Range(0, 8);
        root.Q<Button>("spin").SetEnabled(false);
        root.Q<Label>("result-type").text = pick < 4 ? "★ HELPFUL" : "× HARMFUL";
        root.Q<Label>("result-title").text = outcomes[pick];
        root.Q<Label>("result-detail").text = details[pick];
        // Segment 0 is at twelve o'clock; clockwise segment indices match outcomes.
        root.Q("wheel").style.rotate = new Rotate(new Angle(-pick * 45f));
        FateSelected?.Invoke(outcomes[pick]);
    }
    void TestCircuit()
    {
        // Each elbow has two ports: up=1, right=2, down=4, left=8.
        // Glyphs ┗ ┏ ┓ ┛ correspond to up/right, right/down, down/left, left/up.
        int[] dr = { -1, 0, 1, 0 }, dc = { 0, 1, 0, -1 }, bits = { 1, 2, 4, 8 }, opposite = { 4, 8, 1, 2 };
        bool ok = (Mask(0) & 8) != 0 && (Mask(8) & 2) != 0;
        var visited = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(0);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(); if (!visited.Add(i)) continue;
            for (int d = 0; d < 4; d++)
            {
                int r = i / 3 + dr[d], c = i % 3 + dc[d]; if (r < 0 || r > 2 || c < 0 || c > 2) continue;
                int j = r * 3 + c;
                if ((Mask(i) & bits[d]) != 0 && (Mask(j) & opposite[d]) != 0) queue.Enqueue(j);
            }
        }
        if (ok && visited.Contains(8))
        {
            SetObjective("Power restored • Exit unlocked");
            root.Q<Label>("summary").text = "Oxygen remaining: " + Mathf.RoundToInt(oxygen) + "% • Circuit repaired ✓";
            PowerRestored?.Invoke(); Show("complete");
        }
        else root.Q<Label>("circuit-status").text = "Open circuit. Check the source, core and tile connections.";
    }
}
