using System.Linq;
using BTD_Mod_Helper;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.WorldInteractables;
using MelonLoader;
using UnityEngine;

namespace BloonsVR
{
    /// <summary>
    /// Mod entry point and per-frame orchestrator.
    ///
    /// There is no GameObject, no MonoBehaviour and no IMGUI in this mod. BTD6's IL2CPP build strips
    /// <c>GameObject.AddComponent&lt;T&gt;()</c> entirely, so the usual "spawn a GameObject and drive it
    /// from Update()" pattern throws a TypeInitializationException before it ever runs. The tick comes
    /// from a managed coroutine instead — see <see cref="RigDriver"/>.
    ///
    /// Lifecycle hooks come from BTD6 Mod Helper's <see cref="BloonsTD6Mod"/>:
    ///   - <c>OnInGameLoaded</c>: a match exists and InGame.instance.Bridge.Simulation is ready.
    ///   - <c>OnGameObjectsReset</c>: BTD6 wiped the match, so the borrowed camera must be re-bound.
    ///   - <c>OnSceneWasLoaded</c>: covers restarts and map changes that skip the hook above.
    /// </summary>
    public class BloonsVRMod : BloonsTD6Mod
    {
        private static PlayerRig _rig;
        private static TowerPlacer _placer;

        private static int _lastFrame = -1;
        private static bool _disabled;
        private static bool _userDisabled;
        private static bool _diagnosticsLogged;
        private static bool _reportedCameraClash;
        private static int _framesRun;

        /// <summary>
        /// Set when the player turns first person off with V. BTD6's pause menu tears the match down and
        /// rebuilds it, which re-fires the load hooks; without this the rig would spring back on by itself
        /// and Escape would appear to toggle it. The rig is still rebuilt, but left switched off.
        /// </summary>
        internal static bool UserDisabled => _userDisabled;

        internal static void SetUserDisabled(bool value)
        {
            _userDisabled = value;
            // No SetActive anymore - we just track user preference
        }

        /// <summary>Where the player is dropped when a rig is created.</summary>
        internal static Vector3 SpawnPoint { get; private set; } = new Vector3(0f, 5f, -20f);

        /// <summary>The active player rig, for Harmony patches that run outside the mod's tick.</summary>
        internal static PlayerRig Rig => _rig;

        public override void OnEarlyInitialize()
        {
            base.OnEarlyInitialize();
            MelonLogger.Msg("[BloonsVR] loaded. V = toggle mouse lock, WASD = move, C = tower, F = place, B = billboards.");
            CursorButton.OnToggleRequested = () => _rig?.ToggleCursorLock();
        }

        /// <summary>
        /// Register the Harmony patches here. <c>OnInitializeMelon</c> is sealed on <c>BloonsMod</c>, so
        /// <c>OnApplicationStart</c> is the earliest hook we own, and it is what BindingOfBloons uses.
        ///
        /// BTD6 Mod Helper's own <c>ApplyHarmonyPatches</c> is deliberately not used: it swallows its own
        /// exceptions and only logs them, so a failed patch would be invisible here.
        ///
        /// Also deliberately not <c>PatchAll</c>. One ambiguous <c>[HarmonyPatch]</c> makes it throw, and
        /// it throws part-way through — so every class it had not reached yet is silently left unpatched.
        /// That is exactly what happened: a name-only patch on the overridden
        /// <c>UniversalRenderPipeline.Render</c> aborted the run, and the legacy <c>Input.GetKey</c>
        /// prefixes that deny BTD6 the movement keys went down with it. Nothing said so, because the only
        /// evidence those patches were live was a log line that never appeared — and an absent log line is
        /// not evidence.
        ///
        /// Patching each class in its own try/catch means a failure is named, contained, and the rest
        /// still apply. Every applied class is listed, so "is Layer 1 actually live" is answerable from the
        /// log rather than inferred from the absence of a symptom.
        /// </summary>
        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
            MelonLogger.Msg("[BloonsVR] OnApplicationStart called - applying Harmony patches");

