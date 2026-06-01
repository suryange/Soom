# SOOM — Sandstorm Clarity PoC (Unity / URP / Meta Quest)

Goal: blowing-sand visuals that **respond to one input value** — vision is buried in
dust → clears progressively. The input is a single float `Clarity` (0 = sandstorm,
1 = clear). In the PoC you drive it by hand; later your breathing pipeline writes the
exact same value.

```
[ClarityPoCDriver]  ──writes──▶  [BreathSignalSO.Clarity]  ──read──▶  [SandstormController]
 (thumbstick/keys)                  (the only seam)                     ├─ DustVignette overlay quad (billowing haze)
                                                                        ├─ sand ParticleSystem
                                                                        ├─ GroundDust plane (drifting surface sand)
                                                                        └─ URP fog
```

## Files
- `BreathSignalSO.cs` — the shared state asset (input ↔ output seam)
- `SandstormController.cs` — reads Clarity, drives all visual layers (with damping)
- `ClarityPoCDriver.cs` — PoC input (delete once real breathing input exists)
- `DustVignette.shader` — radial airborne-dust overlay (2-layer scrolling + domain warp), stereo-safe
- `GroundDust.shader` — wind-blown sand drifting over the floor, world-XZ projected (no slope
  stretch) — HLSL port of the Unity blog's "MovingDust Mask" Shader Graph
- `dust_noise.png` — seamless tileable dust texture, shared by all dust shaders

## Requirements
- Unity 6 (6000.x), **URP** active.
- XR Plug-in Management → **OpenXR** for Android, with the **Meta Quest Support** feature
  group + **Oculus Touch Controller** interaction profile.
- **Input System** package. The driver's keyboard test is guarded by `ENABLE_INPUT_SYSTEM`;
  the thumbstick path uses `UnityEngine.XR` and works regardless. Head tracking is via the
  Input System `TrackedPoseDriver` on the camera (added by the builder).
- Build target: Android, Quest 2/3.

## Setup — one click (recommended)
An editor script `Assets/Editor/SoomSetup.cs` automates everything below. Top menu **SOOM ▸ Setup**:
1. **`1. Install XR Packages (OpenXR)`** — adds `com.unity.xr.management` + `com.unity.xr.openxr`
   at the version compatible with your Unity (resolved by the Package Manager, not pinned).
2. **`2. Build PoC Scene`** — in the *currently open scene*, creates & wires:
   `BreathSignal.asset`, `DustVignette.mat` (noise assigned, tiling 3×3, texture set to Repeat),
   a head-tracked Main Camera, the head-locked **DustOverlay** quad, the world **SandParticles**
   system, **SandstormFX** (`SandstormController`, all references wired), and **ClarityDriver**.
   Idempotent — re-run anytime; it reuses objects/assets by name.
3. **`Open Read-Me · XR steps`** — prints the few remaining clicks that must be done in the
   XR Plug-in Management UI (loader/profile toggles + Android build target).

After step 2, save the scene, press **Play**, and hold Up/Down (see Test). For Quest, do the
XR Plug-in Management clicks from step 3, then Build & Run.

> Manual setup is no longer needed, but the equivalent hand steps (signal asset → texture wrap →
> material → camera+TrackedPoseDriver → overlay quad → particles → controller/driver wiring) are
> what the builder performs, in case you want to inspect or tweak any single piece.

## Test
- **Editor:** press Play, hold **Up arrow** (clears) / **Down arrow** (sandstorm).
  Or drag the `Clarity` slider on the `BreathSignal` asset while in Play mode.
- **Headset:** build & run, push the **right thumbstick** up/down.
- You should see the dust ring contract from the edges inward as Clarity rises, the
  sand particles thin out, and the fog lift — all smoothly (SmoothDamp), not snapping.

Tune feel via `SandstormController`: the `clarityToObscurity` curve (make it ease-in so
the last bit of clearing feels rewarding) and `smoothTime`.

## Performance notes (Quest)
- The overlay is one full-screen transparent quad = full-screen overdraw. One layer is
  fine; don't stack several full-screen transparent layers.
- Keep particle count modest and use GPU instancing.
- Hold 72/90 Hz. The radial design keeps the **center always sharp**, which also helps
  comfort (motion sickness) — keep it that way.

## Upgrade path
- **True scene blur (like the reference image's blurred periphery):** the overlay only
  *covers* the periphery with dust; it doesn't blur the actual scene. For real optical
  blur, use URP's **Full Screen Pass Renderer Feature** + a **Fullscreen Shader Graph**
  (URP 14+/Unity 6) — it runs a screen-space pass and handles stereo for you. Heavier on
  Quest; gate it behind a quality toggle and validate frame time.
- **Real breathing input:** delete `ClarityPoCDriver`; have your `BreathProcessor`
  (respiration belt or controller IMU → filter → stability 0..1) write `signal.Clarity`.
  Nothing on the visual side changes. Drive the same value from a recorded-CSV replay
  source first so you can tune the visuals without a headset + live breath in the loop.
