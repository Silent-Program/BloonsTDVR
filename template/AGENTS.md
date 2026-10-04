# AGENTS.md — BloonsVR

BTD6 VR mod. A MelonLoader mod that puts the player inside a Bloons TD 6 match: first-person
movement plus screen-centre raycast tower placement now, VR and controller UI next.

**Read `MODLOG.md` before changing anything.** It holds the verified API surface, the gotchas
already paid for, and the open questions. This file is the short version.

---

## Workflow Rules (from UniversalModderPriv template)

### Branch Discipline
- **No direct commits to `main`** — create a plan branch first (`plan/<short-slug>`).
- **All merges to `main` via PR** — no fast-forward, no direct push. Require user approval.
- Delete branch after merge.

### Commit Rules
- One logical change per commit (atomic, revertible).
- Commit messages: `<type>(<scope>): <imperative summary>` + body (what & why).
- Types: `feat`, `fix`, `refactor`, `docs`, `chore`, `test`, `plan`.

### Universal Modder Integration
- Primary tool for game modding: `universal-modder` (from `tools/universal-modder/` submodule).
- Use for: file patching, archive handling, format conversion, mod packaging, deployment.
- Verify `universal-modder --version` matches `configs/tools.toml` before executing.

---

## Layout

- `src/BloonsVR/` — the mod:
  - `BloonsVRMod.cs` — entry point + per-frame orchestrator, driven by `OnUpdate()`. Harmony patches
    register here too, in `OnApplicationStart`.
  - `PlayerRig.cs` — player state in managed code and the rig's own unparented camera.
  - `TowerPlacer.cs` — screen-centre raycast → `TowerManager.CreateTower`, plus the V/Tab/B/C/F hotkeys.
  - `InputOverride.cs` — the three-layer WASD/mouse block and the Harmony prefixes for it.
  - `InputReader.cs` — raw-device key reads. The only place our input comes from.
  - `SpriteBillboard.cs` — makes 2D sprites face the player (`B` to toggle). **Does not stick yet.**
  - `DisplayRotationPatch.cs` — Harmony hooks probing how BTD6 writes display transforms.
  - `RenderProbe.cs` — Harmony postfix on `UniversalRenderPipeline.Render`; reports which cameras Unity is
    actually asked to draw. The first thing to read when the view is wrong.
  - `CursorButton.cs` — uGUI overlay that releases the cursor for BTD6's menus.
  - `PixelFont.cs` — 5x7 bitmap font rendered into a `Texture2D`; no runtime font exists.
  - `Btd6Map.cs` — defensive helpers over `GameModel` / `MapModel.areas` / `AreaModel`.
  - `Hotkeys.cs` — rebindable keys, because BTD6 also binds keys.
- `tools/font_preview.py` — renders the font glyphs to a PNG so they can be eyeballed.
- `tools/universal-modder/` — Universal Modder submodule (not gitignored).
- `MODLOG.md` — the journal. Update it with every change and every new finding.
- `configs/games.toml` — game install paths (BTD6 registered here).
- `configs/tools.toml` — tool versions (universal-modder pinned).

---

## Commands

```powershell
cd src\BloonsVR
.\build.ps1                 # build only
.\build.ps1 -Deploy         # build + copy BloonsVR.dll into the game's Mods folder
dotnet build -c Release     # equivalent, no deploy
```

