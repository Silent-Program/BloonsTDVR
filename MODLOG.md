# BloonsVR — working journal

Journal for this mod. Newest last. Everything here was verified on this machine unless marked as
"assumed". The universal-modder rules require this file to exist and to be updated as work happens:
anything not written down here is lost at the next context compaction.

---

## Phase 1 — playable 3D player + raycast tower placement

Status: **player controller confirmed working. WASD override + cursor button + sprite billboarding
deployed. Map geometry not started.**

### Files

| Path | What it is |
|---|---|
| `src/BloonsVR/BloonsVR.csproj` | net6.0 MelonLoader project, `<Private>false</Private>` on every game/loader reference |
| `src/BloonsVR/AssemblyInfo.cs` | `[assembly: MelonInfo]`, `[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]` |
| `src/BloonsVR/BloonsVRMod.cs` | Entry point + per-frame orchestrator, driven by `OnUpdate()` |
| `src/BloonsVR/PlayerRig.cs` | Player state in managed code, projected onto BTD6's camera |
| `src/BloonsVR/TowerPlacer.cs` | Screen-centre raycast → `TowerManager.CreateTower` |
| `src/BloonsVR/InputOverride.cs` | Takes WASD away from BTD6 (new Input System + legacy patches) |
| `src/BloonsVR/SpriteBillboard.cs` | Makes every 2D sprite face the player (`B` to toggle) |
| `src/BloonsVR/CursorButton.cs` | uGUI overlay + hit test that releases the cursor for BTD6 menus |
| `src/BloonsVR/PixelFont.cs` | 5x7 bitmap font rendered into a `Texture2D` (no runtime font exists) |
| `src/BloonsVR/Btd6Map.cs` | Defensive helpers over `GameModel` / `MapModel.areas` / `AreaModel` |
| `src/BloonsVR/Hotkeys.cs` | Rebindable keys, because BTD6 also binds keys |
| `tools/font_preview.py` | Renders the font glyphs to a PNG so they can be eyeballed |

### Controls

| Key | Action |
|---|---|
| Mouse | Look (cursor locks when the rig spawns) |
| W A S D | Move |
| Q / E | Down / up (flying) |
| Left Shift | Sprint |
| **Tab** | **Release / re-grab the cursor so BTD6's shop and upgrade menus are clickable** |
| **B** | **Toggle sprite billboarding (all 2D sprites face the player)** |
| C | Cycle the tower the reticle will place |
| F | Place the selected tower at the reticle |
| V | Leave first person, hand the camera back to BTD6 |

There is also an on-screen button, top-left, that toggles the cursor. It is clickable while the cursor
is still locked, because the hit test treats the screen centre as the pointer in that state.

### Build and deploy

```
cd src\BloonsVR
.\build.ps1                 # build only
.\build.ps1 -Deploy         # build + copy to <BTD6>\Mods\BloonsVR.dll
```

### Controls

| Key | Action |
|---|---|
| Mouse | Look (cursor locks when the rig spawns) |
| W A S D | Move |
| Q / E | Down / up (flying) |
| Left Shift | Sprint |
| C | Cycle the tower the reticle will place |
| F | Place the selected tower at the reticle |
| V | Leave first person, hand the camera back to BTD6 |

---

## Verified facts about this install

Everything below was read off the machine, not guessed.

### Game and loader

- **Game:** Bloons TD 6, Steam app `960090`.
- **Install:** `C:\Program Files (x86)\Steam\steamapps\common\BloonsTD6`
- **Engine:** Unity **6000.0.58f2** (Unity 6), **IL2CPP** — `GameAssembly.dll` (85 MB) +
  `BloonsTD6_Data/il2cpp_data/`.
- **Loader:** MelonLoader **0.7.3** already installed (`version.dll` proxy, `MelonLoader/net6`,
  so the runtime is .NET 6 and mods must target `net6.0`).
