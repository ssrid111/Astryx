# Dark blob alien warning UI

The main `Astryx.uxml` includes `BlobThreat.uxml` as a reusable template. `AstryxDemo` binds a `BlobThreatUI` component on the existing HUD object at runtime; no scene asset edit or second UI Document is required. Warnings hide when any Astryx menu opens and restore their current state when gameplay resumes. The clear state is hidden by default.

The enemy backend supplies its actual state:

```csharp
// Existing AstryxDemo reference:
ui.SetAlienThreat(BlobThreatUI.ThreatState.Nearby);
ui.SetAlienThreat(BlobThreatUI.ThreatState.Following, Vector2.left, true);
ui.SetAlienThreat(BlobThreatUI.ThreatState.Danger);
ui.SetAlienThreat(BlobThreatUI.ThreatState.Clear);
```

Direction is screen space (+X right, +Y up). Only request the arrow for a known off-screen direction. No enemy AI, detection radius, damage, or automatic pursuit is implemented by this UI.

To review in Play Mode, start the mission, select the HUD GameObject, then use the BlobThreatUI component menu: Preview/Nearby, Preview/Following, Preview/Danger or Preview/Clear. The optional Tools/Astryx/Preview Blob Alien Warning UI helper selects/adds that component for local review, without saving the scene. When no Astryx HUD exists, it can attach the standalone preview layout.

Alien art: `Assets/Sprites/Enemies/BlobAlien/BlobAlien.png`. Genuine transparent PNG, one static pose, centered pivot, 1200 pixels per unit. Generated from the agreed dark navy/purple cartoon concept to match the mint-accented astronaut. Use a SpriteRenderer for world placement when implementing the enemy; the warning already uses this art as its portrait. Animation and enemy movement remain separate gameplay/art work.