The game does **not** need a rebuild; drop the DLL in `Mods\` and start BTD6.

```powershell
uv run --quiet --project C:\Users\think\Documents\BloonsVR\universal-modder python -m um <args>
```

Useful: `scan "BloonsTD6"`, `backup create`, `backup restore`, `win shot`, `kb search`.

---

## Read the game before guessing at its API

`ilspycmd` is installed (`%USERPROFILE%\.dotnet\tools`, add it to `$env:Path`). BTD6 is IL2CPP, so
this is the source of truth:

```powershell
$asm = "C:\Program Files (x86)\Steam\steamapps\common\BloonsTD6\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"
ilspycmd -l c $asm                                                      # ~13k types
ilspycmd -t "Il2CppAssets.Scripts.Models.Map.AreaModel" -o out $asm      # one type's API
```

- Types are **indented with a tab** — `Trim()` before matching, `^public` will not match.
- Zero-argument methods appear **without parentheses**. Regexes over XML docs break on this.
- Bodies are stubs; only signatures and constants are real. That is usually enough.
- `Mods\Btd6ModHelper.xml` (1 MB) documents the whole mod-helper API — read it, don't guess.

---

## Environment (verified — do not re-derive)

| | |
|---|---|
| Game | Bloons TD 6, Steam app `960090` |
| Install | `C:\Program Files (x86)\Steam\steamapps\common\BloonsTD6` |
| Engine | Unity **6000.0.58f2**, **IL2CPP** |
| Loader | MelonLoader **0.7.3**, already installed (`version.dll` proxy) |
| Target framework | **net6.0** (MelonLoader 0.7.3 runs on .NET 6) |
| Saves | `C:\Users\think\AppData\LocalLow\Ninja Kiwi\BloonsTD6` |
| Saves backup | `C:\Users\think\.universal-modder\backups\btd6-saves\` via `um backup` |
| Log | `<BTD6>\MelonLoader\Logs\Latest.log` |
| Anti-cheat | none — safe offline, but never mod while in an online mode |

**`Btd6ModHelper.dll` must stay in `Mods\`.** Without it BTD6 does not save progress.

---

## Traps that will cost you an hour each

1. **Only the *generic* `GameObject.AddComponent<T>()` is stripped. The `Il2CppSystem.Type` overload
   works.** `AddComponent<T>()` throws `TypeInitializationException: MethodInfoStoreGeneric_AddComponent_Public_T_0\`1`
   because IL2CPP dropped that generic instantiation, so Il2CppInterop's reflection lookup NREs. But
   `GameObject.AddComponent(Il2CppSystem.Type)` has a direct method pointer and is fine. Use the pattern
   BindingOfBloons uses:
   ```csharp
   var component = gameObject.AddComponent(Il2CppType.Of<SpriteRenderer>()).TryCast<SpriteRenderer>();
   ```
   The same applies to every other generic Unity API. **Check the generated wrapper before assuming
   something is gone** — `ilspycmd -l c "...\UnityEngine.CoreModule.dll"`. An earlier version of this file
   claimed nothing could be added to the scene at all; that was wrong and cost real time.

2. **Use `BloonsTD6Mod.OnUpdate()` as the per-frame hook.** MelonLoader 0.7's `MelonMod` genuinely has no
   per-frame callback, but BTD6 Mod Helper adds `OnUpdate()` itself and other BTD6 mods drive off it.
   A Harmony postfix on a Unity message method (`InGame.Update`) applies without error and **never fires** —
   Unity dispatches messages from a generated table, not IL2CPP's method table.

3. **Namespace prefixes differ by assembly.** Game types: `Assets.Scripts.X` →
   `Il2CppAssets.Scripts.X`. Unity modules keep their original names (`UnityEngine.Camera`,
   `UnityEngine.InputSystem.Keyboard`). Do not assume either way.

4. **BTD6 has its own vector types.** Simulation APIs take
   `Il2CppAssets.Scripts.Simulation.SMath.Vector2/Vector3`, **not** `UnityEngine.Vector2/Vector3`.
   `SMath.Vector2` is `(x, z)`. The decompiled source reads as plain `Vector2`; only the compiler
   reveals the truth. Use `Btd6Map.ToBtd2` / `ToBtd3`.

5. **`GameModel` is not an `IEnumerable<TowerModel>` in the managed sense.** It only *looks* like one;
   Il2CppInterop exposes it as the mangled method `System_Collections_Generic_IEnumerable_..._GetEnumerator`
   returning an Il2Cpp enumerator. Casting throws `InvalidCastException`. Resolve towers via
   `GameModel.GetTower(baseId)` instead — `TowerPlacer` keeps a curated id list for that.

