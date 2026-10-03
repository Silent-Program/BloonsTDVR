using BTD_Mod_Helper.Api.Helpers;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Map;
using Il2CppAssets.Scripts.Simulation.Towers;
using UnityEngine;

// BTD6 does not use Unity's Vector2/Vector3 in its simulation layer. Model and placement APIs take
// Il2CppAssets.Scripts.Simulation.SMath types, where SMath.Vector2 is (x, z) - the same two numbers
// Unity's Vector2 carries, but a different struct that will not implicitly convert.
using Btd2 = Il2CppAssets.Scripts.Simulation.SMath.Vector2;
using Btd3 = Il2CppAssets.Scripts.Simulation.SMath.Vector3;

namespace BloonsVR
{
    /// <summary>
    /// Thin, defensive accessors over the BTD6 simulation model.
    ///
    /// BTD6's map is a set of <see cref="AreaModel"/> polygons (MapModel.areas). Every polygon carries its
    /// own ground <c>height</c>, an <c>id</c> and an <see cref="AreaType"/>. Tower placement is XZ only:
    /// the game resolves Y from the area the point falls inside, which is why TowerManager.CreateTower
    /// wants the area id alongside the position.
    /// </summary>
    public static class Btd6Map
    {
        /// <summary>Live GameModel, or null outside a match.</summary>
        public static GameModel Model => Instances.CurrentGameModel;

        /// <summary>Live TowerManager, or null until the map has finished loading.</summary>
        public static TowerManager TowerManager => Instances.TowerManager;

        /// <summary>Unity world XZ to BTD6's 2D map coordinate (x, z).</summary>
        public static Btd2 ToBtd2(Vector2 xz) => new Btd2(xz.x, xz.y);

        /// <summary>Unity world position to BTD6's simulation vector.</summary>
        public static Btd3 ToBtd3(Vector3 position) => new Btd3(position.x, position.y, position.z);

        /// <summary>
        /// First map area whose polygon contains <paramref name="xz"/>.
        /// </summary>
        public static AreaModel FindAreaAt(Vector2 xz)
        {
            var model = Model;
            if (model == null || model.map == null)
                return null;

            var areas = model.map.areas;
            if (areas == null)
                return null;

            var probe = ToBtd2(xz);
            for (int i = 0; i < areas.Length; i++)
            {
                var area = areas[i];
                if (area == null)
                    continue;

                try
                {
                    if (area.IsPointInside(probe))
                        return area;
                }
                catch
                {
                    // A malformed polygon should not take the frame down.
                }
            }

            return null;
        }

        /// <summary>
        /// Area that a tower may legally be built on at <paramref name="xz"/>, or null.
        /// BTD6 blocks towers on the bloon track, on water and on unplaceable regions.
        /// </summary>
        public static AreaModel FindPlaceableArea(Vector2 xz)
        {
            var model = Model;
            if (model == null || model.map == null)
                return null;

            var areas = model.map.areas;
            if (areas == null)
                return null;

            var probe = ToBtd2(xz);
            AreaModel best = null;
            for (int i = 0; i < areas.Length; i++)
            {
                var area = areas[i];
                if (area == null || !IsPlaceable(area))
                    continue;

                try
                {
                    if (!area.IsPointInside(probe))
                        continue;
                }
                catch
                {
                    continue;
                }

                // Stacked areas: prefer the highest walkable surface.
                if (best == null || area.height > best.height)
                    best = area;
            }

            return best;
        }

        public static bool IsPlaceable(AreaModel area)
        {
            if (area == null || area.isDisabled || area.lockedArea || area.isBlocker)
                return false;

            switch (area.type)
            {
                case AreaType.land:
                case AreaType.ice:
                case AreaType.removable:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Ground height at a world XZ. Prefers a real physics probe (handles slopes and ramps) and
        /// falls back to the area model, which keeps the player standing even on maps whose ground has
        /// no collider.
        /// </summary>
        public static float GroundHeight(Vector3 worldPos, float probeFrom, float probeTo)
        {
            var origin = new Vector3(worldPos.x, probeFrom, worldPos.z);
            var distance = probeFrom - probeTo;
            if (distance > 0f &&
                Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance + 1f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            var area = FindAreaAt(new Vector2(worldPos.x, worldPos.z));
            if (area != null)
                return area.height;

            return worldPos.y;
        }
    }
}