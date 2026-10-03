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
    /// So: ask <em>every</em> keyboard device, not just <c>Keyboard.current</c>.
    ///
    /// Deliberately does <b>not</b> fall back to legacy <c>UnityEngine.Input</c>. Layer 1 of
    /// <see cref="InputOverride"/> prefixes those getters to deny BTD6 the movement keys, and a consumer
    /// reading the same getters would starve itself. Raw device only — that is the whole point of the
    /// split: BTD6 gets the lie, we get the truth.
    /// </summary>
    internal static class InputReader
    {

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
                    return current[key].isPressed;
                }
                catch (System.Exception)
                {
                    return false;
                }
            }

            return false;
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
            // Nothing to report any more: the legacy path is intentionally unused.
        }
    }
}