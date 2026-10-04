using System.Collections.Generic;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Models.Map;
using MelonLoader;
using MelonLogger = MelonLoader.MelonLogger;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.Key;

namespace BloonsVR
{
    /// <summary>
    /// Places towers by firing a ray out of the middle of the screen and asking BTD6's own
    /// <c>TowerManager.CreateTower</c> to build at the hit point.
    ///
    /// V key toggles mouse lock/unlock (first person look on/off).
    /// C cycles tower, F places tower.
    /// No TAB functionality - V toggles mouse lock.
    /// </summary>
    public class TowerPlacer
    {
        /// <summary>How far the placement ray reaches before giving up.</summary>
        public const float MaxRange = 500f;

        private readonly PlayerRig _rig;
        private readonly List<string> _towerIds = new List<string>();
        private int _index = -1;
        private string _status = "ready";
        private bool _hasAim;
        private Vector3 _aimPoint;

        public TowerPlacer(PlayerRig rig) => _rig = rig;

        /// <summary>Last resolved world aim point, valid when <see cref="HasAim"/>.</summary>
        public Vector3 AimPoint => _aimPoint;

        public bool HasAim => _hasAim;

        /// <summary>Base id of the tower the reticle will place.</summary>
        public string SelectedTowerId => _index >= 0 && _index < _towerIds.Count ? _towerIds[_index] : null;

        /// <summary>One line describing what the reticle is pointing at, for the console.</summary>
        public string Status => _hasAim
            ? $"{SelectedTowerId ?? "-"} at {_aimPoint} - {_status}"
            : $"{SelectedTowerId ?? "-"} - {_status}";

        /// <summary>Runs once per frame. Handles mouse lock toggle, aim ray and placement.</summary>
        public void Tick()
        {
            if (_rig == null)
                return;

            // V key toggles mouse lock (first person look on/off)
            if (InputReader.PressedThisFrame(Hotkeys.ToggleRig))
            {
                _rig.ToggleCursorLock();
                return;
            }

            if (InputReader.PressedThisFrame(Hotkeys.CursorToggle))
            {
                // Ignore TAB - V now handles mouse lock
                return;
            }

            if (InputReader.PressedThisFrame(Hotkeys.ToggleBillboards))
            {
                SpriteBillboard.Toggle();
                return;
            }

            if (!_rig.IsActive || _rig.RigCamera == null)
            {
                _hasAim = false;
                return;
            }

            EnsureTowerList();

            // Always aim - even when mouse is unlocked (for reticle display)
            UpdateAim();

            // Only allow cycling/placing when mouse is locked (first person active)
            if (!InputReader.PressedThisFrame(Hotkeys.CursorToggle) && _rig.CursorLocked)
            {
                if (InputReader.PressedThisFrame(Hotkeys.CycleTower))
                    CycleTower();

                if (InputReader.PressedThisFrame(Hotkeys.PlaceTower))
                    Place();
            }
        }

        // ---------------------------------------------------------------- aim

        private void UpdateAim()
        {
            _hasAim = false;

            var centre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var ray = _rig.RigCamera.ScreenPointToRay(centre);

            if (Physics.Raycast(ray, out RaycastHit hit, MaxRange, ~0, QueryTriggerInteraction.Ignore))
            {
                _aimPoint = hit.point;
                _hasAim = true;
                return;
            }

            // No collider under the reticle: fall back to a horizontal plane at the player's own ground
            // level, so placement still works on maps without ground physics.
            var plane = new Plane(Vector3.up, new Vector3(0f, _rig.PlayerPosition.y, 0f));
            if (plane.Raycast(ray, out float enter) && enter > 0f && enter < MaxRange)
            {
                _aimPoint = ray.GetPoint(enter);
                _hasAim = true;
            }
        }

        // ---------------------------------------------------------------- tower list

        /// <summary>
        /// Candidate base ids, taken from BTD6's own tower hotkey list in
        /// <c>Il2Cpp.Btd6ActionMap</c> rather than guessed. Every id here is one the game itself binds a
        /// hotkey to, so <c>GameModel.GetTower</c> should resolve all of them; anything that does not exist
        /// in the running game comes back null and is dropped, and the resolved set is logged.
        /// </summary>
        private static readonly string[] CandidateTowerIds =
        {
            "DartMonkey", "BoomerangMonkey", "BombShooter", "TackShooter", "IceMonkey", "GlueGunner",
            "Desperado", "SniperMonkey", "MonkeySub", "MonkeyBuccaneer", "MonkeyAce", "HeliPilot",
            "MortarMonkey", "DartlingGunner", "WizardMonkey", "SuperMonkey", "NinjaMonkey", "Alchemist",
            "BananaFarm", "SpikeFactory", "MonkeyVillage", "EngineerMonkey", "BeastHandler", "Mermonkey",
            "Skywarden", "DartlingGunner", "GlueGunner", "HeliPilot", "IceMonkey", "MonkeySub",
            "MonkeyBuccaneer", "MonkeyAce", "MonkeyVillage", "MortarMonkey", "NinjaMonkey", "SniperMonkey",
            "SpikeFactory", "SuperMonkey", "TackShooter", "WizardMonkey"
        };

        private void EnsureTowerList()
        {
            if (_towerIds.Count > 0)
                return;

            var model = Btd6Map.Model;
            if (model == null)
                return;

            foreach (var id in CandidateTowerIds)
            {
                var tower = model.GetTower(id);
                if (tower != null && tower.tier == 0 && !tower.isParagon)
                    _towerIds.Add(id);
            }

            MelonLogger.Msg($"[BloonsVR] placeable towers ({_towerIds.Count}): {string.Join(", ", _towerIds)}");
        }

        // ---------------------------------------------------------------- placement

        private void CycleTower()
        {
            if (_towerIds.Count == 0)
                return;

            _index = (_index + 1) % _towerIds.Count;
            SetStatus($"selected {SelectedTowerId}");
        }

        private void Place()
        {
            if (!_hasAim || _index < 0 || _index >= _towerIds.Count)
                return;

            var model = Btd6Map.Model;
            if (model == null || model.map == null)
                return;

            var towerModel = model.GetTower(_towerIds[_index]);
            if (towerModel == null)
                return;

            var areaModel = Btd6Map.FindPlaceableArea(new Vector2(_aimPoint.x, _aimPoint.z));
            var areaId = areaModel.id;
            if (areaModel == null)
            {
                MelonLogger.Warning($"[BloonsVR] no placeable area at {_aimPoint}");
                return;
            }

            var towerManager = BTD_Mod_Helper.Api.Helpers.Instances.TowerManager;
            if (towerManager == null)
            {
                MelonLogger.Error("[BloonsVR] TowerManager not available");
                return;
            }

            var pos = Btd6Map.ToBtd3(_aimPoint);
            pos.y = Btd6Map.GroundHeight(_aimPoint, _aimPoint.y + 10f, -20f);

            try
            {
                var tower = towerManager.CreateTower(
                    towerModel, pos, 0, areaId, default,
                    null, false, true, 0f, true, -1, -1);

                if (tower != null)
                {
                    MelonLogger.Msg($"[BloonsVR] placed {towerModel.baseId} at {pos}");
                    SetStatus($"placed {towerModel.baseId}");
                }
                else
                {
                    MelonLogger.Warning("[BloonsVR] CreateTower returned null");
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Error($"[BloonsVR] placement failed: {e.Message}");
            }
        }

        private void SetStatus(string status)
        {
            _status = status;
            MelonLogger.Msg($"[BloonsVR] {status}");
        }
    }
}
