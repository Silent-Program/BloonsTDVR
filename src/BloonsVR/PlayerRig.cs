using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// First-person rig: a player position/rotation kept in managed code and projected onto the camera
    /// that actually renders the match.
    ///
    /// No GameObject and no MonoBehaviour: BTD6's IL2CPP build strips <c>GameObject.AddComponent&lt;T&gt;()</c>,
    /// so neither a player object nor a camera component can be created. The camera is borrowed instead.
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

        private InGame _inGame;
        private Camera _camera;
        private GameObject _cameraObject;
        private bool _cameraSettingsApplied;

        // BTD6's own cameras, switched off while we are on screen. It rewrites their transform every frame
        // (the log shows it pinned back to (0,0,0) rot (60,0,0) right after our write), so borrowing one of
        // them is unwinnable: we would be fighting for the same transform forever. Hence our own camera.
        private readonly System.Collections.Generic.List<Camera> _btdCameras =
            new System.Collections.Generic.List<Camera>();

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

        /// <summary>Take over the view, creating our own camera and switching BTD6's off.</summary>
        public bool Initialise(InGame inGame)
        {
            _inGame = inGame;
            IsActive = true;

            CreateOwnCamera();
            LockCursor(true);
            return _camera != null;
        }

        private void CreateOwnCamera()
        {
            if (_camera != null)
                return;

            // Sample BTD6's camera settings before switching it off, so ours matches what the player is
            // used to seeing (layer mask especially - the world lives on BTD6's layers).
            var reference = Camera.main;
            if (reference == null && _inGame != null)
                reference = _inGame.sceneCamera;

            _cameraObject = new GameObject("BloonsVR_Camera");
            // Deliberately left at the scene root with no parent: nothing in the game can move it.
            _camera = _cameraObject.AddComponent(Il2CppInterop.Runtime.Il2CppType.Of<Camera>())
                .TryCast<Camera>();

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            _camera.orthographic = false;
            _camera.fieldOfView = 70f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 5000f;
            _camera.depth = 100f;

            if (reference != null)
                _camera.cullingMask = reference.cullingMask;

            SwitchOffBtdCameras(reference);
            MelonLogger.Msg($"[BloonsVR] own camera created; {Describe(_camera)}");
        }

        private void SwitchOffBtdCameras(Camera extra)
        {
            _btdCameras.Clear();

            var main = Camera.main;
            if (main != null && main != _camera)
                _btdCameras.Add(main);

            if (extra != null && extra != _camera && !_btdCameras.Contains(extra))
                _btdCameras.Add(extra);

            var scene = _inGame == null ? null : _inGame.sceneCamera;
            if (scene != null && scene != _camera && !_btdCameras.Contains(scene))
                _btdCameras.Add(scene);

            foreach (var camera in _btdCameras)
            {
                try
                {
                    camera.enabled = false;
                    MelonLogger.Msg($"[BloonsVR] disabled BTD6 camera {camera.name}");
                }
                catch (System.Exception e)
                {
                    MelonLogger.Warning($"[BloonsVR] could not disable {camera.name}: {e.Message}");
                }
            }
        }

        /// <summary>Put BTD6's cameras back exactly as we found them and drop ours.</summary>
        public void Shutdown()
        {
            if (IsActive)
                RestoreCameraSettings();

            IsActive = false;
            CursorLocked = true;

            foreach (var camera in _btdCameras)
            {
                try
                {
                    if (camera != null)
                        camera.enabled = true;
                }
                catch (System.Exception)
                {
                    // The camera is going away anyway.
                }
            }

            _btdCameras.Clear();

            if (_cameraObject != null)
                UnityEngine.Object.Destroy(_cameraObject);

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
                LockCursor(true);
                MelonLogger.Msg("[BloonsVR] first person ON - own camera, BTD6 cameras off");
            }
            else
            {
                // Hand the view back: our camera off, BTD6's back on.
                if (_camera != null)
                    _camera.enabled = false;

                foreach (var camera in _btdCameras)
                {
                    try
                    {
                        if (camera != null)
                            camera.enabled = true;
                    }
                    catch (System.Exception)
                    {
                        // Ignore.
                    }
                }

                MelonLogger.Msg("[BloonsVR] first person OFF - BTD6 camera restored");
                LockCursor(false);
            }
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

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

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
            float strafe = 0f, forward = 0f, lift = 0f;
            if (keyboard.wKey.isPressed) forward += 1f;
            if (keyboard.sKey.isPressed) forward -= 1f;
            if (keyboard.dKey.isPressed) strafe += 1f;
            if (keyboard.aKey.isPressed) strafe -= 1f;
            if (keyboard[Hotkeys.Up].isPressed) lift += 1f;
            if (keyboard[Hotkeys.Down].isPressed) lift -= 1f;

            var rotation = Quaternion.Euler(0f, _yaw, 0f);
            var move = rotation * Vector3.forward * forward + rotation * Vector3.right * strafe;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            float speed = keyboard.leftShiftKey.isPressed ? SprintSpeed : WalkSpeed;
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

            // --- Drive the camera -------------------------------------------------
            if (_camera == null)
                return;

            if (!_cameraSettingsApplied)
                ApplyCameraSettings();

            var transform = _camera.transform;
            transform.position = position + Vector3.up * EyeHeight;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
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

        private void ApplyCameraSettings()
        {
            if (_camera == null || _cameraSettingsApplied)
                return;

            _camera.enabled = true;
            _cameraSettingsApplied = true;
        }

        private void RestoreCameraSettings()
        {
            // Nothing to restore any more: our camera is ours and gets destroyed, and BTD6's cameras were
            // only ever switched off, never modified.
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