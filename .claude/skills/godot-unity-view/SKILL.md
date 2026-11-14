---
name: godot-unity-view
description: >
  Port Kalyeah's Godot scenes, view scripts, signals, rigged kid .glb models,
  shaders, and VFX/SFX (scenes/**/*.tscn, scripts/view/*.gd) into Unity 6 URP
  Mobile Prefabs, read-only view puppets, and pooled effects.
---

# Godot-to-Unity Migration — Part 2: URP Mobile View & Prefabs (`godot-unity-view`)

Owns porting `scenes/{match,map,props,fx,ui}/**/*.tscn`, `scripts/view/*.gd`, view signals, `.glb` character/prop rigs, shaders, and VFX/SFX into Unity 6 URP Mobile.

## Boundary (zero overlap)

- **Owns:** Unity Prefabs, scene hierarchy, read-only `Kalyeah.View` `MonoBehaviour` puppets, camera presentation (`Shoulder` + data-toggled `Follow`), URP materials/ShaderGraph/outlines, `.glb` `Animator` wiring, owner asset-policy stand-ins, and view/VFX/SFX object pools.
- **Defers to:** `godot-unity-sim` for `Kalyeah.Sim` gameplay rules and `ScriptableObject` balance schemas; `godot-unity-platform` for bootstrap autoloads, WebSocket/Hotspot networking, `EnhancedTouch` input capture, UTF test runner config, and Android APK export.

## 1. Sim-read-only view puppets

- `Kalyeah.View` references `Kalyeah.Sim` one-way and reads `MatchSim` / interpolated `SimSnapshot` state plus discrete sim events (cast, hit, tag, rescue, score).
- Never mutate sim state or place hit detection, range clamps, cooldowns, or capture timers inside `Update`, `LateUpdate`, `Animator` state behaviors, or Unity `OnTriggerEnter` colliders.
- Interpolate visual puppet transforms smoothly between `30 Hz` ticks (`20 Hz` network snapshots) before updating the over-the-shoulder perspective `Camera` (or data-toggled `CameraRules.follow` fallback) in `LateUpdate`.
- Drive world markers purely from sim/settings state: flat ground rescue countdown (`0.5` white alpha while counting -> `0.55` team tint when ready) and `MoveMarker` ease-out ring (static dot when `ReduceMotion` is enabled).

## 2. `.tscn` & `signal` -> flat Prefabs & typed C# events

| Godot (`scenes/` & `scripts/view/`) | Unity 6 (`Prefabs/` & `Kalyeah.View`) |
|---|---|
| `.tscn` sub-scene | Flat, single-responsibility modular Prefab (avoid deep nested-Prefab chains) |
| `%UniqueName` / `@export var path: NodePath` | `[SerializeField] private FooView _foo;` bound on the Prefab (`OnValidate` null-check) |
| `signal hit_landed(id, pos)` | `public event Action<int, Vector3> HitLanded;` (avoid `UnityEvent` in hot combat paths) |
| `get_node("../../..")` (banned) | Banned: never call `GameObject.Find`, `Transform.Find`, `GetComponentInChildren` per frame, or `SendMessage` |

- Always pair `+=` subscriptions in `OnEnable` / `Bind` with `-=` unsubscriptions in `OnDisable` / `OnDestroy` so round resets and scene loads leak zero listeners.
- Trigger HUD/overhead bar redraws only when watched values change (HP, stamina/overclock, elixir/cooldown, status priority, score) — never rebuild strings or layouts every frame.

## 3. URP Mobile materials, team outlines & `.glb` kid rigs

- Target Unity 6 **URP Mobile** (Forward renderer) with strict **SRP Batcher** compatibility across `BarangayProps`, mirrored *tabi-tabing bahay*, Estero water, and komiks VFX:
  - Declare per-material uniforms inside `CBUFFER_START(UnityPerMaterial)` and share `sharedMaterial` instances; use `MaterialPropertyBlock` only on explicitly GPU-instanced prop batches.
  - Keep world/character textures ASTC/ETC2 VRAM-compressed (`mipmaps` enabled on 3D textures, normal maps marked Normal) and combine static overhead `SpaghettiWires` / *sampayan* meshes.
- **Team readability rule (binding):** Render flat team silhouette outlines (`blue` ally vs `red` enemy via URP `RenderObjects` stencil/hull feature) plus overhead health bar, team number badge, and single top-priority italic status word (`Stunned` overrides `Exhausted`). **Never draw ground rings or circles under any kid's feet.**
- Import all 10 rigged `.glb` kids (`assets/models/characters/<id>.glb`, `kid_view_scale = 0.82` ≈ `1.52 m`) with `static readonly int` `Animator.StringToHash` IDs (`Idle`, `Walk`, `Run`, `Jump`, `Crouch`, `Slide`, `Trip`, `Cast`, `Tagged`).

## 4. Owner asset policy & mandatory view pooling

- **Advise, never generate (owner rule):** Never generate `.glb` models, sprite sheets, textures, icons, or audio files. Keep procedural C# mesh/UI stand-ins working until the owner adds authored assets, and state which asset type is needed:
  - **Blender 3D model** — street props, cover, buildings, electric post seen in 3D depth/shadows.
  - **Sprite sheet** — 2D painted-look animated puffs and hit effects in 3D space.
  - **2D image** — flat HUD icons, cards, posters, signs, ground decals, loading backgrounds.
- Pre-warm fixed `ObjectPool<T>` pools at match load for `Tirador` stones, superpower projectiles, `FxSketch` impact stars/dust rings, overhead bars, and one-shot `AudioSource` emitters — zero `Instantiate` / `Destroy` mid-round.

## Anti-patterns to reject

- Accessing `renderer.material` at runtime (clones material instances and breaks SRP Batcher).
- Drawing selection/team rings on the street under free or tagged kids.
- Passing string state/parameter names to `Animator.SetBool("...")` or `Shader.SetFloat("...")` per frame.
