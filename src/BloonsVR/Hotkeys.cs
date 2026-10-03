namespace BloonsVR
{
    /// <summary>
    /// Rebindable key bindings.
    ///
    /// BTD6 has its own hotkeys (tower hotkey slots, speed-up, pause) and it keeps reading them while the
    /// rig is active, so these can collide. They are collected here to make a rebind a one-line change.
    /// </summary>
    public static class Hotkeys
    {
        /// <summary>Leave first person and hand the camera back to BTD6.</summary>
        public const UnityEngine.InputSystem.Key ToggleRig = UnityEngine.InputSystem.Key.V;

        /// <summary>Cycle to the next tower in the placement list.</summary>
        public const UnityEngine.InputSystem.Key CycleTower = UnityEngine.InputSystem.Key.C;

        /// <summary>Place the selected tower where the reticle points.</summary>
        public const UnityEngine.InputSystem.Key PlaceTower = UnityEngine.InputSystem.Key.F;

        /// <summary>Temporary lift while held (Q/E in the rig).</summary>
        public const UnityEngine.InputSystem.Key Up = UnityEngine.InputSystem.Key.E;

        /// <summary>Temporary drop while held (Q/E in the rig).</summary>
        public const UnityEngine.InputSystem.Key Down = UnityEngine.InputSystem.Key.Q;

        /// <summary>
        /// Release or re-grab the mouse cursor without leaving first person. This is what lets the player
        /// click BTD6's shop, upgrade and hero menus; the on-screen button does the same thing.
        /// </summary>
        public const UnityEngine.InputSystem.Key CursorToggle = UnityEngine.InputSystem.Key.Tab;

        /// <summary>Turn every 2D sprite so it faces the player.</summary>
        public const UnityEngine.InputSystem.Key ToggleBillboards = UnityEngine.InputSystem.Key.B;
    }
}