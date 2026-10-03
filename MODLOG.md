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

The rig **borrows** `sceneCamera`: it sets `orthographic = false`, `fieldOfView = 70`,
`nearClipPlane = 0.05`, `farClipPlane = 5000`, then writes `transform.position` /
`transform.rotation` every frame from the `InGame.Update` postfix. All of those are restored on exit
(`V`). A postfix is the last thing that runs in `InGame.Update`, so the pose we write should be the one
that renders — and `WatchForCameraClash()` logs a one-time warning if something writes it after us.

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

## Run log — what the logs actually proved (2026-10-03 15:41 session)

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

## Open questions / next steps

1. **Re-run and confirm the log.** Expected sequence, in order:
   - `[BloonsVR] loaded. V = first person, WASD = move, C = tower, F = place.`
   - `[BloonsVR] Harmony patch on InGame.Update applied.` (if absent, see below)
   - `[BloonsVR] rig ready, spawned at ..., harmony=True`
   - `[BloonsVR] N placeable towers indexed.`
   If the postfix does not actually fire, the log says
   `InGame.Update postfix did not fire; switching to the managed-coroutine driver` and the coroutine
   takes over. Either path is fine; **both must never run at once** or movement would be applied twice.
2. **No more error spam.** `Tick` logs a failure once and then tears the rig down. If errors reappear,
   read the *first* one only.
3. Confirm `Keyboard.current` / `Mouse.current` are non-null (new Input System assumption, gotcha 7).
   If they are null, no movement will happen at all and the log will be otherwise clean.
4. Does the camera actually turn? If `WatchForCameraClash` warns, BTD6 is writing `sceneCamera` after
   our postfix. Fallback options: patch the writer's method with a `Prefix` returning false, or move the
   write into `InGame.UpdateSimulation()` and check ordering relative to `InGame.Update()`.
5. Check that `CreateTower` refuses track/water. If it does not, we need footprint validation
   (`TowerModel.footprint` / `MapModel.blockers`) before calling it.
6. Re-verify the tower list: walking `GameModel`'s explicit `IEnumerable<TowerModel>` at runtime may
   include hero forms and internal towers beyond the `tier == 0` / `isParagon` filters.
7. **No HUD is possible via IMGUI.** Controller menus must be built from BTD6's own UI (the Mod Helper's
   `ModHelper*` components are built that way) or from world-space meshes.
8. Then: controller poses, then VR rendering.