6. **`Il2Cppmscorlib.dll` and `Unity.InputSystem.dll` live in `MelonLoader\Il2CppAssemblies\`,**
   not `MelonLoader\net6\`.

7. **BTD6 uses the new Input System.** Use `Keyboard.current` / `Mouse.current`;
   `UnityEngine.Input` will likely throw.

8. **No font is obtainable at runtime.** `Resources.GetBuiltinResource<T>` is a stripped generic and
   TMP exposes no static font asset, so `PixelFont` renders text into a `Texture2D` by hand. Verify any
   change to the glyphs with `tools/font_preview.py` (renders a PNG you can look at).

9. **`Mesh.vertices` and friends do not exist** — only `NativeArray` generics plus `List<T>` overloads.
   Use `GameObject.CreatePrimitive(PrimitiveType.Sphere)` for geometry instead of building meshes.

10. **`MelonInfoAttribute(type, name, version, author)`** — version comes third.

11. **`BloonsMod.ApplyHarmonyPatches(Type)` swallows its own exceptions**, so it cannot be used to
    detect patch failure. Construct `new Harmony(id).CreateClassProcessor(t).Patch()` yourself.

12. **`BloonsMod.OnEarlyInitializeMelon` is sealed.** Override `OnEarlyInitialize`.

13. **After deploying, check the hash** — `.\build.ps1 -Deploy` piped through
    `Select-Object -First N` aborts the copy silently:
    ```powershell
    (Get-FileHash "<BTD6>\Mods\BloonsVR.dll").Hash -eq (Get-FileHash .\bin\Release\BloonsVR.dll).Hash
    ```

---

## How placement works, and why

Placement goes through `TowerManager.CreateTower(tower, SMath.Vector3 pos, 0, areaId, default,
deductCash: true, playPlacementEffects: true)`. `areaId` is the `MapModel.areas` polygon
containing the point (`land`/`ice`/`removable`, not `track`/`water`/`unplaceable`), and Y comes from
that area's `height`.

BTD6's own flow (`InputManager.PrimeTower` → `EnterPlacementMode` → `cursorPositionWorld` →
`TryPlace`) is driven by the **desktop cursor position**, which first person and a VR ray do not
have. That flow is still intact and is the place to hook when you want BTD6's placement ghost or
its validation — see `MODLOG.md`.

Map queries go through `BTD_Mod_Helper.Api.Helpers.Instances`
(`CurrentGameModel`, `TowerManager`, `InputManager`, `InGame`, `Map`). `TowerManager` and
`InputManager` are not Unity singletons — do not look for `Instance`.

---

## Camera

The rig **owns its camera.** BTD6 rewrites its own camera transform *every frame* — the log shows it
pinned back to `pos=(0,0,0) rot=(60,0,0)` right after our write. Borrowing `Camera.main` or
`InGame.sceneCamera` is therefore unwinnable: an earlier version here did exactly that and fought BTD6
every frame, which is what produced "stuck above the map" and "rotating around the unmodded pivot".

Instead: `new GameObject("BloonsVR_Camera")` at the scene root with **no parent**, plus
`AddComponent(Il2CppType.Of<Camera>())`. Copy `cullingMask` off BTD6's camera, force
`orthographic=false` / FOV 70 / near 0.05 / `depth=100`, and attach `UniversalAdditionalCameraData` with
`m_RendererIndex` and `renderType` copied off BTD6's camera. Nothing in the game can touch our transform.

`Camera.main` and `InGame.sceneCamera` are **different objects** in a match (different tags, same
transform), and `Camera.main` is sometimes null. Never assume they are the same thing.

**Cameras cannot be enumerated here.** `UnityEngine.Camera` exposes only the non-generic
`allCamerasCount` / `GetAllCamerasCount()`; there is no `GetAllCameras(Camera[])` in the generated wrapper
and `FindObjectsOfType<T>` / `Resources.FindObjectsOfTypeAll<T>` are stripped generics. A camera can only
be *found* via `Camera.main` or `InGame.instance.sceneCamera`, never listed.

### Do not disable BTD6's cameras

Disabling BTD6's camera was tried twice and failed both times. What you get is not an error — it is a
**frozen picture while the simulation keeps running**, because the back buffer retains the last frame
Unity actually drew. `allCamerasCount = 1` (just ours) alongside an unchanged on-screen view is the
signature. `URP renderer index -1` is *not* the bug: `-1` is URP's "use the pipeline default" sentinel.

Both cameras render; ours is full-screen at `depth = 100` and clears over the top. `V` toggles between the two views.

**Rule: hook the render loop before theorising.** `RenderProbe.cs` is a Harmony postfix on
`UniversalRenderPipeline.Render`, logging which cameras Unity is actually asked to draw. BTD6 uses the stock
pipeline — no subclass of `UniversalRenderPipeline` or `RenderPipeline` exists anywhere in
`Assembly-CSharp` — so that list is the real one. It settles in one line what three rounds of reasoning
did not: is our camera **absent** (the pipeline skips it — camera setup problem) or **present** (it renders
— the problem is what it draws).

Two traps in that probe, both already paid for:
- **`Render` is an override**, so it is declared on both `RenderPipeline` and `UniversalRenderPipeline` and
  a name-only `[HarmonyPatch]` throws `AmbiguousMatchException`. Always pass `new[] { typeof(...) }`
  argument types when patching an override. A single bad attribute aborts the whole `PatchAll`, so one
  ambiguous probe silently disabled *every* patch in the assembly.
- **BTD6's match camera is orthographic**: `Scene`, `fov=15`, `depth=5`, `rot=(60,0,0)`. Our depth 100
  draws after it, which is all a full-screen override needs.

---

## Input

- **BTD6 runs both Unity input backends at once** and reads through each: legacy `Input.GetKey` returns
  real states without throwing *and* `Keyboard.current` is non-null with live keys. **Hitting one backend
  proves nothing** — BindingOfBloons logged `GetKey(W) hidden from BTD6 (patch active)` and hotkeys still
  fired. Only the symptom disappearing is evidence.

- **The consumer must read the raw device** (`InputSystem.devices` / `Keyboard.current`) — never legacy
  `GetKey`, never an `InputAction`. `InputReader` follows this and has **no legacy fallback**, because
  Layer 1 prefixes those exact getters and a legacy fallback would starve us as well as BTD6.

- **Never poll `Keyboard.current` alone**: it returned false for every movement key while
  `Mouse.current` worked, so the player could not walk. Read through `InputReader`, which asks every
  keyboard in `InputSystem.devices`. Heartbeat prints `keys=[kb=N W0 A0 S0 D0]` — read that first.
  **That snapshot samples one frame per heartbeat and has now lied three times.** Read it only beside the
  cumulative counters `frames=N any=M held=W123/A0/S0/D0 mouse=Kf/Lpx`. `W0` next to a heartbeat means
  "not held on that frame" and nothing more — the player walked 24 units from `(0,0,-18)` to
  `(-5.26,0,6.51)` while `W0` printed on every line. **Anything reported as instantaneous needs a
  cumulative counter beside it before it is allowed to become a conclusion.**
  - The real control is a *working hotkey on the same code path*. `V` goes through
    `InputReader.PressedThisFrame`, the same device indexer as `Held(Key.W)`; `V` firing while `W` read 0
    proved the path good and the theory wrong. `Keyboard.current` being non-null is not evidence.

- `InputOverride` is three layers, all scoped to "rig active and cursor locked":
  1. **Legacy Harmony prefixes** on `GetKey`/`GetKeyDown`/`GetKeyUp`/`GetAxis`/`GetAxisRaw`, plus a
     log-only `GetButton` probe. **`new[] { typeof(KeyCode) }` is mandatory** — `GetKey` is overloaded on
     `KeyCode` and `string`.
  2. **Named actions** from `Il2Cpp.InputSystemController.instance.actionMap`: `m_Player_Move`,
     `m_Player_Look`, and the mouse consumers `m_UI_Point` (this is what spun the selected tower),
     `m_UI_MiddleClick`, `m_UI_RightClick`, `m_UI_ScrollWheel`. Deliberately **not** `m_UI_Submit` /
     `m_UI_Click` / `m_UI_Navigate` — the player needs those to click the shop.
  3. **A scan** of `InputSystem.ListEnabledActions()` for `<keyboard>/w|a|s|d` bindings, re-asserted each
     frame and **rescanned every 60 frames**. That call returns an **Il2Cpp list**: iterate with `var`,
     never assign to a `System.Collections.Generic.List`. The scan must repeat — it saw only 10 enabled
     actions and BTD6 enables more maps mid-match, so a one-shot scan misses every later map.

- **Never use `Harmony.PatchAll`.** It walks the assembly and **throws part way through**, so every class
  it had not reached stays unpatched, silently. A name-only patch on the overridden
  `UniversalRenderPipeline.Render` aborted the run and took the legacy `Input.GetKey` prefixes with it —
  two runs of "BTD6 still reacts to WASD" were caused by that, and nothing said so. Loop over
  `assembly.GetTypes()`, patch each `[HarmonyPatch]` class in its own try/catch, and **log every class
  applied by name**. The only evidence Layer 1 was live was a log line that never appeared; an absent log
  line is not evidence.

- Harmony patches register in `OnApplicationStart` — `OnInitializeMelon` is sealed on `BloonsMod`. Use
  your own `Harmony` instance; BTD6 Mod Helper's `ApplyHarmonyPatches` swallows its own exceptions.

- Heartbeat reports `btdInput=blocked(N held, M enabled)`. **`LEAKED` means BTD6 re-enabled an action we
  held.** `M` climbing while `N` stays flat means BTD6 enabled a map the rescan has not reached yet.

---

## UI and input

- No IMGUI: it needs a MonoBehaviour to host `OnGUI`. `CursorButton` uses uGUI instead (Canvas +
  Image + RawImage, no GraphicRaycaster, so it cannot intercept BTD6's own clicks) with its own hit
  test. While the cursor is locked the pointer is the screen centre, so the button is clickable
  without ever releasing the cursor first.
- **TAB** releases/re-grabs the cursor without leaving first person; mouse look is suppressed while it
  is released so clicking the shop does not swing the camera. `C` and `F` are ignored while released.

---

## Sprites

`SpriteBillboard` makes every 2D sprite face the player, by copying the camera's rotation onto
`Instances.DisplayFactory.active`. **B** toggles it.

Two things to know:
- A SpriteRenderer's visible face is its local **-Z** and a camera looks along its local **+Z**, so
  assigning the camera's rotation is what faces the sprite correctly. `Quaternion.LookRotation(pos -
  camPos)` would face it backwards — do not "fix" it that way.
- It currently billboards everything, deliberately, so breakage is visible. Anything that belongs flat
  on the ground (map walls, range circles, track arrows) will stand up on its side. `ShouldBillboard`
  in `SpriteBillboard.cs` is the single opt-out point; add exclusions there rather than filtering at
  the call site.

**The rotations do not stick**, because BTD6 rewrites display transforms later in the frame. Do not add
more Harmony hooks to `UnityDisplayNode` hoping to catch the ordering: `SetQuaternionRotation` **and**
`SetPosition` both log `rotSets=0 posSets=0`, so neither is the writer — it happens somewhere else
entirely. Start from `Assets.Scripts.Simulation.Display.DisplayNode` / `IDisplayNode` (the sim→display
bridge) or `Assets.Scripts.Unity.Display.Scene`, which owns the `Factory` and a per-frame `position`.

---

## VR (phase 2, not started)

The active OpenXR runtime is **Oculus** (`C:\Program Files\Oculus\Support\oculus-runtime\`), so
target OpenXR. BTD6 has `UnityEngine.XRModule`/`VRModule` but **no XR plug-in provider**, and an
IL2CPP build cannot gain one at runtime — so Unity's own XR stack is not an option. The plan is
P/Invoke to OpenXR from the mod and render the rig camera into two eye `RenderTexture`s.
`NewUnityModder/UnityVRMod` is a working IL2CPP reference implementation (BepInEx, DX11 only).
BTD6 ships a `D3D12\` folder, so force `-force-d3d11` for any DX11-only native VR layer.

---

## Working rules

- Keep `MODLOG.md` current. It becomes the knowledge-base field note at the end.
- `um backup create` before any launch that touches saves or profile data.
- Ask before driving the user's mouse and keyboard, changing graphics settings, deleting files, or
  publishing anything.
- Never ship game files, extracted assets or decompiled code. Mods ship as a DLL that references the
  game's own assemblies; every reference is `<Private>false</Private>` for exactly that reason.
- When a failure repeats three times, stop and change approach instead of pushing harder.

---

## Template Update Discipline (from UniversalModderPriv)

**This repo was initialized from the UniversalModderPriv template.** The template lives at:
`https://github.com/Silent-Program/UniversalModderPriv`

