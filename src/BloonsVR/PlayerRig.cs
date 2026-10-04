using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.Key;

namespace BloonsVR
{
    /// <summary>
    /// First-person rig: player state in managed code, projected onto BTD6's Scene camera.
    ///
    /// We DON'T create a camera. BTD6's Scene camera IS rendered by URP (proven by RenderProbe).
    /// We take its transform in a Harmony postfix on WorldCameraController.LateUpdate,
    /// which runs AFTER BTD6's Update pins it to (60,0,0). We win the transform fight.
    /// V toggles our control on/off.
    ///
    /// Movement writes a plain position. Ground height from Btd6Map.GroundHeight.
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
        private Camera _sceneCamera;
        private bool _hasStoredCameraState;
        private CameraClearFlags _storedClearFlags;
        private Color _storedBackgroundColor;
        private bool _storedOrthographic;
        private float _storedFOV;
        private float _storedNearClip;
        private float _storedFarClip;
        private int _storedCullingMask;
        private float _storedDepth;

        private Vector3 _playerPosition;
        private float _yaw;
        private float _pitch;
        private bool _spawned;

        public Camera RigCamera => _sceneCamera;

        public bool IsActive { get; private set; }

        public bool CursorLocked { get; private set; } = true;

        public Vector3 PlayerPosition => _playerPosition;

        public float Yaw => _yaw;

        public float Pitch => _pitch;

        public bool Initialise(InGame inGame)
        {
            _inGame = inGame;
            _sceneCamera = inGame?.sceneCamera;

            if (_sceneCamera == null)
            {
                MelonLogger.Error("[BloonsVR] No sceneCamera on InGame");
                return false;
            }

            // Store BTD6's original camera state so we can restore it on V.
            StoreCameraState();

            // Configure for first person: perspective, FOV 70, clear to dark.
            _sceneCamera.orthographic = false;
            _sceneCamera.fieldOfView = 70f;
            _sceneCamera.nearClipPlane = 0.05f;
            _sceneCamera.farClipPlane = 5000f;
            _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            _sceneCamera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            _sceneCamera.depth = 100f; // draw last (though it's the only one URP renders)
            _sceneCamera.rect = new Rect(0f, 0f, 1f, 1f);

            IsActive = true;
            LockCursor(true);

            MelonLogger.Msg($"[BloonsVR] rig created using Scene camera: {Describe(_sceneCamera)}");
            return true;
        }

        private void StoreCameraState()
        {
            if (_sceneCamera == null) return;
            _hasStoredCameraState = true;
            _storedClearFlags = _sceneCamera.clearFlags;
            _storedBackgroundColor = _sceneCamera.backgroundColor;
            _storedOrthographic = _sceneCamera.orthographic;
            _storedFOV = _sceneCamera.fieldOfView;
            _storedNearClip = _sceneCamera.nearClipPlane;
            _storedFarClip = _sceneCamera.farClipPlane;
            _storedCullingMask = _sceneCamera.cullingMask;
            _storedDepth = _sceneCamera.depth;
        }

        private void RestoreCameraState()
        {
            if (_sceneCamera == null || !_hasStoredCameraState) return;

            _sceneCamera.clearFlags = _storedClearFlags;
            _sceneCamera.backgroundColor = _storedBackgroundColor;
            _sceneCamera.orthographic = _storedOrthographic;
            _sceneCamera.fieldOfView = _storedFOV;
            _sceneCamera.nearClipPlane = _storedNearClip;
            _sceneCamera.farClipPlane = _storedFarClip;
            _sceneCamera.cullingMask = _storedCullingMask;
            _sceneCamera.depth = _storedDepth;
            _sceneCamera.rect = new Rect(0f, 0f, 1f, 1f);

            _hasStoredCameraState = false;
            MelonLogger.Msg("[BloonsVR] Scene camera state restored");
        }

        public void SetActive(bool active)
        {
            if (IsActive == active) return;

            IsActive = active;

            if (active)
            {
                // Re-apply first-person settings
                if (_sceneCamera != null)
                {
                    _sceneCamera.orthographic = false;
                    _sceneCamera.fieldOfView = 70f;
                    _sceneCamera.nearClipPlane = 0.05f;
                    _sceneCamera.farClipPlane = 5000f;
                    _sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                    _sceneCamera.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
                    _sceneCamera.depth = 100f;
                    _sceneCamera.rect = new Rect(0f, 0f, 1f, 1f);
                }
                LockCursor(true);
                MelonLogger.Msg("[BloonsVR] first person ON - controlling Scene camera");
            }
            else
            {
                RestoreCameraState();
                LockCursor(false);
                MelonLogger.Msg("[BloonsVR] first person OFF - Scene camera restored");
            }
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SetCursorLocked(bool locked)
        {
            if (CursorLocked == locked) return;
            CursorLocked = locked;
            LockCursor(locked);
            MelonLogger.Msg(locked
                ? "[BloonsVR] cursor locked - mouse look active"
                : "[BloonsVR] cursor released - click BTD6 menus, TAB to re-lock");
        }

        public void Teleport(Vector3 position, float yaw)
        {
            _playerPosition = position;
            _yaw = yaw;
            _pitch = 20f;
            _spawned = true;
        }

        public void Tick()
        {
            if (!IsActive || _sceneCamera == null) return;

            if (!_spawned)
                Teleport(BloonsVRMod.SpawnPoint, 0f);

            // --- Look ------------------------------------------------------------
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
            if (InputReader.Held(Key.W)) forward += 1f;
            if (InputReader.Held(Key.S)) forward -= 1f;
            if (InputReader.Held(Key.D)) strafe += 1f;
            if (InputReader.Held(Key.A)) strafe -= 1f;
            if (InputReader.Held(Hotkeys.Up)) lift += 1f;
            if (InputReader.Held(Hotkeys.Down)) lift -= 1f;

            var rotation = Quaternion.Euler(0f, _yaw, 0f);
            var move = rotation * Vector3.forward * forward + rotation * Vector3.right * strafe;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = InputReader.Held(Key.LeftShift) ? SprintSpeed : WalkSpeed;
            var position = _playerPosition + move * (speed * Time.deltaTime);

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

            // NOTE: Camera transform is applied in WorldCameraControllerLateUpdatePostfix (Harmony),
            // not here. This Tick only computes the pose.
        }

        public void Shutdown()
        {
            if (IsActive) RestoreCameraState();
            IsActive = false;
            CursorLocked = true;
        }

        internal static string Describe(Camera camera)
        {
            if (camera == null) return "NULL";
            return $"name={camera.name} tag={camera.tag} enabled={camera.enabled} " +
                   $"active={camera.gameObject.activeInHierarchy} ortho={camera.orthographic} " +
                   $"fov={camera.fieldOfView} depth={camera.depth} " +
                   $"pos={camera.transform.position} rot={camera.transform.eulerAngles} " +
                   $"targetTexture={(camera.targetTexture == null ? "none" : camera.targetTexture.name)}";
        }
    }
}