            var harmony = new HarmonyLib.Harmony("com.bloonsvr");
            var assembly = typeof(BloonsVRMod).Assembly;
            var applied = new System.Collections.Generic.List<string>();
            var failed = new System.Collections.Generic.List<string>();

            foreach (var type in assembly.GetTypes())
            {
                if (type == null || !IsPatchTarget(type))
                    continue;

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied.Add(type.Name);
                }
                catch (System.Exception e)
                {
                    // The first line of e is the useful one; the rest is Harmony stack noise.
                    failed.Add($"{type.Name}: {e.Message.Split('\n')[0].Trim()}");
                }
            }

            MelonLogger.Msg(
                $"[BloonsVR] Harmony: {applied.Count} patch class(es) applied " +
                $"[{string.Join(", ", applied)}]");

            foreach (var problem in failed)
                MelonLogger.Error($"[BloonsVR] Harmony: patch class FAILED -> {problem}");
        }

        /// <summary>
        /// True when this type, or any of its methods, carries a <c>[HarmonyPatch]</c>.
        ///
        /// Method-level patches are included because <c>PatchAll</c> supported them and a mod that quietly
        /// stops honouring them is a trap for whoever edits this next.
        /// </summary>
        private static bool IsPatchTarget(System.Type type)
        {
            if (type.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length > 0)
                return true;

            foreach (var method in type.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Length > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// BTD6 Mod Helper's per-frame hook. One call per frame, on the main thread, inside the match.
        ///
        /// This is not a MelonLoader feature: MelonLoader 0.7's <c>MelonMod</c> has no per-frame callback
        /// at all. BTD6 Mod Helper implements <c>OnUpdate</c> itself, which is why it is used here instead of
        /// an invented driver.
        /// </summary>
        public override void OnUpdate()
        {
            Tick();
        }

        public override void OnInGameLoaded(InGame inGame)
        {
            base.OnInGameLoaded(inGame);
            EnsureRig(inGame);
        }

        public override void OnGameObjectsReset()
        {
            base.OnGameObjectsReset();
            DestroyRig();
        }

        public override void OnMatchEnd()
        {
            base.OnMatchEnd();
            DestroyRig();
        }

        public override void OnMainMenu()
        {
            base.OnMainMenu();
            DestroyRig();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            base.OnSceneWasLoaded(buildIndex, sceneName);
            DestroyRig();
        }

        /// <summary>
        /// One tick of the rig. Driven by <see cref="RigDriver"/>.
        ///
        /// Failures here must never tear the rig down and retry next frame: a per-frame exception in here
        /// used to create and destroy the rig hundreds of times a second and bury the real error in log
        /// spam. Failures are now counted, logged once, and the rig is disabled for good.
        /// </summary>
        internal static void Tick()
        {
            if (_disabled)
                return;

            try
            {
                int frame = Time.frameCount;
                if (frame == _lastFrame)
                    return;
                _lastFrame = frame;

                if (_rig == null)
                    EnsureRig(BTD_Mod_Helper.Api.Helpers.Instances.InGame);
                if (_rig == null)
                    return;

                _framesRun++;

                // Before the rig reads anything, so the cumulative key counts cover the same frames.
                InputReader.Sample();

                if (_framesRun % 60 == 0)
                    Heartbeat();

                _rig.Tick();
                _placer.Tick();

                // Keep the render probe quiet unless first person is on.
                RenderProbe.Active = _rig.IsActive;

                // Only steal WASD from BTD6 while we are actually the ones using it.
                InputOverride.SetBlocking(_rig.IsActive && _rig.CursorLocked);
                InputOverride.Tick();

                CursorButton.Tick(_rig.CursorLocked, _rig.IsActive);

                // Every 2D sprite in the match faces the player.
                SpriteBillboard.SetCamera(_rig.RigCamera);
                SpriteBillboard.Tick(_rig.RigCamera);

                WatchForCameraClash();

                if (!_diagnosticsLogged)
                {
                    _diagnosticsLogged = true;
                    LogDiagnostics();
                }
            }
            catch (System.Exception e)
            {
                _disabled = true;
                MelonLogger.Error($"[BloonsVR] tick failed after {_framesRun} frames, rig disabled for this session: {e}");
            }
        }

        private static void EnsureRig(InGame inGame)
        {
            if (_rig != null || _disabled)
                return;

            var model = Btd6Map.Model;
            if (model == null || model.map == null)
                return;

            if (inGame == null || inGame.sceneCamera == null)
                return;

            SpawnPoint = ChooseSpawn(model.map);

            var rig = new PlayerRig();
            if (!rig.Initialise(inGame))
                return;

            rig.Teleport(SpawnPoint, 0f);

            _rig = rig;
            _placer = new TowerPlacer(rig);

            if (_userDisabled)
            {
                // No SetActive - we just track user preference
                MelonLogger.Msg("[BloonsVR] rig rebuilt but left off (player had pressed V)");
            }

            MelonLogger.Msg($"[BloonsVR] rig ready, spawned at {SpawnPoint}");
        }

        private static void DestroyRig()
        {
            InputOverride.SetBlocking(false);
            CursorButton.Destroy();
            SpriteBillboard.Reset();
            _rig?.Shutdown();
            _rig = null;
            _placer = null;
        }

        /// <summary>
        /// BTD6 pans and zooms its own camera. We overwrite the transform every frame, but if something
        /// writes it afterwards we would silently fight it. Say so once rather than leaving the user with
        /// a camera that mysteriously drifts.
        /// </summary>
        private static void WatchForCameraClash()
        {
            if (_reportedCameraClash || _rig == null || !_rig.IsActive)
                return;

            var camera = _rig.RigCamera;
            if (camera == null)
                return;

            var expected = _rig.PlayerPosition + Vector3.up * PlayerRig.EyeHeight;
            if (Vector3.Distance(camera.transform.position, expected) > 2f)
            {
                _reportedCameraClash = true;
                MelonLogger.Warning(
                    $"[BloonsVR] something moves InGame.sceneCamera after our tick " +
                    $"(camera {camera.transform.position}, expected {expected}); the rig will fight it.");
            }
        }

        /// <summary>
        /// Once a second, prove the loop is alive and the player state is actually changing. This is the
        /// check that separates "the controller is broken" from "the camera we are writing is not the one
        /// on screen" — the two look identical from inside the game.
        /// </summary>
        private static void Heartbeat()
        {
            var model = Btd6Map.Model;

            // Unity exposes only a non-generic allCamerasCount here, so this is the sole way to see
            // cameras come and go. It has to be sampled over time: BTD6's real match camera appears after
            // the rig is built, which is why a single reading at spawn is misleading.
            string cams;

            try
            {
                cams = Camera.allCamerasCount.ToString();
            }
            catch (System.Exception)
            {
                cams = "?";
            }

            MelonLogger.Msg(
                $"[BloonsVR] tick {_framesRun}: player={_rig?.PlayerPosition} " +
                $"yaw={_rig?.Yaw:F0} pitch={_rig?.Pitch:F0} camPos={_rig?.RigCamera?.transform.position} " +
                $"aim={(_placer != null && _placer.HasAim ? _placer.AimPoint.ToString() : "none")} " +
                $"cash={(model == null ? -1f : model.cash)} billboards={SpriteBillboard.Billboarded} " +
                $"rotSets={SpriteBillboard.RotationSets} rig={(_rig == null ? "no" : _rig.IsActive.ToString())} " +
                $"cams={cams} btdInput={InputOverride.HoldState()} keys=[{InputReader.Describe()}]");
        }

        /// <summary>Everything unknown about the runtime, dumped once so one run answers all of it.</summary>
        private static void LogDiagnostics()
        {
            var model = Btd6Map.Model;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            string pipeline;

            try
            {
                pipeline = UnityEngine.Rendering.RenderPipelineManager.GetCurrentPipelineAssetType();
            }
            catch (System.Exception e)
            {
                pipeline = $"unavailable ({e.GetType().Name})";
            }

            MelonLogger.Msg(
                $"[BloonsVR] diag: frame={Time.frameCount} delta={Time.deltaTime:F4} pipeline={pipeline} " +
                $"keyboard={(keyboard == null ? "NULL" : "ok")} mouse={(mouse == null ? "NULL" : "ok")} " +
                $"cursorLock={Cursor.lockState} screen={Screen.width}x{Screen.height}");
            MelonLogger.Msg($"[BloonsVR] diag Camera.main  {PlayerRig.Describe(Camera.main)}");
            MelonLogger.Msg($"[BloonsVR] diag sceneCamera {PlayerRig.Describe(BTD_Mod_Helper.Api.Helpers.Instances.InGame?.sceneCamera)}");
            MelonLogger.Msg(
                $"[BloonsVR] diag rig={(_rig?.IsActive.ToString() ?? "no")} " +
                $"cash={(model == null ? -1f : model.cash)} " +
                $"areas={(model?.map?.areas == null ? -1 : model.map.areas.Length)}");

            // Every device, with the flags that decide whether it can receive events at all. This is the
            // difference between "the Input System is not running" and "this particular device is not
            // being fed", which look identical from a key read.
            InputReader.LogDeviceDiagnostics();
        }

        /// <summary>
        /// Drop the player above the centre of the buildable part of the map, looking at the track.
        /// Uses only the area polygons so it works before any physics exists.
        /// </summary>
        private static Vector3 ChooseSpawn(Il2CppAssets.Scripts.Models.Map.MapModel map)
        {
            var areas = map.areas;
            if (areas == null || areas.Length == 0)
                return new Vector3(0f, 5f, -20f);

            var sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < areas.Length; i++)
            {
                var area = areas[i];
                if (area == null || !Btd6Map.IsPlaceable(area))
                    continue;

                // AreaModel.GetCenterPoint returns BTD6's SMath.Vector3, not UnityEngine's.
                var areaCentre = area.GetCenterPoint();
                sum += new Vector3(areaCentre.x, areaCentre.y, areaCentre.z);
                count++;
            }

            if (count == 0)
                return new Vector3(0f, 5f, -20f);

            var centre = sum / count;
            return new Vector3(centre.x, centre.y + 12f, centre.z - 18f);
        }
    }

    /// <summary>
    /// Harmony postfix on InGame.Update.
    ///
    /// BTD6's Update() pins the Scene camera to orthographic (60,0,0) every frame.
    /// InGame.Update runs every frame, so writing our pose here should win if we run LAST.
    /// We use a Postfix to run after BTD6's Update logic.
    /// </summary>
    [HarmonyPatch(typeof(InGame), "Update")]
    internal static class InGameUpdatePostfix
    {
        private static void Postfix(InGame __instance)
        {
            if (BloonsVRMod.Rig == null || !BloonsVRMod.Rig.IsActive)
                return;

            var rig = BloonsVRMod.Rig;
            var camera = rig.RigCamera;
            if (camera == null)
                return;

            var transform = camera.transform;
            var expectedPos = rig.PlayerPosition + Vector3.up * PlayerRig.EyeHeight;
            
            // Always log to verify postfix is firing
            MelonLogger.Msg($"[BloonsVR] InGameUpdate: rig={rig.IsActive}, camera={camera.name}, currentPos={transform.position}, expectedPos={expectedPos}");
            
            transform.position = expectedPos;
            transform.rotation = Quaternion.Euler(rig.Pitch, rig.Yaw, 0f);
        }
    }
}
