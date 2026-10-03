using System.Collections.Generic;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Map;
using Il2CppAssets.Scripts.Models.Towers;
using MelonLogger = MelonLoader.MelonLogger;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// Places towers by firing a ray out of the middle of the screen and asking BTD6's own
    /// <c>TowerManager.CreateTower</c> to build at the hit point.
    ///
    /// Why not drive the mouse? BTD6's normal flow is
    /// <c>InputManager.PrimeTower -&gt; EnterPlacementMode -&gt; cursorPositionWorld -&gt; TryPlace</c>, which
    /// is hard-wired to the desktop cursor position. In first person (and later on a VR controller ray)
    /// we have our own camera, so we resolve the target point ourselves and then apply the same area and
    /// cash rules the game would have applied.
    ///
    /// There is no IMGUI overlay: IMGUI needs a MonoBehaviour to host OnGUI, and BTD6's IL2CPP build
    /// strips AddComponent so we cannot host one. Status goes to the MelonLoader console instead, which
    /// is where the log goes anyway.
    ///
    /// Hotkeys live in <see cref="Hotkeys"/> because BTD6 also binds keys; change them there.
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

        /// <summary>Runs once per frame. Handles the rig toggle, the aim ray and placement.</summary>
        public void Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _rig == null)
                return;

            // The toggle has to be read before the IsActive check, otherwise turning first person off
            // would leave nothing able to turn it back on.
            if (keyboard[Hotkeys.ToggleRig].wasPressedThisFrame)
            {
                var turningOff = _rig.IsActive;
                _rig.SetActive(!_rig.IsActive);
                BloonsVRMod.SetUserDisabled(turningOff);
                SetStatus(_rig.IsActive ? "first person on" : "first person off, BTD6 camera restored");
                return;
            }

            if (keyboard[Hotkeys.CursorToggle].wasPressedThisFrame)
            {
                // Hand the cursor back without leaving first person, so the shop/upgrade menus stay
                // clickable. C and F are ignored in that state so they cannot fire through BTD6's UI.
                if (_rig.CursorLocked)
                {
                    _rig.SetCursorLocked(false);
                    return;
                }
            }

            if (!_rig.IsActive || _rig.RigCamera == null)
            {
                _hasAim = false;
                return;
            }

            EnsureTowerList();

            if (keyboard[Hotkeys.ToggleBillboards].wasPressedThisFrame)
            {
                SpriteBillboard.Toggle();
                return;
            }

            if (!_rig.CursorLocked)
            {
                // Cursor is free for clicking BTD6's menus. Keep aiming for the readout, but never place.
                UpdateAim();
                return;
            }

            UpdateAim();

            if (keyboard[Hotkeys.CycleTower].wasPressedThisFrame)
                CycleTower();

            if (keyboard[Hotkeys.PlaceTower].wasPressedThisFrame)
                Place();
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

        // ------------------------------------------------------------ tower list

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
            "Druid", "Mermonkey", "Skywarden", "BananaFarm", "SpikeFactory", "MonkeyVillage",
            "EngineerMonkey", "BeastHandler",
        };

        private void EnsureTowerList()
        {
            var model = Btd6Map.Model;
            if (model == null || _towerIds.Count > 0)
                return;

            foreach (var id in CandidateTowerIds)
            {
                var tower = model.GetTower(id);
                if (tower == null || tower.tier != 0 || tower.isParagon || tower.IsSubEntity)
                    continue;

                if (tower.cost <= 0f || _towerIds.Contains(id))
                    continue;

                _towerIds.Add(id);
            }

            _towerIds.Sort((a, b) => string.CompareOrdinal(NameOf(model, a), NameOf(model, b)));
            _index = _towerIds.Count > 0 ? 0 : -1;

            MelonLogger.Msg($"[BloonsVR] placeable towers ({_towerIds.Count}): {string.Join(", ", _towerIds)}");
        }

        private static string NameOf(GameModel model, string baseId)
        {
            try
            {
                return model.GetTower(baseId)?.name ?? baseId;
            }
            catch
            {
                return baseId;
            }
        }

        private void CycleTower()
        {
            if (_towerIds.Count == 0)
            {
                SetStatus("no towers available (not in a match?)");
                return;
            }

            _index = (_index + 1) % _towerIds.Count;
            SetStatus($"selected {SelectedTowerId}");
        }

        // ------------------------------------------------------------ placement

        private void Place()
        {
            if (!_hasAim)
            {
                SetStatus("nothing under the reticle");
                return;
            }

            var model = Btd6Map.Model;
            if (model == null)
            {
                SetStatus("no game model");
                return;
            }

            var towerManager = Btd6Map.TowerManager;
            if (towerManager == null)
            {
                SetStatus("map still loading");
                return;
            }

            var baseId = SelectedTowerId;
            if (baseId == null)
            {
                SetStatus("press C to pick a tower");
                return;
            }

            var tower = model.GetTower(baseId);
            if (tower == null)
            {
                SetStatus($"unknown tower {baseId}");
                return;
            }

            var point = _aimPoint;
            var xz = new Vector2(point.x, point.z);

            AreaModel area = Btd6Map.FindPlaceableArea(xz);
            if (area == null)
            {
                SetStatus($"cannot build on {xz}");
                return;
            }

            if (model.cash < tower.cost)
            {
                SetStatus($"not enough cash ({model.cash:0} < {tower.cost:0})");
                return;
            }

            try
            {
                // areaPlacedOn plus area.height are what make BTD6 happy: the game validates a tower
                // against the area it belongs to, not against raw XZ.
                var position = new Vector3(point.x, area.height, point.z);
                var placed = towerManager.CreateTower(tower, Btd6Map.ToBtd3(position), 0, area.id, default,
                    deductCash: true, playPlacementEffects: true);

                if (placed == null)
                {
                    SetStatus($"game refused {tower.name}");
                    MelonLogger.Warning($"[BloonsVR] CreateTower returned null for {tower.name} at {position}");
                }
                else
                {
                    SetStatus($"placed {tower.name}");
                    MelonLogger.Msg($"[BloonsVR] placed {tower.name} ({baseId}) at {position} on area {area.id}");
                }
            }
            catch (System.Exception e)
            {
                SetStatus("placement failed");
                MelonLogger.Error($"[BloonsVR] CreateTower threw: {e}");
            }
        }

        private void SetStatus(string status)
        {
            if (_status == status)
                return;

            _status = status;
            MelonLogger.Msg($"[BloonsVR] {_status}");
        }
    }
}