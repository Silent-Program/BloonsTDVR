using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.Key;

namespace BloonsVR
{
    /// <summary>
    /// First-person rig: a player position/rotation kept in managed code and projected onto a camera the
    /// rig owns outright.
    ///
    /// The rig is two objects — a body and a camera parented to it. The camera is created from scratch
    /// (<c>new GameObject</c> plus <c>AddComponent(Il2CppType.Of&lt;Camera&gt;())</c>) because BTD6 rewrites
    /// its own camera transform every frame — the log shows it pinned back to
    /// <c>pos=(0,0,0) rot=(60,0,0)</c> immediately after our write — so borrowing <c>Camera.main</c> or
    /// <c>InGame.sceneCamera</c> means fighting for the same transform forever.
    ///
    /// BTD6's camera is **not** disabled and **not** resized. Both render every frame: BTD6's draws first
    /// and ours clears over the top of it because our depth is far higher. Disabling BTD6's camera was
    /// tried and is a dead end — it leaves a frame with nothing drawing in it, which reads as a frozen
    /// screen while the simulation carries on. A half-and-half viewport split also works, but it makes the
    /// game unplayable while the rig is on, so <c>V</c> is a plain toggle between the two full-screen views.
    ///
    /// Movement writes a plain position rather than using a CharacterController, because BTD6 maps are not
    /// uniformly collider-covered and a CharacterController would drop the player through the world.
    /// Ground height comes from <see cref="Btd6Map.GroundHeight"/>, which probes physics when there is a
    /// collider and falls back to the map's area polygons when there is not.
    /// </summary>
    public class PlayerRig
    {
        public const float WalkSpeed = 8f;
        public const float SprintSpeed = 20f;
        public const float VerticalSpeed = 7f;
        public const float EyeHeight = 1.7f;
        public const float MouseSensitivity = 2.2f;
        public const float GroundProbeUp = 40f;
        public const float GroundProbeDown = 80f;

        /// <summary>
        /// Our camera claims the whole frame. BTD6's camera is left on its own full-screen rect and simply
        /// draws first; ours has the higher depth and clears over the top of it.
        ///
        /// A half-and-half viewport split was tried and works, but it is not what was asked for, and it
        /// makes the game unplayable while the rig is on.
        /// </summary>
        private static readonly Rect FullScreen = new Rect(0f, 0f, 1f, 1f);

        private InGame _inGame;
        private Camera _camera;
        private GameObject _playerObject;
        private GameObject _cameraObject;
        private bool _cameraSettingsApplied;

        // BTD6's own camera is left completely alone now: it keeps its own full-screen rect and its own depth,
// and our camera simply draws after it. See ApplyRenderOwnership.

        private Vector3 _playerPosition;
        private float _yaw;
        private float _pitch;
        private bool _spawned;

        /// <summary>The camera the rig is driving, or null until one is found.</summary>
        public Camera RigCamera => _camera;

        /// <summary>True while the rig owns the view.</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// False while the player has deliberately released the cursor to click BTD6's menus. Mouse look is
        /// suppressed in that state, otherwise every click on the shop menu would also swing the camera.
        /// </summary>
        public bool CursorLocked { get; private set; } = true;

        /// <summary>Where the player is standing, in world space.</summary>
        public Vector3 PlayerPosition => _playerPosition;

        /// <summary>Head yaw in degrees.</summary>
        public float Yaw => _yaw;

        /// <summary>Head pitch in degrees.</summary>
        public float Pitch => _pitch;

        /// <summary>Take over the view with our own camera drawn over BTD6's.</summary>
        public bool Initialise(InGame inGame)
        {
            _inGame = inGame;
            IsActive = true;

            CreateRig();
            LockCursor(true);
            return _camera != null;
        }

        private void CreateOwnCamera()
        {
            if (_camera != null)
                return;

            // Sample BTD6's camera settings before we add our own, so ours matches what the player is
            // used to seeing (layer mask especially - the world lives on BTD6's layers).
            var reference = Camera.main;
            if (reference == null && _inGame != null)
                reference = _inGame.sceneCamera;

            // A player object with the camera parented to it, rather than a loose camera we poke every
            // frame. Yaw goes on the body and pitch on the head, which is how a body actually stands, and
            // it gives phase 2 a real transform to hang the HMD and controllers off instead of inventing
            // one later.
            _playerObject = new GameObject("BloonsVR_Player");
            _cameraObject = new GameObject("BloonsVR_Camera");
            _cameraObject.transform.SetParent(_playerObject.transform, false);

            _camera = _cameraObject.AddComponent(Il2CppInterop.Runtime.Il2CppType.Of<Camera>())
                .TryCast<Camera>();

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            _camera.orthographic = false;
            _camera.fieldOfView = 70f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 5000f;

            // Full screen, and later in the frame than anything BTD6 owns, so our view covers the game
            // rather than sharing it.
            _camera.rect = FullScreen;
            _camera.depth = 100f;

            if (reference != null)
                _camera.cullingMask = reference.cullingMask;

            CopyUrpRenderer(reference);

            // Diagnostic. Unity exposes only a non-generic allCamerasCount here — GetAllCameras and
            // FindObjectsOfType<T> are both absent or stripped — so this is the only way to find out how
            // many cameras are in play. A reading of 1 means BTD6's match camera did not exist yet when we
            // measured; it is logged again from the render probe every second.
            try
            {
                MelonLogger.Msg(
                    $"[BloonsVR] rig created; cameras in scene at spawn={Camera.allCamerasCount}; " +
                    Describe(_camera));
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[BloonsVR] camera count unavailable: {e.Message}");
            }
        }

