using MelonLoader;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// Takes BTD6's movement and look actions away from it while the first-person rig is active, and hands
    /// them back on V.
    ///
    /// The earlier version of this scanned <c>InputSystem.ListEnabledActions()</c> looking for bindings
    /// containing <c>&lt;Keyboard&gt;/w</c> and disabled whatever it found. It did not work: BTD6 keeps its
    /// input in its own <see cref="Il2Cpp.Btd6ActionMap"/> of named actions, and the WASD camera control
    /// lives in <c>Move</c> while the mouse control lives in <c>Look</c>. Nothing binds those to a path the
    /// scan was matching, so nothing was disabled and BTD6 kept panning and orbiting its camera at the same
    /// time as the rig. Disabling the two actions by name is exact, and kills both bugs at once.
    ///
    /// Disabled: <c>Move</c> / <c>m_Player_Move</c> (WASD) and <c>Look</c> / <c>m_Player_Look</c> (mouse).
    /// <c>Look</c> is the important one people forget — it is what makes BTD6 swing the camera around its
    /// own pivot, which reads as the view "rotating around the unmodded pivot" whenever the rig is on.
    ///
    /// Re-asserted every frame, because BTD6 re-enables its action map when the input mode changes
    /// (keyboard/gamepad/UI transitions). Our own movement polls <c>Keyboard.current</c> directly, so it is
    /// unaffected by any of this.
    /// </summary>
    internal static class InputOverride
    {
        private static bool _blocking;
        private static bool _attached;
        private static int _heldCount;

        private static readonly System.Collections.Generic.List<InputAction> Held =
            new System.Collections.Generic.List<InputAction>();

        /// <summary>True while BTD6 is being denied Move and Look.</summary>
        internal static bool Blocking => _blocking;

        internal static void SetBlocking(bool blocking)
        {
            if (_blocking == blocking)
                return;

            _blocking = blocking;
            if (blocking)
                Attach();
            else
                Release();
        }

        /// <summary>Disable-and-re-assert, once per frame.</summary>
        internal static void Tick()
        {
            if (!_blocking)
                return;

            if (!_attached)
                Attach();

            for (int i = 0; i < Held.Count; i++)
            {
                try
                {
                    var action = Held[i];
                    if (action != null && action.enabled)
                        action.Disable();
                }
                catch (System.Exception e)
                {
                    MelonLogger.Warning($"[BloonsVR] could not re-disable a BTD6 action: {e.Message}");
                }
            }
        }

        private static void Attach()
        {
            if (_attached)
                return;

            var controller = Il2Cpp.InputSystemController.instance;
            if (controller == null || controller.actionMap == null)
            {
                MelonLogger.Warning("[BloonsVR] Btd6ActionMap not ready yet; will retry");
                return;
            }

            var map = controller.actionMap;

            // These are plain fields on Btd6ActionMap. The same actions are also reachable as
            // map.Player.Move / map.Player.Look through the nested PlayerActions struct, but the fields are
            // the direct route and hold stable references we can re-enable individually later.
            Take(map.m_Player_Move, "m_Player_Move");
            Take(map.m_Player_Look, "m_Player_Look");

            _attached = true;
            _heldCount = Held.Count;

            if (_heldCount < 2)
                MelonLogger.Warning($"[BloonsVR] only found {_heldCount} of the expected 2 player actions");

            MelonLogger.Msg($"[BloonsVR] took {_heldCount} BTD6 player action(s) (Move + Look) for the rig");
        }

        private static void Take(InputAction action, string name)
        {
            if (action == null || Held.Contains(action))
                return;

            try
            {
                if (action.enabled)
                    action.Disable();

                Held.Add(action);
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[BloonsVR] could not disable BTD6 action {name}: {e.Message}");
            }
        }

        /// <summary>
        /// Whether Move and Look are currently still disabled. Reported every heartbeat so a leak is
        /// visible: if this ever reads <c>LEAKED</c>, BTD6 re-enabled its action map and is receiving WASD
        /// and mouse look again, which is exactly the symptom of "WASD still maps to BTD6 controls".
        /// </summary>
        internal static string HoldState()
        {
            if (!_blocking)
                return "released";

            if (!_attached || Held.Count == 0)
                return "not-attached";

            for (int i = 0; i < Held.Count; i++)
            {
                var action = Held[i];
                if (action != null && action.enabled)
                    return "LEAKED";
            }

            return "blocked";
        }

        private static void Release()
        {
            for (int i = 0; i < Held.Count; i++)
            {
                try
                {
                    var action = Held[i];
                    if (action != null && !action.enabled)
                        action.Enable();
                }
                catch (System.Exception)
                {
                    // The action is going away anyway.
                }
            }

            if (_heldCount > 0)
                MelonLogger.Msg($"[BloonsVR] released {_heldCount} BTD6 action(s) back to the game");

            Held.Clear();
            _heldCount = 0;
            _attached = false;
        }
    }
}