# Plan: Bug-Fix Sprint (Pre-Phase 2)

**ID:** PLAN-20261004-001
**Status:** In Progress
**Target Branch:** `fix/bugfix-sprint`
**Created:** 2026-10-04
**Approved By:** User

## Objective

Fix all known Phase 1 bugs before starting Phase 2 (3D map → VR). Current bugs cause input leaks, spawn issues, camera fights, and broken billboarding.

## Scope

- **Games affected:** Bloons TD 6 (BTD6)
- **Files touched:** `InputOverride.cs`, `PlayerRig.cs`, `BloonsVRMod.cs`, `SpriteBillboard.cs`, `DisplayRotationPatch.cs`

## Phases

### Phase 1: Layer 1 Input Blocking (Highest Priority) ✅ DONE
- [x] Force all 5 Layer 1 static constructors via static constructor anti-optimization
- [x] Verify `GetKey`, `GetKeyDown`, `GetKeyUp`, `GetAxis`, `GetAxisRaw` patches applied
- [x] Test: WASD completely blocked from BTD6 while first person active

### Phase 2: V Key Conflict (In Progress)
- [ ] Add `Key.V` to Layer 1 suppression (or rebind first-person toggle to another key)
- [ ] Test: V toggles first person without opening BTD6 tower menu

### Phase 3: TAB Cursor Release Hotkey Leak
- [ ] In `InputOverride.Tick()`, check `CursorLocked` and suppress all our hotkeys when `false`
- [ ] Test: TAB releases cursor, WASD/V do nothing in BTD6 menus

### Phase 4: Player Spawn Fix
- [ ] Use `Btd6Map` to find valid area center + height at rig initialization
- [ ] Test: Player spawns at y=12 (above ground), not inside geometry

### Phase 5: Camera Fight (LateUpdate Timing)
- [ ] Verify `WorldCameraControllerLateUpdatePostfix` runs AFTER BTD6's Update
- [ ] Test: Camera never snaps back to (0,0,0) rot(60,0,0)

### Phase 6: Sprite Billboard Stickiness
- [ ] Find actual transform writer: `DisplayNode`/`IDisplayNode`/`Scene`
- [ ] Patch the actual writer, not `SetQuaternionRotation`/`SetPosition`
- [ ] Test: Sprites stay facing player, don't flip back

### Phase 3: TAB Cursor Release Hotkey Leak
- [ ] In `InputOverride.Tick()`, check `CursorLocked` and suppress all our hotkeys when `false`
- [ ] Test: TAB releases cursor, WASD/V do nothing in BTD6 menus

### Phase 3: Player Spawn Fix
- [ ] Use `Btd6Map` to find valid area center + height at rig initialization
- [ ] Test: Player spawns at y=12 (above ground), not inside geometry

### Phase 5: Camera Fight (LateUpdate Timing)
- [ ] Verify `WorldCameraControllerLateUpdatePostfix` runs AFTER BTD6's Update
- [ ] Test: Camera never snaps back to (0,0,0) rot(60,0,0)

### Phase 6: Sprite Billboard Stickiness
- [ ] Find actual transform writer: `DisplayNode`/`IDisplayNode`/`Scene`
- [ ] Patch the actual writer, not `SetQuaternionRotation`/`SetPosition`
- [ ] Test: Sprites stay facing player, don't flip back

## Risks / Unknowns

- Layer 1 static constructors may need explicit type access via reflection
- `WorldCameraController.LateUpdate` may not be the right hook point
- Actual billboard writer may be in simulation→display bridge (`DisplayNode`)

## Rollback Plan

- Revert to `main` (v0.0.1 tag) if any fix breaks movement/camera

## Testing / Validation Evidence

- [ ] Game launches without errors
- [ ] Mod loads in MelonLoader
- [ ] First-person movement works (WASD, mouse look)
- [ ] V toggles first person cleanly
- [ ] TAB releases cursor, no hotkey leaks
- [ ] Player spawns above ground
- [ ] Camera follows player without fighting
- [ ] Sprites face player and stay facing