        /// <summary>Create the player object and the camera hanging off it.</summary>
        private void CreateRig()
        {
            CreateOwnCamera();
        }

        /// <summary>
        /// Give our camera the same URP renderer as BTD6's.
        ///
        /// This is the most likely reason a runtime-created camera renders nothing in a URP game: a camera
        /// with no renderer data falls back to renderer index 0, and if BTD6's world is drawn by a different
        /// index the camera has nothing to show. It only shows up at runtime, because the missing
        /// <c>UniversalAdditionalCameraData</c> cannot be seen by decompiling.
        /// </summary>
        private void CopyUrpRenderer(Camera reference)
        {
            if (_camera == null)
                return;

            try
            {
                var ours = _cameraObject
                    .AddComponent(Il2CppInterop.Runtime.Il2CppType
                        .Of<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>())
                    .TryCast<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

                if (ours == null)
                {
                    MelonLogger.Warning("[BloonsVR] could not attach UniversalAdditionalCameraData");
                    return;
                }

                int rendererIndex = 0;
                if (reference != null &&
                    reference.TryGetComponent(
                        out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData theirs) &&
                    theirs != null)
                {
                    rendererIndex = theirs.m_RendererIndex;
                    ours.renderType = theirs.renderType;
                }

                ours.m_RendererIndex = rendererIndex;

                MelonLogger.Msg(
                    $"[BloonsVR] URP renderer index {rendererIndex}" +
                    (reference == null ? " (no reference camera)" : " (copied from BTD6's camera)"));
            }
            catch (System.Exception e)
            {
                // GetComponent only exposes a generic overload here, which may itself be stripped. If it is,
                // we fall back to renderer 0 and say so rather than pretending it matched.
                MelonLogger.Warning($"[BloonsVR] URP camera data not copied ({e.GetType().Name}); using renderer 0");
            }
        }

        /// <summary>Drop the rig and give the frame back to BTD6.</summary>
        public void Shutdown()
        {
            if (IsActive)
                RestoreCameraSettings();

            IsActive = false;
            CursorLocked = true;

            // The player object owns the camera, so destroying it takes both. Unity defers the actual
            // destroy to the end of the frame, which is fine - nothing else references them.
            if (_playerObject != null)
                UnityEngine.Object.Destroy(_playerObject);

            _playerObject = null;
            _cameraObject = null;
            _camera = null;
            _cameraSettingsApplied = false;
            _spawned = false;
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>Release or re-grab the mouse cursor while staying in first person.</summary>
        public void SetCursorLocked(bool locked)
        {
            if (CursorLocked == locked)
                return;

            CursorLocked = locked;
            LockCursor(locked);
            MelonLogger.Msg(locked
                ? "[BloonsVR] cursor locked - mouse look active"
                : "[BloonsVR] cursor released - click BTD6 menus, TAB to re-lock");
        }

        /// <summary>Hand the view back to BTD6, or take it again.</summary>
        public void SetActive(bool active)
        {
            if (IsActive == active)
                return;

            IsActive = active;

            if (active)
            {
                // Re-enable our camera. This used to be missing, so after a single V-off the rig stayed
                // invisible forever and every later press of V looked like it did nothing.
                if (_camera != null)
                    _camera.enabled = true;

                ApplyRenderOwnership();
                LockCursor(true);
                MelonLogger.Msg("[BloonsVR] first person ON - our camera full-screen, drawn over BTD6's");
            }
            else
            {
                // Not BTD6's camera that gets switched on here: it was never switched off. Ours simply
                // stops rendering and the game is the only thing drawing again.
                if (_camera != null)
                    _camera.enabled = false;

                MelonLogger.Msg("[BloonsVR] first person OFF - BTD6 full-screen");
                LockCursor(false);
            }
        }

        /// <summary>
        /// Take the whole frame: our camera full-screen and late in the draw order.
        ///
        /// BTD6's camera is not touched at all. It keeps its own full-screen rect and its own depth, draws
        /// first, and ours clears over the top of it because our depth is far higher. Disabling BTD6's
        /// camera instead was tried twice and is a dead end — it produced a frame with nothing drawing in
        /// it, which reads as a frozen screen while the simulation runs on.
        ///
        /// Re-asserted every frame rather than set once. It is two property writes, and BTD6 does rewrite
        /// camera state during a match, so re-asserting turns "somebody changed our camera behind our back"
        /// from an invisible failure into a non-event.
        /// </summary>
        private void ApplyRenderOwnership()
        {
            if (_camera == null)
                return;

            _camera.rect = FullScreen;
            _camera.depth = 100f;
        }

        /// <summary>Drop the player in, above the buildable part of the map.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            _playerPosition = position;
            _yaw = yaw;
            _pitch = 20f;
            _spawned = true;
        }

