# BloonsVR (mod source)

The mod source lives here; the user-facing documentation is the [README](../../README.md) at the repo
root. This file exists because `um publish check` wants install steps next to the code.

## Build

```powershell
.\build.ps1                 # build only
.\build.ps1 -Deploy         # build, then copy to <BTD6>\Mods\BloonsVR.dll
```

Requirements: .NET 6 SDK, plus a BTD6 install that has been run once with MelonLoader so that
`MelonLoader\Il2CppAssemblies\` exists. Pass `-BloonsTD6Dir <path>` if Steam installed it elsewhere.

Verify the deploy — piping `build.ps1` into `Select-Object -First N` closes the pipeline and aborts the
copy without an error:

```powershell
(Get-FileHash "<BTD6>\Mods\BloonsVR.dll").Hash -eq (Get-FileHash .\bin\Release\BloonsVR.dll).Hash
```

## Layout

| File | Role |
|---|---|
| `AssemblyInfo.cs` | `MelonInfo` / `MelonGame` attributes |
| `BloonsVRMod.cs` | Entry point and per-frame orchestrator, driven by `OnUpdate()` |
| `PlayerRig.cs` | Player state and the rig's own camera |
| `TowerPlacer.cs` | Screen-centre raycast and `TowerManager.CreateTower` |
| `SpriteBillboard.cs` | Makes 2D sprites face the player (**does not stick yet**) |
| `DisplayRotationPatch.cs` | Harmony hooks probing how BTD6 writes display transforms |
| `InputOverride.cs` | Disables BTD6's `Move` / `Look` actions while the rig is active |
| `CursorButton.cs` | uGUI overlay that releases the cursor for BTD6's menus |
| `PixelFont.cs` | 5x7 bitmap font rendered to a `Texture2D` (no runtime font exists) |
| `Btd6Map.cs` | Helpers over `GameModel` / `MapModel.areas` / `AreaModel` |
| `Hotkeys.cs` | Rebindable keys |

## Read before changing anything

`../../MODLOG.md` is the working journal. It records the verified API surface, the dead ends, and — most
importantly — which Unity and IL2CPP APIs turned out to be stripped. Several obvious approaches fail at
runtime in this game, and the journal says which.

## Credits

Bloons TD 6 by Ninja Kiwi. MelonLoader and BTD6 Mod Helper by their respective authors. Built with the
universal-modder toolkit and AI assistance.