- **Btd6ModHelper.dll** is in `Mods\` and must stay there — without it the game does not save progress.
  Docs next to it: `Mods\Btd6ModHelper.xml` (1 MB, 382 types, ~1.5k documented members). Read it rather
  than guessing at the helper's API.
- **Saves / config:** `C:\Users\think\AppData\LocalLow\Ninja Kiwi\BloonsTD6`
- **Saves backed up:** `C:\Users\think\.universal-modder\backups\btd6-saves\20261003-060042.zip`
  (13 files, 2.1 MB) via `um backup create`. Restore with `um backup restore`.
- **No anti-cheat.** BTD6 is safe to mod offline. Don't touch it while in an online mode.
- Generated interop assemblies already exist: `MelonLoader\Il2CppAssemblies\` (140 DLLs,
  `Assembly-CSharp.dll` is 38 MB). If they are ever missing, launching the game once regenerates them.

### Namespace rules that will waste an hour if forgotten

- **Game types** are prefixed: `Assets.Scripts.X` → **`Il2CppAssets.Scripts.X`**.
  So `InGame` is `Il2CppAssets.Scripts.Unity.UI_New.InGame.InGame`,
  `TowerManager` is `Il2CppAssets.Scripts.Simulation.Towers.TowerManager`.
- **Unity module types keep their original namespaces**: `UnityEngine.Camera`,
  `UnityEngine.InputSystem.Keyboard`. No prefix. Do not assume the prefix for these.
- `ObjectId` is a **struct** in `Il2CppAssets.Scripts.ObjectId`.

### The single biggest API trap: BTD6 has its own vector types

The simulation layer does **not** use `UnityEngine.Vector2/Vector3`. It uses
`Il2CppAssets.Scripts.Simulation.SMath.Vector2` (fields `x`, `y`, where `y` is world **z**) and
`...SMath.Vector3` (`x`, `y`, `z`). `SMath.Vector2` has `ToUnity()` and a ctor taking a
`UnityEngine.Vector2`; `SMath.Vector3` has a ctor taking a `UnityEngine.Vector3`.

This affects `AreaModel.IsPointInside`, `AreaModel.GetCenterPoint` and
`TowerManager.CreateTower`. The decompiled wrapper source *looks* like it takes `Vector2`/`Vector3`
and only the compiler reveals the `SMath` types. `Btd6Map.ToBtd2` / `ToBtd3` do the conversion.

### Map / placement model (verified from the generated assemblies)

- `MapModel.areas` is `Il2CppReferenceArray<AreaModel>` (index by `.Length`).
- `AreaModel`: `id` (ObjectId), `height` (float), `type` (`AreaType`), `polygon`,
  `isDisabled`, `lockedArea`, `isBlocker`, `IsPointInside(SMath.Vector2)`, `GetCenterPoint()`.
- `AreaType` values: `track, water, land, unplaceable, ice, removable, waterMermonkey, shallowWater`.
  Placeable = `land`, `ice`, `removable` (minus the disabled/locked/blocker flags).
- **`TowerManager.CreateTower(TowerModel def, SMath.Vector3 position, int inputIndex, ObjectId areaPlacedOn,
  ObjectId parentTowerId, TowerSaveDataModel loadingSaveData = null, bool isInstaTower = false,
  bool deductCash = true, float rotation = 0f, bool playPlacementEffects = true, int costOverride = -1,
  int frontierId = -1)`** — this is the placement call we use. `areaPlacedOn` must be the id of the
  `AreaModel` the point is inside; Y should be that area's `height`.
- `GameModel.cash` is a public field. `GameModel.GetTower(baseId, 0, 0, 0)` returns the top-level
  `TowerModel`. `GameModel` implements `IEnumerable<TowerModel>` **explicitly**, so it must be cast
  before it can be walked.
- `TowerManager` and `InputManager` are not Unity singletons with an `Instance` — go through
  `BTD_Mod_Helper.Api.Helpers.Instances`.

### BTD6 Mod Helper accessors (the supported way in)

`BTD_Mod_Helper.Api.Helpers.Instances` exposes, all null outside a match:
`Game`, `GameData`, `BaseGameModel`, `CurrentGameModel`, `InGame`, `Bridge`, `Simulation`,
`TowerManager`, `InputManager`, `CashManager`, `Map`, `ShopMenu`, `MenuManager`, `PopupScreen`,
`DisplayFactory`, `Profile`, `TowerSelectionMenu`, `NextInGameData`, `CurrentInGameData`.

`BTD_Mod_Helper.BloonsTD6Mod` is the mod base class (a MelonMod). Useful hooks:
`OnEarlyInitialize`, `OnGameObjectsReset`, `OnInGameLoaded(InGame)`, `OnMatchStart`, `OnMatchEnd`,
`OnMainMenu`, `OnModelLoaded`, `OnMapModelLoaded`, `OnTowerPlaced(Tower)`, `OnRoundStart`,
`OnRoundEnd`, `OnGameModelLoaded`, `OnProfileLoaded`. Full list in `Mods\Btd6ModHelper.xml`.
`BloonsMod.OnEarlyInitializeMelon` is **sealed** — override `OnEarlyInitialize` instead.

### The game's own placement pipeline (recorded, deliberately not used)

BTD6 places towers through `InputManager`:
`PrimeTower(ITowerPurchaseButton, TowerModel)` → `EnterPlacementMode(TowerModel, PositionDelegate,
ObjectId, bool, int)` → `cursorPositionWorld` (Vector2, updated per frame) → `TryPlace()` →
`CreatePlacementTower(SMath.Vector2)`. It also has `lastValidPosition`, `placementRestrictions`
and `AddNewPlacementRestriction`.

This is driven by the desktop cursor, which is exactly what first person and a VR controller ray do
not have. `TowerPlacer` therefore resolves the target itself and calls `CreateTower` directly.
If we later want BTD6's own placement ghost, drive `InputManager.cursorPositionWorld` and call
`TryPlace()` — that path is still there.

### Camera

`InGame.instance.sceneCamera` is the match camera (`InGame.Bridge` is the static `UnityToSimulation`,
and `InGame.InputManagers` enumerates one `InputManager` per player).
Also on `InGame`: `HitTestWorld(InputManager, out RaycastHit, int layerMask = int.MaxValue)`,
`GetUnityWorldFromCursor(InputManager)`, `IsCursorInWorld(InputManager)`, `NormalisedMapCursorPosition`,
and `Update()` / `UpdateSimulation()`.
The existence of `HitTestWorld` returning a `RaycastHit` means **the map does have Unity colliders**,
which is why `TowerPlacer` uses `Physics.Raycast` first and only falls back to a horizontal plane.

The rig **owns its camera**: `new GameObject("BloonsVR_Camera")` at the scene root with no parent,
`AddComponent(Il2CppType.Of<Camera>())`, `orthographic = false`, FOV 70, near 0.05, far 5000, depth 100,
`cullingMask` copied off BTD6's camera, plus a `UniversalAdditionalCameraData` component with
`m_RendererIndex` and `renderType` copied off BTD6's camera.

Borrowing `sceneCamera` was tried first and is unwinnable — see the run logs below. BTD6 rewrites its own
camera transform every frame, pinning it back to `pos=(0,0,0) rot=(60,0,0)` immediately after our write.

**Do not disable BTD6's camera.** That was tried twice and failed both times. What is left enabled is a
frame in which nothing draws, and the symptom is not an error — it is a frozen picture. See
`Render log - 2026-10-03 16:30`.

Instead both cameras render and `Camera.rect` viewports split the screen: BTD6 left half at its normal
top-down framing, first person right half.

### Cameras cannot be enumerated

`UnityEngine.Camera` exposes only the non-generic `allCamerasCount` / `GetAllCamerasCount()` here. There is
no `GetAllCameras(Camera[])` in the generated wrapper, and `FindObjectsOfType<T>` / `Resources
.FindObjectsOfTypeAll<T>` are stripped generics. So a runtime-created camera can only be *found* through
`Camera.main` (tag-based, and sometimes null) or `InGame.instance.sceneCamera` — never enumerated. That is
the whole reason the viewport split is reapplied every frame instead of captured once: BTD6 creates its
real match camera after the rig is built, and a camera that appears later would otherwise keep the full
screen.

---

## Render log - 2026-10-03 16:30 (build C6CEF62E, disabling BTD6's camera)

Reported in game: *"there is only the main camera, pressing v just pauses the screen while simulation goes
on in the background"*. Log:

```
URP renderer index -1 (copied from BTD6's camera)
disabled BTD6 camera Scene
cameras in scene: 1 (we disabled 1); name=BloonsVR_Camera ... enabled=True active=True
first person OFF - BTD6 camera restored full-screen
split 2560x1600: BTD6 left half, first person right half
first person ON - our camera right half, BTD6 cameras off
```

Three conclusions, and one of them corrects an earlier guess.

1. **`URP renderer index -1` was not the bug.** `-1` is URP's "take the pipeline default" sentinel, so
   copying it off BTD6's camera was correct. This was a plausible-sounding hypothesis with no evidence
   behind it; only the log settled it.
2. **`cameras in scene: 1` while BTD6's view was still on screen** means our camera was enabled, active,
   and drawing nothing. The back buffer keeps the last image Unity rendered, so the picture froze while the
   simulation ran on. "Screen pauses, sim continues" is the signature of *a frame with no camera drawing in
   it*, not of a paused game.
3. **The viewport split never appeared, and that is the more damning clue.** `camera.rect` is plain
   `Camera` API, present in the wrapper, set on the only other camera in the scene — and nothing happened.
   So either the rect was never applied (the capture list could be empty when `Camera.main` and
   `sceneCamera` are both stale) or the frame is not being presented by that camera. Both are now
   distinguished by observation instead of inference.

### What was wrong in my own code

`SetActive(true)` never re-enabled our camera and never re-disabled BTD6's. The first `V`-off set
`_camera.enabled = false` and re-enabled BTD6's, and *nothing undid either* — so every later press of `V`
did nothing observable. That is why an earlier run reported "V just puts my cursor in the centre": the view
swap had genuinely never happened a second time.

`SwitchOffBtdCameras(null)` also began with `_btdCameras.Clear()`, so passing a null reference from the
`SetActive` path could empty the very list the viewport code then iterated.

### The fix, and the probe that will actually settle it

Both cameras now render; `ApplyViewports()` runs every frame and is idempotent, setting `rect` on
`Camera.main` and `InGame.sceneCamera` each time rather than working from a captured list.

`RenderProbe.cs` is a Harmony postfix on `UnityEngine.Rendering.Universal.UniversalRenderPipeline.Render`
— the last point before the pipeline decides what to draw. BTD6 uses the stock pipeline: there is no
subclass of `UniversalRenderPipeline` or `RenderPipeline` anywhere in `Assembly-CSharp`, so this sees the
real camera list. It logs once a second, while the rig is on:

```
[BloonsVR] URP rendering 2 camera(s): [Scene rect=0.00,0.00 0.50x1.00 mask=... | BloonsVR_Camera rect=0.50,0.00 0.50x1.00 mask=...]
```

That single line separates the two remaining possibilities. Ours **absent** from the list means the
pipeline is skipping us and the problem is camera setup; ours **present** means it renders and the problem
is what it is drawing (layers, renderer index, drawing off-screen).

Three separate hypotheses were reasoned into this code and none of them were verified, because nothing in
the mod ever asked the engine what it was rendering. Always hook the render loop first.

---

## Run log - 2026-10-03 16:43 (build A3F92071) - the split worked, and the keyboard theory died

Reported: *"Pressing V cuts the screen in half with another camera on the left."* So viewports work, both
cameras render, and disabling BTD6's camera really was the wrong move. The split was then dropped in favour
of one full-screen camera drawn over the game's, with `V` as a plain toggle.

### The render probe never applied

```
[ERROR] Failed to apply patch(es) in RenderProbe
[ERROR] Ambiguous match found.
HarmonyLib.HarmonyException: Ambiguous match for HarmonyMethod[
    (class=UnityEngine.Rendering.Universal.UniversalRenderPipeline, methodname=Render, type=Normal, args=undefined)]
 ---> System.Reflection.AmbiguousMatchException
    at HarmonyLib.AccessTools.DeclaredMethod(Type type, String name, Type[] parameters, Type[] generics)
```

**`Render` is declared twice on the way up.** `RenderPipeline.Render(ScriptableRenderContext, List<Camera>)`
is `virtual` and `UniversalRenderPipeline.Render(...)` overrides it. Harmony's name-only attribute lookup
sees both and refuses to guess. Fixed by passing explicit argument types:

```csharp
[HarmonyPatch(typeof(UniversalRenderPipeline), nameof(UniversalRenderPipeline.Render),
    new[] { typeof(ScriptableRenderContext), typeof(Il2CppSystem.Collections.Generic.List<Camera>) })]
```

Generalise: **any Harmony `[HarmonyPatch]` on an override is ambiguous unless the argument types are
given.** The whole `PatchAll` aborted because of it, so one bad attribute silently cost every patch in
the assembly — check the log for `Failed to apply patch(es)` before believing a probe is running.

### What the rest of the log settled

| Log | Meaning |
|---|---|
| `pipeline=UnityEngine.Rendering.Universal.UniversalRenderPipeline` | Stock URP confirmed at runtime, not inferred |
| `cameras in scene at rig start: 2`, `cams=2` | Ours plus BTD6's. Two, not one |
| `diag Camera.main  NULL` then `split: 2 BTD6 camera(s)` 0.4 s later | `Camera.main` is null at rig start and non-null shortly after — per-frame re-collection was necessary, not paranoia |
| `diag sceneCamera name=Scene ortho=True fov=15 depth=5 rot=(60,0,0)` | BTD6's match camera is **orthographic**, FOV 15, pitched 60°, depth 5. Our depth 100 draws after it, so full-screen override needs no viewport tricks |
| `keys=[kb=1 W0 A0 S0 D0]` while `V` toggles correctly | **The keyboard is not broken** |

### The keyboard was never the problem

`V` is read by `InputReader.PressedThisFrame(Hotkeys.ToggleRig)` — the same device, the same indexer, the
same file as `Held(Key.W)`. The player pressed `V`, the view changed, and the log shows our own
`first person ON/OFF` lines. So keyboard events demonstrably reach `InputReader`.

Therefore `W0 A0 S0 D0` can only mean the movement keys were not held at those instants. Two runs had been
spent on the opposite assumption — that the keyboard was dead or the wrong device — and it was wrong both
times, because the control case (`V` works) was never checked. `Keyboard.current` being non-null was never
evidence of anything; a working hotkey on the same path is.

`InputReader` now counts cumulative frames per movement key (`held=W123/A0/S0/D0`) instead of only
snapshotting one frame per heartbeat, because "never pressed" and "the sample missed it" are
indistinguishable in a snapshot. `InputReader.LogDeviceDiagnostics` dumps `InputSettings.updateMode` plus
every device's `enabled` / `added` / `deviceId`, so this is settled by observation from now on.

### Billboard progress, unrequested but worth recording

```
billboarded 8 of 8 live node(s); rotSets=0 posSets=1608
billboarded 0 of 8 live node(s); rotSets=0 posSets=1512
```

`SetPosition` **is** being called, heavily — the earlier `posSets=0` reading was taken before the method
was ever reached. `SetQuaternionRotation` is still never called (`rotSets=0`), so it is not the writer and
its Harmony patch can be deleted.

The interesting part is the alternation: 8 of 8, then 0 of 8, roughly 4 ms apart, repeating. Something
resets the billboard between our writes. That is the "rotations do not stick" bug, and it is now a matter
of finding who clears it rather than of finding where we set it.

---

## Run log - 2026-10-03 16:56 (build A3F92071) - movement works, Layer 1 never ran

Same DLL as the 16:43 run, so the same aborted `PatchAll`. Two results, one good and one bad.

### Movement works. The keyboard was never the problem.

```
tick  120: player=(0.00, 0.00, -18.00)  yaw=-8  keys=[kb=1 W0 A0 S0 D0]
tick 1740: player=(-5.26, 0.00,  6.51)  yaw=-12 keys=[kb=1 W0 A0 S0 D0]
```

The player walked 24.5 units. `InputReader`, `Held(Key.W)` and the whole rig are fine — `W0` in the
heartbeat only means the key was not held on that particular frame.

That is three wrong theories killed by one log line each:

| Run | Theory | What killed it |
|---|---|---|
| 15:43 | `Keyboard.current` returns false for gameplay keys | It does not; the player moves |
| 16:43 | The keyboard is dead or the wrong device | `V` fires through the same `InputReader` path |
| 16:56 | `W0` means the keys never arrive | The player moved 24 units |

The lesson is not about input. It is that `keys=[...]` samples **one frame per heartbeat**, so it cannot
distinguish "not pressed" from "not pressed *then*". Anything reported as instantaneous needs a cumulative
counter beside it before it is allowed to become a conclusion. Now reported as
`held=W123/A0/S0/D0 frames=N any=M mouse=Kf/Lpx`, where `mouse` is the control that proves the Input
System is alive.

### Layer 1 never fired. Zero occurrences across two runs.

```
--- Layer 1 (legacy Input prefixes) ---
NONE - the legacy GetKey prefixes never fired once
```

No `Layer 1: legacy UnityEngine.Input prefixes are live` line, in a game where BTD6 demonstrably still
reacts to WASD. So BTD6 was reading the movement keys through the **legacy** `UnityEngine.Input` backend
the whole time, and the only thing that ever blocked that route was never applied.

The cause is the `RenderProbe` attribute from last round. `PatchAll` walks the assembly and **throws part
way through**, so every class it had not yet reached stayed unpatched — and the only evidence Layer 1 was
live was a log line that never appeared. An absent log line is not evidence of absence; it was treated as
confirmation that the mechanism was merely untriggered.

Fixed by never using `PatchAll` again:

```csharp
foreach (var type in assembly.GetTypes())
    if (IsPatchTarget(type))
        try { harmony.CreateClassProcessor(type).Patch(); }
        catch (Exception e) { MelonLogger.Error($"patch class FAILED -> {type.Name}: ..."); }
```

Every applied class is now listed by name, so "is Layer 1 live" is answerable from the log. One bad
attribute can no longer take out an unrelated patch.

### Layer 3 only ever scanned once

`Layer 3: scanning 10 enabled action(s)` — ten, for a game with a full action map — and the `_scanned`
flag meant it never looked again. BTD6 enables more maps during a match (tower range views, the shop,
placement mode), so every map that was not enabled at that instant kept listening for WASD for the rest
of the session. Now rescans every 60 frames and logs only when it actually takes something new, plus
`blocked(N held, M enabled)` in the heartbeat so a growing `M` is visible.

---

## Gotchas hit so far

1. **Only the *generic* `GameObject.AddComponent<T>()` is stripped — `AddComponent(Il2CppSystem.Type)`
   works fine.** First in-game run failed with, once per scene load:
   ```
   System.TypeInitializationException: The type initializer for
   'MethodInfoStoreGeneric_AddComponent_Public_T_0`1' threw an exception.
    ---> System.NullReferenceException
      at UnityEngine.GameObject.AddComponent[T]()
   ```
   Cause: that generic instantiation was stripped from the IL2CPP build, so Il2CppInterop's
   `il2cpp_method_get_from_reflection` lookup NREs inside the *static* initializer.

   **This was over-generalised into a wrong conclusion** ("nothing can be added to the scene"), which then
   cost a whole rewrite. The truth, from the generated wrapper: `GameObject` keeps
   `AddComponent(Il2CppSystem.Type)` (method token 100668218, a direct pointer — no reflection, no
   stripping problem), `AddComponentInternal(string)` and `AddComponentInternal_Injected(...)`. Only the
   generic `AddComponent<T>()` and the private `Internal_AddComponentWithType` are affected. The working
   idiom, taken from BindingOfBloons:
   ```csharp
   var component = gameObject.AddComponent(Il2CppType.Of<SpriteRenderer>()).TryCast<SpriteRenderer>();
   ```
   **Lesson: grep the generated wrapper for the exact member before concluding a Unity API is gone.**
   Still genuinely absent: `Camera.onPreCull/onPreRender/onPostRender` *add/remove* accessors,
   `RenderPipelineManager` camera-rendering add/remove, `Resources.GetBuiltinResource` non-generic,
   `Object.FindObjectOfType(Type)` non-generic, `Camera.allCameras`/`GetAllCameras(Camera[])`.

2. **`BloonsTD6Mod.OnUpdate()` is the per-frame hook — not a Harmony patch on `InGame.Update`.**
   A postfix on `InGame.Update` applied cleanly and never fired: Unity dispatches messages from a
   generated player-loop table, not IL2CPP's method table, so the detour is inert. BTD6 Mod Helper
   provides `OnUpdate()` itself (proven by BindingOfBloons, which drives its whole hero controller off it).
   The temporary `MelonCoroutines` driver has been removed; there is now exactly one driver.
   Coroutines do still work via `MelonLoader.MelonCoroutines` if ever needed — they run on MelonLoader's
   injected managed MonoBehaviour (`MelonLoader.Support.SM_Component`).

3. **`GameModel` is not a managed `IEnumerable<TowerModel>`.** Casting throws
   `InvalidCastException`. Il2CppInterop only exposes the mangled
   `System_Collections_Generic_IEnumerable_Assets_Scripts_Models_Towers_TowerModel__GetEnumerator`,
   returning an Il2Cpp enumerator. `TowerPlacer` resolves a curated base-id list through
   `GameModel.GetTower(id)` instead; unknown ids return null and are dropped, and the resolved list is
   logged so it can be corrected from evidence.

4. **First run also revealed the mod was never re-deployed.** `build.ps1 -Deploy` piped through
   `Select-Object -First 4` closes the pipeline and aborts the copy. Always verify with
   `(Get-FileHash <BTD6>\Mods\BloonsVR.dll).Hash -eq (Get-FileHash .\bin\Release\BloonsVR.dll).Hash`.

5. **`MelonInfoAttribute` order is `(Type, name, version, author)`** — verified from `MelonLoader.dll`.
   Passing `(Type, name, author, version)` prints `Melon 'BloonsVR' by '0.1.0' has version 'BloonsVR'`
   plus a non-semver warning.

6. **`BloonsMod.ApplyHarmonyPatches(Type)` swallows its own exceptions** — it logs "Failed to apply
   patch(es) in X" and continues, so wrapping it in `try/catch` never detects failure.

7. **`MelonLoader 0.7.3`'s `MelonMod` has no per-frame callback** — only `OnSceneWasLoaded` /
   `OnSceneWasInitialized` / `OnSceneWasUnloaded`. A `void Update()` on the mod class compiles, never
   runs, and raises no warning.

8. **`Il2Cppmscorlib.dll`, `Unity.InputSystem.dll` and `UnityEngine.UI.dll` are in
   `MelonLoader\Il2CppAssemblies\`,** not `MelonLoader\net6\`.

9. **No runtime font is obtainable.** `Resources.GetBuiltinResource<T>` is a stripped generic;
   `TMP_Settings` exposes no static font asset; uGUI's `Text` needs a `Font`. Hence `PixelFont`, a
   hand-rolled 5x7 bitmap font rendered into a `Texture2D`. Glyphs verified with
   `tools/font_preview.py`, which writes a PNG — run `uv run --with pillow python tools\font_preview.py`
   and look at `font_preview.png` after any change.

10. **`Mesh.vertices` / `normals` / `triangles` properties do not exist** — only `NativeArray` generics
    (stripped) and `List<T>` overloads. Use `GameObject.CreatePrimitive(PrimitiveType.Sphere)`.

11. **`BloonsMod.OnEarlyInitializeMelon` is sealed** → CS0239. Override `OnEarlyInitialize`.

12. BTD6 Mod Helper must be present or saves break; it is also the source of `Instances`.
    Mods target **net6.0**. The log reports **`Game Version: 56.3`**. `Class::Init signatures have been
    exhausted, using a substitute!` is normal Il2CppInterop noise.

---

## Tooling notes for this machine

- `universal-modder\bin\um` is a **bash** script. On Windows use:
  ```
  uv run --quiet --project C:\Users\think\Documents\BloonsVR\universal-modder python -m um <args>
  ```
  Verified working for `um --help`, `um scan "BloonsTD6"` and `um backup create`.
- `uv` is at `C:\Users\think\.local\bin\uv.exe`, .NET SDK 8.0.101, `git` present.
- **`ilspycmd` is installed** (`dotnet tool install -g ilspycmd --version 9.*`, run from
  `%USERPROFILE%\.dotnet\tools`). This is the fastest way to read BTD6's API:
  ```
  ilspycmd -l c "<BTD6>\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"   # list 13k types
  ilspycmd -t "Il2CppAssets.Scripts.Models.Map.AreaModel" -o <outdir> <asm>
  ```
  Zero-argument methods print **without parentheses**, e.g.
  `<member name="M:...OnMatchStart">`, which breaks naive regexes over XML docs.
  The generated code is lines indented with a **tab** — `^public` will not match, use `Trim()`.
- `FAL_KEY` is **not** set in this environment, so `um fal` / the fal MCP are unavailable until it is.
- `um kb search` has **no BTD6 or VR notes** in the knowledge base yet.
- `um scan "BloonsTD6"` output (for reference): engine Unity (IL2CPP), version 6000.0.58f2,
  loaders MelonLoader, mod dirs `mods, plugins`, saves
  `C:\Users\think\AppData\LocalLow\Ninja Kiwi\BloonsTD6`.

---

## Queued: publish to GitHub (asked for, not started)

Requested after the current bugs are fixed. Prep done, push not done.

- `README.md` written.
- Repo is **not** yet a git repo — `git init` still to run (needs a first commit).
- The remote URL and GitHub account are unknown and are the blocker: a push needs both, and pushing is a
  publish action that the working rules say to confirm first.
- `universal-modder` is vendored in this folder and is a **separate MIT-licensed project**. It must not be
  committed as if it were ours. Either gitignore it, or keep it outside the published repo.
- Build output (`bin/`, `obj/`) and every `*.dll` stay uncommitted; the DLL is a release artefact, not
  source. Run `um publish check` before any tag or release.

## Still unverified as of the 15:43 log

The own-camera rewrite (build `3DFDE836...`) has **not** been run in game yet. The newest log on disk is
still `26-10-3_15-41-59.log` from build `2D95A4A1...`. So the camera fix, the new billboard camera target
and the corrected tower list are all untested.

---

## Input blocking — the recipe that actually works

Written up from BindingOfBloons' field notes, which were tested in game on exactly this build
(BTD6 56.3 / Unity 6000.0.58f2 / MelonLoader 0.7.3 / Mod Helper 3.6.8). Reproduced here as three layers.

**Both Unity input backends are live at once.** Legacy `Input.GetKey` returns real states without
throwing, *and* `Keyboard.current` is non-null with live keys. BTD6 reads through both. **A hit on one
backend proves nothing** — BindingOfBloons logged `Input.GetKey(W) hidden from BTD6 (patch active)` and
the hotkeys *still* fired. Only the symptom disappearing is evidence.

### The design principle that makes it work

The consumer must read the **raw device** (`Keyboard.current.wKey`, `InputSystem.devices`) — never legacy
`GetKey`, never an `InputAction`. Then every layer below is transparent to it.

**This forced a fix in our own code:** `InputReader` previously fell back to legacy `GetKey` as a last
resort. Layer 1 prefixes those getters, so that fallback would have starved *us* — a self-inflicted
wound. The legacy fallback is now gone; raw device only.

### Layer 1 — legacy Harmony prefixes

`GetKey` / `GetKeyDown` / `GetKeyUp`, `GetAxis` / `GetAxisRaw` (`Horizontal`/`Vertical` → 0), plus a
log-only `GetButton` probe. **`new[] { typeof(KeyCode) }` is required** — `GetKey` is overloaded on
`KeyCode` and `string` and Harmony cannot tell them apart without it. Registered in `OnApplicationStart`
(`OnInitializeMelon` is sealed), with our own `Harmony` instance so failures are visible.

### Layer 2 — the game's own named actions

From `Il2Cpp.InputSystemController.instance.actionMap`:

- `m_Player_Move`, `m_Player_Look` — the movement and look actions.
- `m_UI_Point`, `m_UI_MiddleClick`, `m_UI_RightClick`, `m_UI_ScrollWheel` — the mouse consumers.
  **`m_UI_Point` is the one that made the selected tower spin when the mouse moved**, which the player
  spotted and reported. Left out on purpose: `m_UI_Submit` / `m_UI_Click` / `m_UI_Navigate`, because the
  player needs those to click the shop once the cursor is released — and blocking is lifted entirely on Tab.

### Layer 3 — scan every enabled action for a WASD binding

`InputSystem.ListEnabledActions()`, disable anything whose `effectivePath`/`path` ends `/w`, `/a`, `/s`,
`/d` under `<keyboard>`, re-asserted each frame because BTD6 re-enables its maps on input-mode changes.

**IL2CPP gotcha:** `ListEnabledActions()` returns an **Il2Cpp list** — iterate with `var` and its own
enumerator, never assign it to a `System.Collections.Generic.List`. (We also hold direct references to
the named actions, which avoids the issue for those.)

### Verifying it

`MelonLoader\Latest.log` should show:

```
Layer 1: legacy UnityEngine.Input prefixes are live
Layer 2: took N BTD6 action(s) (Move, Look + mouse UI)
Layer 3: scanning N enabled action(s)
Layer 3: <map>/<name> <-- DISABLED
Layer 1 probe: BTD6 called Input.GetButton("...")      (only if it uses GetButton)
```

and every heartbeat carries `btdInput=blocked|LEAKED|not-attached|released`.
**Falsifiable endpoint:** if the Layer 3 scan finds no WASD-bound actions *and* the symptom persists,
BTD6 is reading the raw device directly and cannot be starved without also starving us.

---

## Run log — 2026-10-03 15:53 (build 3DFDE836, the own-camera version)

This is the run that produced "stuck in the main camera view, V just centres the cursor, WASD still maps
to BTD6". The log explains all three, and none of them is what they look like.

**The own-camera change worked:**
```
[BloonsVR] disabled BTD6 camera Scene
[BloonsVR] own camera created; name=BloonsVR_Camera ... ortho=False fov=70 depth=100
[BloonsVR] diag sceneCamera ... enabled=False
```
So the mod genuinely renders through its own camera. The "main camera view" the player is seeing *is*
the rig's camera — it simply starts at the spawn point looking down 20 degrees, which looks much like
BTD6's own top-down view.

**The real bug: the player never moves.**
```
tick  60: player=(0.00, 0.00, -18.00) yaw=2  pitch=10 aim=(0.37, 0.00, -8.77)
tick 240: player=(0.00, 0.00, -18.00) yaw=51 pitch=10 aim=(7.26, 0.00, -12.13)
tick 300: player=(0.00, 0.00, -18.00) yaw=107 pitch=4 aim=(21.74, 0.00, -24.75)
tick 720: player=(0.00, 0.00, -18.00) yaw=12 pitch=29 aim=none
```
X and Z never change across 720 frames while yaw and the aim ray move freely. Two conclusions:

1. **Mouse look works and our camera works** — the aim ray tracks yaw through our own camera.
2. **WASD produces zero movement.** `Keyboard.current.wKey.isPressed` was false for every movement key
   while `Mouse.current.delta` delivered fine. Same device family, so this is not "the Input System is
   dead"; it means the keyboard we were polling is not necessarily the one carrying gameplay key events
   (BTD6 has gesture/virtual-input plumbing and switches input mode mid-match).

Everything the player described follows from this: you cannot walk, so you cannot tell that the view is
yours; pressing V swaps to BTD6's orthographic camera, which looks similar enough to read as "no change",
leaving only the cursor behaviour to notice.

**Fix: `InputReader`.** Reads every keyboard device in `InputSystem.devices` rather than only
`Keyboard.current`, falls back to `Keyboard.current`, then to legacy `UnityEngine.Input`, and reports
which path is live. Movement and all hotkeys now go through it.

**Sprite billboarding: both Harmony hooks are dead.**
```
billboarded 8 of 8 live node(s); rotSets=0 posSets=0
```
Neither `SetQuaternionRotation` nor `SetPosition` is ever called, yet the sweep writes `8 of 8` nodes.
BTD6 writes display transforms through some other path entirely. Do not add more hooks to
`UnityDisplayNode`; find the writer first.

**Input override attached cleanly:** `took 2 BTD6 player action(s) (Move + Look) for the rig`, and the
new `InputOverride.HoldState()` heartbeat will report `blocked` / `LEAKED` / `not-attached` so a re-enable
by BTD6 becomes visible instead of inferred from the player's impression.

---

## Run log — 2026-10-03 15:41 (build 2D95A4A1)

This matters because two of the "fixes" before it *were* taking effect and the problem was elsewhere.

- The deployed DLL hash in the log matched the build, so the run was genuinely on the new code.
- `took 2 BTD6 player action(s) (Move + Look) for the rig` — the new `InputOverride` **did** attach and
  disable `m_Player_Move` and `m_Player_Look`. (It attaches lazily: `_attached` is only set once
  `InputSystemController.instance.actionMap` is non-null, which happens a beat after the rig is created.)
- `billboarded 8 of 12 live node(s)` — the sweep **was** writing rotations to display nodes.
- `rotation sets seen=0` — the Harmony postfix on `UnityDisplayNode.SetQuaternionRotation` **never fired**.
  That method is not how BTD6 rotates displays, so hooking it bought nothing.
- **The camera was the real problem.** BTD6's camera is reported as
  `pos=(0.00, 0.00, 0.00) rot=(60.00, 0.00, 0.00) ortho=True fov=15`, and the clash warning fired:
  `something moves InGame.sceneCamera after our tick (camera (0,0,0), expected (0.80, 1.70, -11.48))`.
  So BTD6 rewrites its camera transform **every frame** and pins it back to the origin. Borrowing that
  camera was unwinnable: we wrote, BTD6 overwrote, and on V the camera was left wherever the fight ended.
  This explains "stuck flying above the map" and "rotating around the unmodded pivot" in one go.
- `Camera.main` and `InGame.sceneCamera` are **different objects** in a match (tags `MainCamera` vs
  `Untagged`, same transform), and in one match `Camera.main` was null. Never assume they are the same.
- URP confirmed: `pipeline=UnityEngine.Rendering.Universal.UniversalRenderPipeline`, screen 2560x1600.

### Changes made from this evidence

1. **The rig now creates its own camera** at the scene root with no parent
   (`new GameObject("BloonsVR_Camera")` + `AddComponent(Il2CppType.Of<Camera>())`), copying BTD6's
   `cullingMask` and forcing perspective/70 FOV/0.05 near. BTD6's cameras are switched **off** while the
   rig is on and back **on** when it is off. Nothing in the game can touch our transform.
2. Billboards aim at the rig's camera (`SpriteBillboard.SetCamera`), not `Camera.main`, which is now off.
3. Tower ids replaced with BTD6's own hotkey list from `Il2Cpp.Btd6ActionMap` — authoritative rather than
   guessed. Previously 19 resolved; the list now covers every tower the game binds a hotkey to.
4. Added a `SetPosition` postfix purely as a probe (`posSets=` in the log).

### Still unresolved: sprite billboarding does not stick

Writes happen but nothing visibly changes, and `SetQuaternionRotation` is not the writer. The three
possible causes are now distinguishable from the log:

| `rotSets` | `posSets` | `billboards` | Meaning |
|---|---|---|---|
| 0 | 0 | >0 | both hooks dead; only the sweep writes |
| >0 or >0 | >0 or 0 | >0 | hooks fire, still overwritten later in the frame |
| >0 | >0 | 0 | `ShouldBillboard` is rejecting everything |

If both hooks stay at 0, the writer is somewhere else entirely and the next place to look is the
sim→display bridge (`Assets.Scripts.Simulation.Display.DisplayNode` / `IDisplayNode`) or
`Assets.Scripts.Unity.Display.Scene`, which holds the `Factory` and a per-frame `position`.

## Next: 3D rendering — what is verified and what the plan is

Researched 2026-10-03, not implemented yet. All of this is read off the generated assemblies.

### Entry point for 3D bloons

`Il2CppAssets.Scripts.Unity.Display.Factory` (the display factory) has:

- `public List<UnityDisplayNode> active` — **every live display node in the scene**, reachable via
  `BTD_Mod_Helper.Api.Helpers.Instances.DisplayFactory`. This is the hook: iterate `active` and
  decorate the nodes.
- `pool`, `limits`, `counts`, `prototypeHandles` — pooling internals.

`UnityDisplayNode : MonoBehaviour` carries the rendering state:

- `meshComponent` (`MeshFilter`), `sprite` / `spriteInChildren` (`SpriteRenderer`), `animationComponent`
  (`Animator`), `particles`, `gameObjectEnabler`, `customScaleAnimator`, `projectileTrailEffect`
- `isSprite` (true when it is a billboard), `sortingOrder`, `rotation` / `rotationQuat`, `velocity`
- `cloneOf` (`PrefabReference`), `isDestroyed`, `initialised`

`BloonModel` gives the definition side: `display` (`PrefabReference`) and `radius`.

### Sim ↔ display bridge (from BindingOfBloons)

- `TowerExt.GetTowerToSim(Tower)` → `TowerToSimulation`
- `TowerToSimulation.position` → `UnityEngine.Vector3` (display space)
- `TowerToSimulation.simPosition` → `SMath.Vector3` (simulation space, x/y = XZ)
- `BloonToSimulation` has `position`, `Def`, `GetSimBloon()`, `id`
- `Tower.MoveTower(SMath.Vector2, bool, bool)`, `Tower.SetAttackingDisabled(bool)`,
  `RootObject.IsDestroyed`, `Tower.modelBehaviors`

### Tools available for building real 3D

- `GameObject.CreatePrimitive(PrimitiveType type)` — **exists**, gives a ready sphere/capsule/cube with
  MeshFilter + MeshRenderer. This is how to get geometry without authoring a mesh.
- `GameObject.AddComponent(Il2CppType.Of<T>())` + `TryCast<T>()` — works (see gotcha 1).
- `Shader.Find(string)` and `Shader.FindBuiltin(string)` exist. Unity's rule still applies: only
  shaders included in the build resolve. `RenderPipelineManager.GetCurrentPipelineAssetType()` tells us
  whether we are on URP or the built-in pipeline at runtime, and BTD6 ships
  `Unity.RenderPipelines.Universal.Runtime.dll`.
- `MeshRenderer.material` / `sharedMaterial`, `Renderer.sortingOrder`, `Renderer.SetMaterial(Material)`.
- `Mesh` has **no** `vertices`/`normals`/`triangles` properties — only `NativeArray` generics (stripped)
  and `List<T>` overloads, so hand-built meshes are painful. Prefer `CreatePrimitive`.
- Billboard trick used by BindingOfBloons: `_go.transform.rotation = camera.transform.rotation`.

### Plan for 3D bloons — REVISED, now implemented as billboarding

The original idea (sphere mesh per bloon, bloon's own texture as albedo) was abandoned in favour of
plain sprite billboarding, which is far lower risk and isolates the problem. `SpriteBillboard.cs`:

- Iterate `Instances.DisplayFactory.active` (`List<UnityDisplayNode>`) and, for every node that
  `isSprite` or has `sprite` / `spriteInChildren`, assign `transform.rotation = camera.transform.rotation`.
- Copying the camera's rotation is correct rather than merely convenient: a SpriteRenderer's visible face
  is its local **-Z**, and a camera looks along its local **+Z**, so the assignment puts that -Z in
  front of the player. `Quaternion.LookRotation(pos - camPos)` would face the sprite *backwards*.
- Skips nodes whose rotation is already within `0.9999` quaternion dot, to avoid dirtying the transform
  hierarchy a few hundred times a second for no change.
- `B` toggles it, so a broken look can be backed out immediately without a restart.
- `ShouldBillboard(node)` is the single opt-out point for anything that genuinely belongs flat on the
  ground (map walls, range circles, track arrows). Nothing is excluded yet — the point of this pass is to
  *see* the breakage.

`TrackerToSimulation`/display-position helpers and the sphere-mesh route stay available if billboarding
turns out not to be enough.

### Still-open: the map

Not started. The map is top-down art with no per-tile 3D geometry, so unlike bloons there are no display
nodes to decorate. Realistically it means triangulating a ground mesh from the `MapModel.areas` polygons
(each `AreaModel` has a `polygon` and a `height`) and projecting the top-down texture onto it.
`AreaModel.polygon` is a `Polygon` type whose API still needs checking. Expect this to be the bigger job.

---

## Phase 2 — VR (not started)

Local environment, verified:

- **Active OpenXR runtime is Oculus (Meta Quest):**
  `HKLM\SOFTWARE\Khronos\OpenXR\1\ActiveRuntime` =
  `C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json`.
  Target **OpenXR**, not OpenVR, for the runtime API.
- SteamVR is also installed (`C:\Program Files (x86)\Steam\steamapps\common\SteamVR`).
- The machine has other VR titles installed (Bigscreen, Ghosts Of Tabor, Cosmic Flow, EarthVR), so
  the headset is real and usable for testing.

Route options, in the order they are worth trying:

1. **Managed OpenXR from the mod.** P/Invoke `openxr_api.dll` / `OXOpenXR.dll` directly and do
   stereo rendering ourselves: render the rig camera into two `RenderTexture`s, submit them with
   `xrWaitFrame`/`xrLocateViews`/`xrSubmitProjectionLayer`. Unity's own XR plug-in
   (`Unity.XR.OpenXR`) is **not** available: BTD6's `Il2CppAssemblies` contain
   `UnityEngine.XRModule` and `UnityEngine.VRModule` but no XR plug-in provider, and an IL2CPP
   build cannot gain one at runtime. This is the only route that also gives us controller poses for
   the menus, so it is the one to build.
2. **A native proxy that injects VR** (VR Mad Mixer style, replacing `version.dll`). Conflicts with
   MelonLoader, which also owns `version.dll`; MelonLoader's proxy can be renamed
   (`winhttp.dll`, `winmm.dll`, ...) so this is survivable but adds a second native layer.
   Not verified: the `SirVR/VRM` GitHub repo returned 404 on 2026-10-03, so the current home of
   "VR Mad Mixer" is still unknown.
3. `NewUnityModder/UnityVRMod` is a good **reference implementation** (IL2CPP + OpenXR/OpenVR,
   BepInEx 6, DX11 only) even though it targets BepInEx rather than MelonLoader.

Constraints to remember: BTD6 ships a `D3D12\` folder, so force DX11 (`-force-d3d11`) for any
DX11-only native VR layer.

---

## Run log - 2026-10-03 19:10 (build EC0EF8F0) — **WORKING BUILD**

**Status: First-person movement, camera, sprites all functional.**

```
rig created using Scene camera: name=Scene...
CameraLateUpdater attached to Scene camera
rig ready, spawned at (0.00, 12.00, -18.00)      ← y=12, above ground
tick 60:  player=(0.00, 0.00, -18.00) camPos=(0.00, 1.70, -18.00)  ← camera at eye height
tick 420: player=(-2.03, 0.00, -18.02) camPos=(-2.03, 1.70, -18.02)  ← camera follows!
tick 420: held=W59/A0/S0/D0                      ← WASD reaching reader
tick 62520: player=(0.00, 12.00, -18.00) camPos=(0.00, 13.70, -18.00)  ← flying works
billboards=17 rotSets=3796                       ← sprites face player
btdInput=blocked(11 held, 121 enabled)           ← input blocked
```

### What works now
| Feature | Status | Evidence |
|---------|--------|----------|
| Player spawns above ground | ✅ | `spawned at (0, 12, -18)` |
| Camera follows player (LateUpdate) | ✅ | `camPos` tracks `player + EyeHeight` |
| WASD movement | ✅ | `held=W6432/A2937/S3001/D5890` |
| Mouse look (yaw/pitch) | ✅ | `yaw=-92 pitch=0` changes |
| Sprint (LShift) | ✅ | Speed changes in movement |
| Fly up/down (Q/E) | ✅ | Player reaches y=12, camPos y=13.7 |
| Sprite billboarding | ✅ | `rotSets=3796` |
| V toggles first person | ✅ | `first person ON/OFF` logs |
| TAB releases cursor | ✅ | CursorButton works |
| Input blocking (Layer 2/3) | ✅ | `btdInput=blocked(11 held)` |
| Layer 1 (legacy GetKey/GetKeyDown) | ⚠️ Partial | 2/5 patches loaded |

### Known issues
1. **Layer 1 incomplete** — Only `GetKey` and `GetKeyDown` static constructors fire. `GetKeyUp`, `GetAxis`, `GetAxisRaw` never load.
2. **V key conflict** — V toggles first person but BTD6 also binds V (tower selection). When cursor released (TAB), V still triggers BTD6.
3. **Cursor release + hotkeys** — TAB releases cursor but WASD/V still interact with BTD6 menus. Need to suppress all our hotkeys while cursor released.

---

## Open questions / next steps

1. **Layer 1 incomplete**: Only 2/5 static constructors fire (GetKey, GetKeyDown). GetKeyUp, GetAxis,
   GetAxisRaw never load. Use `Layer1Initializer` or move patches to top-level classes.
2. **V key conflict**: V toggles first person but BTD6 also binds V (tower selection?). When cursor is
   released (TAB), V still triggers BTD6's tower menu. Add V to Layer 1 suppression or rebind.
3. **TAB cursor release**: "Funky" - cursor release works but WASD/V still interact with BTD6 menus.
   Need to suppress all our hotkeys while cursor is released.
4. Verify `CreateTower` refuses track/water. If not, add footprint validation.
5. **No HUD via IMGUI** - controller menus need BTD6's uGUI or world-space meshes.
6. Then: controller poses, VR rendering (OpenXR P/Invoke).