        /// <summary>Look, move, then push the pose onto the camera. Runs once per frame.</summary>
        public void Tick()
        {
            if (!IsActive)
                return;

            if (_camera == null)
                ResolveCamera();

            if (!_spawned)
                Teleport(BloonsVRMod.SpawnPoint, 0f);

            // --- Look ------------------------------------------------------------
            // Suppressed while the cursor is free so clicking a BTD6 menu does not swing the camera.
            var mouse = CursorLocked ? Mouse.current : null;
            if (mouse != null)
            {
                var delta = mouse.delta.ReadValue();
                _yaw += delta.x * MouseSensitivity * 0.05f;
                _pitch -= delta.y * MouseSensitivity * 0.05f;
                _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            }

            // --- Move ------------------------------------------------------------
            // InputReader, not Keyboard.current: see its comment for why that was returning false for
            // every movement key while mouse look worked fine.
            float strafe = 0f, forward = 0f, lift = 0f;
            if (InputReader.Held(Key.W)) forward += 1f;
            if (InputReader.Held(Key.S)) forward -= 1f;
            if (InputReader.Held(Key.D)) strafe += 1f;
            if (InputReader.Held(Key.A)) strafe -= 1f;
            if (InputReader.Held(Hotkeys.Up)) lift += 1f;
            if (InputReader.Held(Hotkeys.Down)) lift -= 1f;

            var rotation = Quaternion.Euler(0f, _yaw, 0f);
            var move = rotation * Vector3.forward * forward + rotation * Vector3.right * strafe;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            float speed = InputReader.Held(Key.LeftShift) ? SprintSpeed : WalkSpeed;
            var position = _playerPosition + move * (speed * Time.deltaTime);

            // Snap to the ground unless the player is deliberately flying up or down.
            if (Mathf.Approximately(lift, 0f))
            {
                var ground = Btd6Map.GroundHeight(position, _playerPosition.y + GroundProbeUp, -GroundProbeDown);
                position.y = Mathf.Lerp(_playerPosition.y, ground, 1f - Mathf.Exp(-20f * Time.deltaTime));
            }
            else
            {
                position.y += lift * VerticalSpeed * Time.deltaTime;
            }

            _playerPosition = position;

            // --- Drive the rig ---------------------------------------------------
            if (_camera == null || _playerObject == null)
                return;

            if (!_cameraSettingsApplied)
            {
                _camera.enabled = true;
                _cameraSettingsApplied = true;
            }

            ApplyRenderOwnership();

            // Body carries position and yaw; the camera rides on top carrying pitch. Matching how a body
            // actually stands, and it means phase 2 only has to find the HMD's local offset from the head
            // rather than re-deriving the whole pose.
            var body = _playerObject.transform;
            body.position = _playerPosition;
            body.rotation = Quaternion.Euler(0f, _yaw, 0f);

            var head = _cameraObject.transform;
            head.localPosition = Vector3.up * EyeHeight;
            head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>
        /// Find the camera that actually renders the match.
        ///
        /// <see cref="Camera.main"/> comes first: it is the camera tagged MainCamera, which is the one
        /// Unity presents to the player by definition. <c>InGame.instance.sceneCamera</c> is the fallback,
        /// but it is a plain field on InGame and nothing guarantees it is the presenting camera.
        /// Whichever is chosen is logged with enough detail to identify it next run.
        /// </summary>
        private void ResolveCamera()
        {
            // The rig owns its camera now. If it somehow got dropped, rebuild it rather than silently
            // rendering nothing.
            if (_camera == null)
                CreateOwnCamera();
        }

        private void RestoreCameraSettings()
        {
            // Nothing to put back. BTD6's camera was never modified, and our camera is destroyed with the
            // player object.
            LockCursor(false);
        }

        internal static string Describe(Camera camera)
        {
            if (camera == null)
                return "NULL";

            return $"name={camera.name} tag={camera.tag} enabled={camera.enabled} " +
                   $"active={camera.gameObject.activeInHierarchy} ortho={camera.orthographic} " +
                   $"fov={camera.fieldOfView} depth={camera.depth} " +
                   $"pos={camera.transform.position} rot={camera.transform.eulerAngles} " +
                   $"targetTexture={(camera.targetTexture == null ? "none" : camera.targetTexture.name)}";
        }
    }
}