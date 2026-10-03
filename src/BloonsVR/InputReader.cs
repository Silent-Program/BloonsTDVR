using MelonLoader;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// Reads key state without assuming BTD6 hands us the same keyboard the player is typing on.
    ///
    /// Why this exists: the rig's mouse look works — <c>Mouse.current.delta</c> arrives fine — but polling
    /// <c>Keyboard.current.wKey.isPressed</c> returned false for every movement key, so the player never
    /// moved while BTD6 still reacted to WASD. That combination means the device we were reading is not
    /// necessarily the one carrying the events (BTD6 ships gesture/virtual-input plumbing and can change
    /// input mode mid-match).
    ///
    /// So: ask <em>every</em> keyboard device, not just <c>Keyboard.current</c>, and fall back to the legacy
    /// <c>UnityEngine.Input</c> getters in case gameplay keys are only visible there. Which path is
    /// actually live is reported in the heartbeat log, so this stops being guesswork.
    /// </summary>
    internal static class InputReader
    {
        private static bool _legacyBroken;
        private static bool _legacyLogged;

        /// <summary>True when the key is held on any keyboard we can see.</summary>
        internal static bool Held(Key key)
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (device is Keyboard keyboard)
                {
                    try
                    {
                        if (keyboard[key].isPressed)
                            return true;
                    }
                    catch (System.Exception)
                    {
                        // A keyboard that refuses to answer is not worth failing the frame over.
                    }
                }
            }

            // No device list, or a fresh input event the list has not caught up with yet.
            var current = Keyboard.current;
            if (current != null)
            {
                try
                {
                    if (current[key].isPressed)
                        return true;
                }
                catch (System.Exception)
                {
                    // Fall through to legacy.
                }
            }

            return LegacyHeld(key);
        }

        /// <summary>True on the frame the key went down, on any keyboard.</summary>
        internal static bool PressedThisFrame(Key key)
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i] is Keyboard keyboard)
                {
                    try
                    {
                        if (keyboard[key].wasPressedThisFrame)
                            return true;
                    }
                    catch (System.Exception)
                    {
                        // Ignore.
                    }
                }
            }

            var current = Keyboard.current;
            if (current != null)
            {
                try
                {
                    if (current[key].wasPressedThisFrame)
                        return true;
                }
                catch (System.Exception)
                {
                    // Ignore.
                }
            }

            return false;
        }

        private static bool LegacyHeld(Key key)
        {
            try
            {
                switch (key)
                {
                    case Key.W: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.W);
                    case Key.A: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.A);
                    case Key.S: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.S);
                    case Key.D: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.D);
                    case Key.Q: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.Q);
                    case Key.E: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.E);
                    case Key.C: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.C);
                    case Key.F: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.F);
                    case Key.B: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.B);
                    case Key.V: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.V);
                    case Key.Tab: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.Tab);
                    case Key.LeftShift: return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift);
                    default: return false;
                }
            }
            catch (System.Exception e)
            {
                if (!_legacyBroken)
                {
                    _legacyBroken = true;
                    MelonLogger.Warning($"[BloonsVR] legacy Input unusable: {e.GetType().Name}");
                }

                return false;
            }
        }

        /// <summary>Compact key state, for the heartbeat. Makes a dead input obvious in one line.</summary>
        internal static string Describe()
        {
            int keyboards = 0;
            try
            {
                var devices = InputSystem.devices;
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i] is Keyboard)
                        keyboards++;
                }
            }
            catch (System.Exception)
            {
                // Report -1 rather than throwing from a diagnostic.
                return "devices=?";
            }

            return $"kb={keyboards} W{(Held(Key.W) ? 1 : 0)} A{(Held(Key.A) ? 1 : 0)} " +
                   $"S{(Held(Key.S) ? 1 : 0)} D{(Held(Key.D) ? 1 : 0)}";
        }

        internal static void LogLegacyStatus()
        {
            if (_legacyLogged || !_legacyBroken)
                return;

            _legacyLogged = true;
            MelonLogger.Msg("[BloonsVR] input is coming from the Input System only; legacy path is dead here");
        }
    }
}