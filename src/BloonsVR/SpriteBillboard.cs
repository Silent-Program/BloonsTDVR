using Il2CppAssets.Scripts.Unity.Display;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace BloonsVR
{
    /// <summary>
    /// Makes every 2D sprite in the match face the player.
    ///
    /// BTD6 draws everything from a fixed top-down camera, so its sprites are authored for one viewing
    /// angle. From inside a first-person camera they read as flat cardboard. The fix is the oldest trick
    /// in games: copy the camera's rotation onto the sprite, which is exactly what BindingOfBloons does
    /// for its Isaac billboard.
    ///
    /// Why copy the camera's rotation rather than <c>Quaternion.LookRotation(pos - camPos)</c>: a
    /// SpriteRenderer's visible face is its local -Z, and a camera looks along its local +Z, so assigning
    /// the camera's rotation is what puts that -Z in front of the player. The explicit LookRotation form
    /// would face the sprite backwards.
    ///
    /// This deliberately includes everything with a sprite on it — bloons, towers, projectiles, decals,
    /// range indicators — because that is what makes the breakage visible and therefore fixable. Anything
    /// that should lie flat on the ground (map walls, range circles, track arrows) will pop up on its side;
    /// <see cref="ShouldBillboard"/> is the place to opt a node back out.
    /// </summary>
    internal static class SpriteBillboard
    {
        /// <summary>Master switch, toggled with the B key.</summary>
        internal static bool Enabled { get; private set; } = true;

        /// <summary>Nodes billboarded on the last tick. Reported in the log.</summary>
        internal static int Billboarded { get; private set; }

        /// <summary>
        /// The camera sprites should face. This is the rig's own camera, published each tick — not
        /// <c>Camera.main</c>, which belongs to BTD6 and is switched off while the rig is active.
        /// </summary>
        internal static Camera ActiveCamera { get; private set; }

        internal static void SetCamera(Camera camera) => ActiveCamera = camera;

        private static int _lastReported = -1;
        private static bool _reportedDisabled;
        private static bool _reportedEmpty;

        /// <summary>Harmony postfix on BTD6's display rotation setter. See <see cref="Billboard"/>.</summary>
        internal static void OnRotationSet(UnityDisplayNode node)
        {
            RotationSets++;
            Billboard(node);
        }

        /// <summary>
        /// How many times BTD6 asked a display node to rotate or move. Reported alongside
        /// <see cref="Billboarded"/> so a silent failure is unambiguous: zeros here mean the Harmony hooks
        /// are dead, a non-zero counter with zero <see cref="Billboarded"/> means the filter is rejecting
        /// everything, and both non-zero with no visual change means BTD6 is overwriting us later in the
        /// frame than any hook we own.
        /// </summary>
        internal static int RotationSets { get; private set; }

        internal static int PositionSets { get; private set; }

        /// <summary>
        /// <c>SetQuaternionRotation</c> turned out never to be called (rotationSets stayed at 0), so the
        /// per-frame transform writer is something else. <c>SetPosition</c> is the other candidate and is
        /// hooked purely to find out whether it is the one - if it is, billboarding in its postfix wins the
        /// ordering, because it runs immediately after BTD6 touches the node.
        /// </summary>
        internal static void OnPositionSet(UnityDisplayNode node)
        {
            PositionSets++;
            Billboard(node);
        }

        internal static void Toggle()
        {
            Enabled = !Enabled;
            MelonLogger.Msg(Enabled
                ? "[BloonsVR] sprite billboarding ON"
                : "[BloonsVR] sprite billboarding OFF");
        }

        /// <summary>
        /// Apply the billboard to a single node.
        ///
        /// Called from a Harmony postfix on <c>UnityDisplayNode.SetQuaternionRotation</c>, because writing
        /// the rotation once per frame from our own tick did not stick — BTD6 sets display rotations later in
        /// the frame than BTD6 Mod Helper's <c>OnUpdate</c> fires, so it overwrote us every frame. Hooking
        /// the setter means we run immediately after BTD6 has finished, which is the only ordering that
        /// reliably wins.
        /// </summary>
        internal static void Billboard(UnityDisplayNode node)
        {
            if (!Enabled || node == null)
                return;

            if (!ShouldBillboard(node))
                return;

            var camera = ActiveCamera;
            if (camera == null)
                return;

            var rotation = camera.transform.rotation;
            var transform = node.transform;
            if (Quaternion.Dot(transform.rotation, rotation) > 0.9999f)
                return;

            transform.rotation = rotation;
            Billboarded++;
        }

        /// <summary>
        /// Catch-all sweep over every live display node, for nodes whose rotation is not driven through
        /// <c>SetQuaternionRotation</c>. Cheap, because unchanged rotations are skipped.
        /// </summary>
        internal static void Tick(Camera camera)
        {
            if (!Enabled)
            {
                if (!_reportedDisabled)
                {
                    _reportedDisabled = true;
                    MelonLogger.Msg("[BloonsVR] billboarding disabled; sprites left as BTD6 authored them");
                }

                Billboarded = 0;
                return;
            }

            if (camera == null)
                return;

            var factory = BTD_Mod_Helper.Api.Helpers.Instances.DisplayFactory;
            if (factory == null || factory.active == null)
                return;

            var rotation = camera.transform.rotation;
            List<UnityDisplayNode> nodes = factory.active;
            int count = 0;

            Billboarded = 0;

            if (nodes.Count == 0)
                return;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || !ShouldBillboard(node))
                    continue;

                var transform = node.transform;
                if (Quaternion.Dot(transform.rotation, rotation) > 0.9999f)
                    continue;

                transform.rotation = rotation;
                count++;
            }

            Billboarded = count;

            if (nodes.Count == 0 && !_reportedEmpty)
            {
                _reportedEmpty = true;
                MelonLogger.Warning("[BloonsVR] DisplayFactory.active is empty - nothing to billboard");
            }
            else if (nodes.Count > 0)
            {
                _reportedEmpty = false;
            }

            if (count != _lastReported)
            {
                _lastReported = count;
                MelonLogger.Msg(
                    $"[BloonsVR] billboarded {count} of {nodes.Count} live node(s); " +
                    $"rotSets={RotationSets} posSets={PositionSets}");
            }
        }

        /// <summary>
        /// Opt-out point for individual nodes. Anything that genuinely belongs flat on the ground should be
        /// excluded here rather than by filtering a name list at the call site.
        /// </summary>
        private static bool ShouldBillboard(UnityDisplayNode node)
        {
            // isSprite is the game's own "this is a billboard" flag. Also accept a node whose sprite
            // renderer lives on a child, which is how several prefabs are built.
            return node.isSprite || node.sprite != null || node.spriteInChildren != null;
        }

        internal static void Reset()
        {
            Enabled = true;
            Billboarded = 0;
            ActiveCamera = null;
            _lastReported = -1;
            _reportedDisabled = false;
        }
    }
}