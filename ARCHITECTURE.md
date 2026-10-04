# BloonsVR Architecture Reference

**Purpose:** Document the working systems so they don't get broken during future development.

---

## Input System Architecture

### Three-Layer Blocking Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    InputOverride.Blocking                   │
│  (rig.IsActive && rig.CursorLocked)                         │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
        ┌──────────┐    ┌──────────┐    ┌──────────┐
        │ Layer 1  │    │ Layer 2  │    │ Layer 3  │
        │ Legacy   │    │ Named    │    │ Scan     │
        │ Input    │    │ Actions  │    │ Actions  │
        └──────────┘    └──────────┘    └──────────┘
```

### Layer 1: Legacy UnityEngine.Input (Harmony Prefixes)
**File:** `InputOverride.cs` lines 353-410

```csharp
// 5 classes, each with static constructor + Prefix
[HarmonyPatch(typeof(Input), "GetKey", new[] { typeof(KeyCode) })]
internal static class BlockLegacyGetKey { ... }
[HarmonyPatch(typeof(Input), "GetKeyDown", new[] { typeof(KeyCode) })]
internal static class BlockLegacyGetKeyDown { ... }
[HarmonyPatch(typeof(Input), "GetKeyUp", new[] { typeof(KeyCode) })]
internal static class BlockLegacyGetKeyUp { ... }
[HarmonyPatch(typeof(Input), "GetAxis", new[] { typeof(string) })]
internal static class BlockLegacyGetAxis { ... }
[HarmonyPatch(typeof(Input), "GetAxisRaw", new[] { typeof(string) })]
internal static class BlockLegacyGetAxisRaw { ... }
```

**Anti-optimization pattern (CRITICAL - don't remove):**
```csharp
static BlockLegacyGetKey()
{
    MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetKey patch class loaded");
    _ = typeof(BlockLegacyGetKey);  // Prevents static constructor optimization
}
```

**How it works:**
- `Prefix` returns `false` to skip original method
- Sets `result = false` for WASD keys (87,65,83,68) and Horizontal/Vertical axes
- Returns `true` for all other keys (passes through)

**Verification:** Log shows all 5 classes loaded, Harmony applies 9/9 patches.

### Layer 2: Named Input Actions
**File:** `InputOverride.cs` lines 85-132

```csharp
Take(map.m_Player_Move, "m_Player_Move");
Take(map.m_Player_Look, "m_Player_Look");
Take(map.m_UI_Point, "m_UI_Point");      // Rotates selected tower
Take(map.m_UI_MiddleClick, "m_UI_MiddleClick");
Take(map.m_UI_RightClick, "m_UI_RightClick");
Take(map.m_UI_ScrollWheel, "m_UI_ScrollWheel");
```

**NOT blocked (player needs these for menus):**
- `m_UI_Submit`, `m_UI_Click`, `m_UI_Navigate`

### Layer 3: Periodic WASD Action Scan
**File:** `InputOverride.cs` lines 133-224

- Scans `InputSystem.ListEnabledActions()` every 60 frames
- Disables any action bound to `<keyboard>/w|a|s|d`
- Logs: `Layer 3: pass N, scanned M enabled, disabled K new`

---

## First Person Camera Architecture

### Core Principle: Borrow Scene Camera, Win in LateUpdate

**DO NOT:**
- Create your own Camera (URP ignores it)
- Disable Scene camera
- Fight in Update (BTD6 writes after)

**DO:**
- Borrow `InGame.instance.sceneCamera`
- Write transform in `WorldCameraController.LateUpdate` postfix
- Restore original state on disable

### Camera Hook: WorldCameraController.LateUpdate Postfix
**File:** `BloonsVRMod.cs` lines 503-540

```csharp
[HarmonyPatch(typeof(WorldCameraController), "LateUpdate")]
internal static class WorldCameraControllerLateUpdatePostfix
{
    private static void Postfix(WorldCameraController __instance)
    {
        if (BloonsVRMod.Rig == null || !BloonsVRMod.Rig.IsActive)
            return;
        
        var rig = BloonsVRMod.Rig;
        var camera = rig.RigCamera;  // = InGame.instance.sceneCamera
        if (camera == null) return;

        var transform = camera.transform;
        var expectedPos = rig.PlayerPosition + Vector3.up * PlayerRig.EyeHeight;
        transform.position = expectedPos;
        transform.rotation = Quaternion.Euler(rig.Pitch, rig.Yaw, 0f);
    }
}
```

**Why LateUpdate works:**
- BTD6's `Update()` pins camera to orthographic (0,0,0) rot(60,0,0)
- `LateUpdate` runs AFTER Update
- We write our transform LAST, we win

### Camera State Management
**File:** `PlayerRig.cs`

```csharp
// Store original state on init
StoreCameraState();  // clearFlags, bgColor, ortho, FOV, near/far, cullingMask, depth, rect

