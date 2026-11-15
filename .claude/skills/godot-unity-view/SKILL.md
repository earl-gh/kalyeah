---
name: godot-unity-view
description: >
  Own Kalyeah's Unity 6 URP Mobile 3D world, read-only view puppets, GodotSpace
  coordinate mapping, rigged .glb kids, SRP-batched shaders, and pooled VFX/SFX
  (unity/Game/Assets/Kalyeah/View and Shaders).
---

# Unity 6 & .NET 8 Architecture — Part 2: URP Mobile View & Shaders (`godot-unity-view`)

Owns `unity/Game/Assets/Kalyeah/View/` and `Shaders/`: Unity 6 URP Mobile 3D Barangay world, read-only view puppets, `.glb` character rigs, SRP-batched shaders, and pooled VFX/SFX.

## Boundary (zero overlap)

- **Owns:** Modular Unity 6 view hierarchy, typed C# event wiring, read-only `Kalyeah.Game` `MonoBehaviour` puppets, `GodotSpace` 3D handedness & `RectTransform` anchor conventions, URP Mobile `CBUFFER` / silhouette team-outline shaders, `.glb` rig/material sanitization (`KalyeahResources`), procedural stand-in retention, and `FxPool` / `AudioHub` pooling.
- **Defers to:** `godot-unity-sim` (`Kalyeah.Sim` & `unity/Data/*.json`); `godot-unity-platform` (`AppSession`, WebSocket/Hotspot, `EnhancedTouch`, APK); `apex-combat-cam` (shoulder framing & aim parallax); `apex-street-world` (barangay art & value bands); `apex-mobile-hud` & `apex-screen-fit` (HUD/menu layout & multi-aspect bounds); `apex-net-frame` (ms, overdraw & VRAM ceilings); `CLAUDE.md` §Assets (advise-don't-generate rule).

## 1. Sim-read-only view puppets & `LateUpdate` transform order

- `Kalyeah.Game` reads `SimBridge` / `StreetBridge` state and discrete combat events (cast, hit, tag, rescue, score) one-way.
- Never mutate sim state or put hit detection, range clamps, cooldowns, or capture timers inside `Update`, `LateUpdate`, `Animator` state behaviors, or Unity physics triggers.
- Interpolate puppet transforms between `30 Hz` sim ticks before updating `ShoulderCamera` (or data-toggled follow/orthographic modes) and screen-projected followers (`OverheadBarsView`, `MatchOverlaysView`) in `LateUpdate`.
- Drive world ground markers strictly from sim/settings state: rescue-zone countdown (`0.5` white alpha while counting -> `0.55` team tint when ready) and `MoveMarker` ease-out ring (static dot when `ReduceMotion` is enabled).

## 2. `GodotSpace` 3D handedness & `RectTransform` anchor rules

- **Canonical `GodotSpace` (`unity/Game/Assets/Kalyeah/Scripts/GodotSpace.cs`):** Route all sim street coordinates and yaw angles through `GodotSpace` (`sim (x, z)` -> Unity `(x, 0, -z)`, yaw `180° - θ`) so kids, `.glb` props (`kid_view_scale = 0.82` ≈ `1.52 m`), aim telegraphs, and North/South bases never invert `180°`. Never patch facing with negative `Transform.localScale` (flips triangle winding/normals and breaks SRP Batcher).
- **Sanitized `.glb` materials:** Route runtime-loaded `.glb` hierarchies through `KalyeahResources.SanitizeModelMaterials(GameObject)` so `glTF` shaders/UVs resolve cleanly to URP Mobile materials on a shared cache without per-spawn leaks.
- **Bottom-left `+Y` up `RectTransform` anchors:** Declare explicit normalized anchor/pivot pairs (`anchorMin`, `anchorMax`, `pivot`) and signed `anchoredPosition` offsets — never hardcode raw screen pixel coordinates.

## 3. Explicit references & dirty-driven C# events

- Wire child views via `[SerializeField]` or explicit builder references — ban `GameObject.Find`, `Transform.Find`, per-frame `GetComponentInChildren`, and `SendMessage`.
- Pair every `+=` event subscription in `OnEnable` / `Bind` with `-=` in `OnDisable` / `OnDestroy` so round resets and scene transitions leak zero listeners.
- Redraw UI/follower views only on watched value changes — never rebuild strings or `LayoutGroup` hierarchies every frame.

## 4. URP Mobile SRP Batcher, team outlines & `FxPool`

- Target Unity 6 **URP Mobile** (Forward) with **SRP Batcher** compatibility: wrap per-material uniforms in `CBUFFER_START(UnityPerMaterial)`, share `sharedMaterial` instances across `BarangayEnvironmentBuilder` and props, reserve `MaterialPropertyBlock` strictly for GPU-instanced batches, and merge static `SpaghettiWiresBuilder` meshes.
- Render `blue` ally / `red` enemy silhouette outlines via `Shaders/TeamOutline.shader` (perpendicular view-space rim projection, **zero ground rings or circles under any kid's feet**).
- Precompute `static readonly int` IDs via `Animator.StringToHash` and `Shader.PropertyToID`.
- Pre-warm pools (`FxPool`, `AudioHub`) at match load — zero `Instantiate` / `Destroy` mid-round, and never generate art/audio files (follow `CLAUDE.md` §Assets).

## Anti-patterns to reject

- Bypassing `GodotSpace` on street coordinates/yaws or fixing handedness with negative `Transform.localScale`.
- Accessing `renderer.material` per instance at runtime, drawing ground rings under kids, or passing string names to `Animator`/`Shader` setters per frame.
- Running `Instantiate` / `Destroy` or `GameObject.Find` inside combat ticks or `Update`.
