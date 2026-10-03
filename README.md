# BloonsVR

A VR-oriented mod for **Bloons TD 6**: puts the player *inside* a match — first-person movement through a
real 3D map, with towers placed by pointing at them.

Built as a MelonLoader mod against BTD6's IL2CPP build (Unity 6000.0.58f2).

> **Work in progress.** The first-person player works. The VR mode and the 3D map geometry are not built
> yet. See [Status](#status) for what is real and what is not — please read it before reporting a bug.

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.x into your BTD6 folder
   (`...\Steam\steamapps\common\BloonsTD6`). **.NET 6 runtime required.**
2. Put `Btd6ModHelper.dll` in `Mods\`. The game **will not save progress without it**, so keep it there.
3. Drop `BloonsVR.dll` in `Mods\`.
4. Start the game normally. MelonLoader's console prints what loaded.

## Controls

| Key | Action |
|---|---|
| Mouse | Look |
| `W` `A` `S` `D` | Move |
| `Q` / `E` | Down / up |
| `Left Shift` | Sprint |
| `V` | Leave / enter first person |
| `Tab` | Release / re-grab the cursor so BTD6's menus are clickable |
| `B` | Toggle sprite billboarding |
| `C` | Cycle which tower the reticle places |
| `F` | Place the selected tower where the reticle points |

There is also a button in the top-left corner that toggles the cursor. It is clickable even while the
cursor is still locked, because the button does its own hit test and treats the screen centre as the
pointer.

BTD6's own `WASD` and mouse-look are taken from it while first person is active and handed back on `V`.

## Status

| Feature | State |
|---|---|
| First-person player controller in full 3D | **Working** |
| Cursor release for clicking shop / upgrades | **Working** |
| Take WASD + mouse look from BTD6 | **Working** |
| Tower placement from a screen-centre raycast | Built, unverified |
| Sprite billboarding | Built, **not sticking** — BTD6 overwrites it later in the frame |
| 3D map geometry | Not started |
| VR / controller poses | Not started |

### Known problems

- **Sprite billboarding does not stick.** The mod writes the rotation of every live display node, but
  BTD6 rewrites display transforms later in the frame. Hooking
  `UnityDisplayNode.SetQuaternionRotation` does not help — that method is never called. The real writer is
  still unidentified.
- **The camera needed its own GameObject.** BTD6 rewrites its match camera transform every frame, pinned
  at the origin, so borrowing it is unwinnable. The rig now creates an unparented camera and switches
  BTD6's cameras off while it is active.
- Map walls and any ground decal will stand up on its side once billboarding is fixed, because they
  genuinely belong flat. They are excluded through one function (`ShouldBillboard`) once identified.

## Building from source

Requires the .NET 6 SDK and a BTD6 install that has already been run once with MelonLoader (so that
`MelonLoader\Il2CppAssemblies\` exists).

```powershell
cd src\BloonsVR
.\build.ps1                 # build only
.\build.ps1 -Deploy         # build, then copy to <BTD6>\Mods\BloonsVR.dll
```

Always verify the deploy actually happened — piping `build.ps1` through `Select-Object -First N` aborts
the copy silently:

```powershell
(Get-FileHash "<BTD6>\Mods\BloonsVR.dll").Hash -eq (Get-FileHash .\bin\Release\BloonsVR.dll).Hash
```

## How it works

`BloonsTD6Mod` (BTD6 Mod Helper) drives everything. `OnUpdate()` is the per-frame hook; MelonLoader's own
`MelonMod` has none, and a Harmony postfix on a Unity message method like `Update` applies cleanly but
never fires.

Some constraints worth knowing if you read the source:

- **`GameObject.AddComponent<T>()` is stripped** from this IL2CPP build and throws. The working form is
  `AddComponent(Il2CppType.Of<T>()).TryCast<T>()`.
- **BTD6 has its own vector types.** Simulation APIs take
  `Il2CppAssets.Simulation.SMath.Vector2/Vector3`, not `UnityEngine`'s, and `SMath.Vector2` is `(x, z)`.
- **`GameModel` is not a managed `IEnumerable<TowerModel>`**, despite looking like one.
- **No font is obtainable at runtime**, so the cursor button's label is a hand-rolled bitmap font
  (`PixelFont`). Verify changes to it with `tools/font_preview.py`.
- Tower placement goes through `TowerManager.CreateTower(...)` with the `ObjectId` of the `MapModel.areas`
  polygon under the reticle, rather than BTD6's own `InputManager` placement flow — that flow is driven by
  the desktop cursor position, which first person and a VR ray do not have.

`MODLOG.md` in this repo is the working journal: verified API signatures, dead ends, and the reasoning
behind each decision. Start there if you are contributing.

## Credits

- [Ninja Kiwi](https://www.ninjakiwi.com/) for Bloons TD 6.
- [MelonLoader](https://github.com/LavaGang/MelonLoader) and
  [BTD6 Mod Helper](https://github.com/gurrenm3/BTD-Mod-Helper).
- Built with the [universal-modder](https://github.com/rehan-remade/universal-modder) toolkit.
- Built with AI assistance.