// On activate
sceneCamera.orthographic = false;
sceneCamera.fieldOfView = 70f;
// ... configure for first person

// On deactivate
RestoreCameraState();  // restore ALL original values
```

**CRITICAL:** Never disable Scene camera. Never modify its rect/depth permanently.

---

## Rig Lifecycle

### Initialization (`OnInGameLoaded` → `EnsureRig` → `Initialise`)
```csharp
_rig = new PlayerRig();
rig.Initialise(inGame);  // stores Scene camera, configures it
rig.Teleport(SpawnPoint, 0f);

// Add LateUpdate driver via Harmony (NOT MonoBehaviour)
// WorldCameraControllerLateUpdatePostfix handles transform
```

### Toggle (`V` key → `TowerPlacer.Tick` → `SetActive`)
```csharp
public void SetActive(bool active)
{
    if (IsActive == active) return;
    IsActive = active;
    
    if (active) {
        // Re-apply first-person camera settings
        LockCursor(true);
    } else {
        RestoreCameraState();  // restores ALL original camera state
        LockCursor(false);
    }
}
```

### Shutdown (`DestroyRig`)
```csharp
InputOverride.SetBlocking(false);
CursorButton.Destroy();
SpriteBillboard.Reset();
_rig?.Shutdown();
_rig = null;
_placer = null;
```

---

## Input Flow

```
User presses V
    │
    ▼
TowerPlacer.Tick()  (runs every frame)
    │
    ├── InputReader.PressedThisFrame(Hotkeys.ToggleRig)  // reads raw device
    │
    ▼
_rig.SetActive(!_rig.IsActive)
    │
    ├── rig.IsActive = true
    │
    ├── InputOverride.SetBlocking(true)  // rig.IsActive && CursorLocked
    │
    ▼
InputOverride.Tick() runs every frame
    │
    ├── Layer 1: Harmony prefixes block legacy GetKey/GetAxis
    ├── Layer 2: Named actions disabled
    ├── Layer 3: Periodic scan disables WASD actions
    │
    ▼
InputReader reads raw device (Keyboard.current / InputSystem.devices)
    │
    ▼
PlayerRig.Tick() applies movement
    │
    ▼
WorldCameraControllerLateUpdatePostfix applies camera transform
```

---

## Key Invariants (NEVER BREAK)

1. **Never disable Scene camera** - it's the only one URP renders
2. **Never create your own Camera** - URP ignores runtime-created cameras
3. **Never fight in Update** - BTD6 writes after, use LateUpdate postfix
3. **Never disable Scene camera rect/depth** - store/restore only
4. **Input blocking ONLY when** `rig.IsActive && rig.CursorLocked`
5. **InputReader reads RAW device only** - never legacy Input.GetKey
6. **Static constructors must NOT be optimized** - use `_ = typeof(...)` pattern
6. **Harmony: never use PatchAll** - loop types individually with try/catch
7. **Scene camera state: STORE ONCE, RESTORE ON DISABLE**

---

## Debugging Checklist

| Symptom | Check |
|---------|-------|
| Input not blocked | Harmony applied 9/9? `Layer 1:` logs? `btdInput=blocked`? |
| Camera not moving | LateUpdate postfix firing? `Rig` not null? `IsActive` true? |
| Camera snaps back | LateUpdate running AFTER Update? Harmony postfix applied? |
| V doesn't toggle | `ToggleRig` pressed? `rig` not null? `SetActive` called? |
| Camera wrong pos | `SyncCameraNow()` called after init? LateUpdate applying? |
| Input leaks in menus | `CursorLocked` check in `SetBlocking`? `CursorToggle` handled? |

---

## Last Known Good Commit
`776ffe0` - Layer 1 working, input blocking verified, V toggle logging added