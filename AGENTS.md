# AGENTS.md — BloonsVR

BTD6 VR mod. A MelonLoader mod that puts the player inside a Bloons TD 6 match: first-person
movement plus screen-centre raycast tower placement now, VR and controller UI next.

**Read `MODLOG.md` before changing anything.** It holds the verified API surface, the gotchas
already paid for, and the open questions. This file is the short version.

## Layout

- `src/BloonsVR/` — the mod:
  - `BloonsVRMod.cs` — entry point + per-frame orchestrator (no GameObjects).
  - `RigPatches.cs` — Harmony postfix on `InGame.Update` (the per-frame driver) + coroutine fallback.
  - `PlayerRig.cs` — player position/yaw/pitch in managed code, projected onto BTD6's camera.
  - `TowerPlacer.cs` — screen-centre raycast → `TowerManager.CreateTower`.
  - `Btd6Map.cs` — defensive helpers over `GameModel` / `MapModel.areas` / `AreaModel`.
  - `Hotkeys.cs` — rebindable keys, because BTD6 also binds keys.
- `universal-modder/` — vendored agent toolkit (`um` CLI, skills, knowledge base). Read-only.
- `MODLOG.md` — the journal. Update it with every change and every new finding.

## Commands

```powershell
cd src\BloonsVR
.\build.ps1                 # build only
.\build.ps1 -Deploy         # build + copy BloonsVR.dll into the game's Mods folder
dotnet build -c Release     # equivalent, no deploy
```

The game does **not** need a rebuild; drop the DLL in `Mods\` and start BTD6.

`universal-modder\bin\um` is a bash script and will not run in PowerShell. Use:

```powershell
uv run --quiet --project C:\Users\think\Documents\BloonsVR\universal-modder python -m um <args>
```

Useful: `scan "BloonsTD6"`, `backup create`, `backup restore`, `win shot`, `kb search`.

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

## Camera

The rig **owns its camera.** BTD6 rewrites its own camera transform *every frame* — the log shows it
pinned back to `pos=(0,0,0) rot=(60,0,0)` right after our write. Borrowing `Camera.main` or
`InGame.sceneCamera` is therefore unwinnable: an earlier version here did exactly that and fought BTD6
every frame, which is what produced "stuck above the map" and "rotating around the unmodded pivot".

Instead: `new GameObject("BloonsVR_Camera")` at the scene root with **no parent**, plus
`AddComponent(Il2CppType.Of<Camera>())`. Copy `cullingMask` off BTD6's camera, force
`orthographic=false` / FOV 70 / near 0.05 / `depth=100`, and switch BTD6's cameras **off** while the rig
is on, back **on** when it is off. Nothing in the game can touch our transform.

`Camera.main` and `InGame.sceneCamera` are **different objects** in a match (different tags, same
transform), and `Camera.main` is sometimes null. Never assume they are the same thing.

## UI and input

- No IMGUI: it needs a MonoBehaviour to host `OnGUI`. `CursorButton` uses uGUI instead (Canvas +
  Image + RawImage, no GraphicRaycaster, so it cannot intercept BTD6's own clicks) with its own hit
  test. While the cursor is locked the pointer is the screen centre, so the button is clickable
  without ever releasing the cursor first.
- **TAB** releases/re-grabs the cursor without leaving first person; mouse look is suppressed while it
  is released so clicking the shop does not swing the camera. `C` and `F` are ignored while released.
## Input

- **BTD6 runs both Unity input backends at once** and reads through each: legacy `Input.GetKey` returns
  real states without throwing *and* `Keyboard.current` is non-null with live keys. **Hitting one backend
  proves nothing** — BindingOfBloons logged `GetKey(W) hidden from BTD6 (patch active)` and hotkeys still
  fired. Only the symptom disappearing is evidence.
- **The consumer must read the raw device** (`InputSystem.devices` / `Keyboard.current`) — never legacy
  `GetKey`, never an `InputAction`. `InputReader` follows this and has **no legacy fallback**, because
  Layer 1 prefixes those exact getters and a legacy fallback would starve us as well as BTD6.
- Never poll `Keyboard.current` alone either: it returned false for every movement key while
  `Mouse.current` worked, so the player could not walk. Read through `InputReader`, which asks every
  keyboard in `InputSystem.devices`. Heartbeat prints `keys=[kb=N W0 A0 S0 D0]` — read that first.
- `InputOverride` is three layers, all scoped to "rig active and cursor locked":
  1. **Legacy Harmony prefixes** on `GetKey`/`GetKeyDown`/`GetKeyUp`/`GetAxis`/`GetAxisRaw`, plus a
     log-only `GetButton` probe. **`new[] { typeof(KeyCode) }` is mandatory** — `GetKey` is overloaded on
     `KeyCode` and `string`.
  2. **Named actions** from `Il2Cpp.InputSystemController.instance.actionMap`: `m_Player_Move`,
     `m_Player_Look`, and the mouse consumers `m_UI_Point` (this is what spun the selected tower),
     `m_UI_MiddleClick`, `m_UI_RightClick`, `m_UI_ScrollWheel`. Deliberately **not** `m_UI_Submit` /
     `m_UI_Click` / `m_UI_Navigate` — the player needs those to click the shop.
  3. **A scan** of `InputSystem.ListEnabledActions()` for `<keyboard>/w|a|s|d` bindings, re-asserted each
     frame. That call returns an **Il2Cpp list**: iterate with `var`, never assign to a
     `System.Collections.Generic.List`.
- Harmony patches register in `OnApplicationStart` — `OnInitializeMelon` is sealed on `BloonsMod`. Use
  your own `Harmony` instance; BTD6 Mod Helper's `ApplyHarmonyPatches` swallows its own exceptions.
- Heartbeat reports `btdInput=blocked|LEAKED|not-attached|released`. **`LEAKED` means BTD6 re-enabled its
  action map and is taking WASD and mouse look again.**

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

## VR (phase 2, not started)

The active OpenXR runtime is **Oculus** (`C:\Program Files\Oculus\Support\oculus-runtime\`), so
target OpenXR. BTD6 has `UnityEngine.XRModule`/`VRModule` but **no XR plug-in provider**, and an
IL2CPP build cannot gain one at runtime — so Unity's own XR stack is not an option. The plan is
P/Invoke to OpenXR from the mod and render the rig camera into two eye `RenderTexture`s.
`NewUnityModder/UnityVRMod` is a working IL2CPP reference implementation (BepInEx, DX11 only).
BTD6 ships a `D3D12\` folder, so force `-force-d3d11` for any DX11-only native VR layer.

## Working rules (from `universal-modder`)

- Keep `MODLOG.md` current. It becomes the knowledge-base field note at the end.
- `um backup create` before any launch that touches saves or profile data.
- Ask before driving the user's mouse and keyboard, changing graphics settings, deleting files, or
  publishing anything.
- Never ship game files, extracted assets or decompiled code. Mods ship as a DLL that references the
  game's own assemblies; every reference is `<Private>false</Private>` for exactly that reason.
- When a failure repeats three times, stop and change approach instead of pushing harder.