### Update Flow
1. Improve the template in UniversalModderPriv first (on `plan/template-update-<slug>` branch → PR → main)
2. Tag the release: `git tag -a v1.x.0 -m "Template: <description>"`
3. In this project: `git fetch upstream && git merge upstream/main --allow-unrelated-histories`
4. Resolve conflicts, test, commit

### What to Watch For
- `AGENTS.md` — Workflow changes, new rules, updated commands
- `scripts/` — New helpers (`mod-utils.sh`, `sync-games.sh`, etc.)
- `plans/template.md` — Better structure, new fields
- `.github/pull_request_template.md` — New validation steps
- `configs/tools.toml` — New tools, version bumps (universal-modder, etc.)
- Submodule updates — `tools/universal-modder` version bumps

### Propagation Script (`scripts/sync-template.sh`)
```bash
#!/usr/bin/env bash
# Run in each downstream mod project to pull template updates
git fetch template
git merge template/main --no-edit -m "chore(template): sync from UniversalModderPriv@$(git rev-parse template/main)"
git submodule update --init --recursive
```

### Agent Rule
> **Before starting any new mod project:** Verify the template is current. If `UniversalModderPriv` has newer commits than your project's `template` remote, **sync first** — don't rediscover solved problems.

### Version Tagging
Tag template releases semantically:
- `v1.0.0` — Initial stable template
- `v1.1.0` — New scripts, non-breaking workflow tweaks
- `v2.0.0` — Breaking changes (e.g., plan format change, config restructure)

Downstream projects can then `git merge template/v1.1.0` selectively.