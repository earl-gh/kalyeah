---
name: godot-unity-view
description: >
  Port Kalyeah's Godot scenes, view scripts, signals, rigged kid .glb models,
  and shaders (scenes/**/*.tscn, scripts/view/*.gd) into Unity 6 URP Mobile
  Prefabs, coordinate-safe rigs/UI, read-only view puppets, and pooled effects.
---

# Godot-to-Unity Migration — Part 2: URP Mobile View & Prefabs (`godot-unity-view`)

Owns translating `scenes/{match,map,props,fx,ui}/**/*.tscn`, `scripts/view/*.gd`, view signals, `.glb` rigs, and shaders into Unity 6 URP Mobile.

## Boundary (zero overlap)

- **Owns:** Flat Unity Prefabs, C# `event Action` view wiring, read-only `Kalyeah.View` `MonoBehaviour` puppets, Godot-to-Unity 3D handedness & UI origin conversion, URP Mobile `CBUFFER`/stencil-outline mechanics, `.glb` `Animator` binding, procedural stand-in retention, and `ObjectPool<T>` wiring.
- **Defers to:** `godot-unity-sim` (`Kalyeah.Sim` & `SimConfig`); `godot-unity-platform` (Bootstrap, WebSocket/Hotspot, `EnhancedTouch`, APK); `apex-combat-cam` (shoulder framing & aim parallax); `apex-street-world` (barangay art & value bands); `apex-mobile-hud` (HUD & overhead bar layout/stacking); `apex-net-frame` (ms, overdraw & VRAM ceilings); `CLAUDE.md` §Assets (asset-kind taxonomy).

## 1. Sim-read-only view puppets & `LateUpdate` transform order

- `Kalyeah.View` references `Kalyeah.Sim` one-way, reading interpolated `SimSnapshot` state and discrete sim events (cast, hit, tag, rescue, score).
- Never mutate sim state or place hit detection, range clamps, cooldowns, or capture timers in `Update`, `LateUpdate`, `Animator` state behaviors, or Unity `OnTriggerEnter` colliders.
- Interpolate visual puppet transforms between `30 Hz` sim ticks before updating the `Camera` rig (`Shoulder` or data-toggled `CameraRules.follow`) and camera-facing/screen-projected followers in `LateUpdate`.
- Drive world ground markers strictly from sim/settings state: flat rescue-zone countdown (`0.5` white alpha while counting -> `0.55` team tint when ready) and `MoveMarker` ease-out ring (static dot when `ReduceMotion` is enabled).

## 2. 3D handedness (`-Z` vs `+Z`) & UI origin (`+Y` down vs `+Y` up) traps

- **3D right-handed `-Z` -> left-handed `+Z`:** Godot faces `-Z` (`Vector3.FORWARD = (0,0,-1)`), whereas Unity faces `+Z` (`Vector3.forward = (0,0,1)`).
  - Normalize `.glb` character and prop rigs (`assets/models/characters/<id>.glb`, `kid_view_scale = 0.82` ≈ `1.52 m`) once in `glTFast` / FBX import axis settings so meshes, hand weapon sockets, and clips face Unity `+Z` — never patch `180°` backwards facing with negative `localScale` (flips triangle winding/normals and breaks SRP Batcher).
  - Route all street positions and facing vectors through one canonical `SimSpace` adapter mapping `SimVec2(X, Z)` onto `Transform.position` and `Transform.forward` (`Quaternion.LookRotation`) so kids, aim telegraphs, and mirrored street halves never invert or animate `180°` backwards.
- **UI top-left (`+Y` down) -> `RectTransform` bottom-left (`+Y` up):** Godot `Control` anchors/offsets measure downward from top-left `(0,0)`, whereas Unity `RectTransform` normalizes anchors from `(0,0)` bottom-left to `(1,1)` top-right with `+Y` up.
  - Convert `.tscn` controls by declaring explicit anchor/pivot pairs (e.g., top-anchored `anchorMin = (0,1)`, `anchorMax = (1,1)`, `pivot = (0.5f,1f)`) and negating vertical pixel offsets (`anchoredPosition.y`) instead of copying raw `offset_top`/`offset_bottom` signs.

## 3. `.tscn` & `signal` -> flat Prefabs & dirty-driven C# events

| Godot 4.6 (`scenes/` & `scripts/view/`) | Unity 6 (`Prefabs/` & `Kalyeah.View`) |
|---|---|
| `.tscn` sub-scene | Flat, single-responsibility modular Prefab (avoid deep nested-Prefab chains) |
| `%UniqueName` / `@export var path: NodePath` | `[SerializeField] private FooView _foo;` bound on the Prefab (`OnValidate` null-check) |
| `signal hit_landed(id, pos)` | `public event Action<int, Vector3> HitLanded;` (avoid `UnityEvent` in hot combat paths) |
| `get_node("../../..")` (banned) | Banned: no `GameObject.Find`, `Transform.Find`, per-frame `GetComponentInChildren`, or `SendMessage` |

- Pair every `+=` subscription in `OnEnable` / `Bind` with `-=` in `OnDisable` / `OnDestroy` so round resets and scene loads leak zero listeners.
- Redraw UI/follower views only on watched value-change events — never rebuild strings or `LayoutGroup` hierarchies per frame.

## 4. URP Mobile batching, stencil outlines, stand-ins & `ObjectPool<T>`

- Target Unity 6 **URP Mobile** (Forward) with **SRP Batcher** mechanics: wrap per-material uniforms in `CBUFFER_START(UnityPerMaterial)`, share `sharedMaterial` references across `BarangayProps` and houses, reserve `MaterialPropertyBlock` strictly for GPU-instanced prop batches, and merge static overhead wire/*sampayan* meshes.
- Render `blue` ally / `red` enemy silhouette outlines via a URP `RenderObjects` stencil/hull pass (**never draw ground rings or circles under any kid's feet**) and sync overhead follower transforms to camera/screen space in `LateUpdate` (`apex-mobile-hud` owns overhead bar/badge/status layout).
- Precompute `static readonly int` IDs via `Animator.StringToHash` (`Idle`, `Walk`, `Run`, `Jump`, `Crouch`, `Slide`, `Trip`, `Cast`, `Tagged`) and `Shader.PropertyToID`.
- Keep C# procedural mesh/UI stand-ins working until replaced (never generate `.glb`, sprite, texture, icon, or audio files yourself) and defer asset-kind advisories to `CLAUDE.md` §Assets + `apex-street-world`.
- Pre-warm `UnityEngine.Pool.ObjectPool<T>` instances at match load for projectiles, `FxSketch` impacts, overhead followers, and one-shot `AudioSource` emitters — zero `Instantiate` / `Destroy` mid-round (`apex-net-frame` owns ms, overdraw & VRAM ceilings).

## Anti-patterns to reject

- Leaving `.glb` rigs or `SimVec2` facing adapters inverted `180°`, or fixing handedness with negative `Transform.localScale`.
- Copying Godot top-down `+Y` pixel offsets directly into bottom-up `RectTransform.anchoredPosition.y`.
- Accessing `renderer.material` at runtime, drawing ground rings under kids, or passing string names to `Animator`/`Shader` setters per frame.
