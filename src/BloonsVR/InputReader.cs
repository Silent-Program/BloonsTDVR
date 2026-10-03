using MelonLoader;
using UnityEngine;
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
        private static readonly Key[] MovementKeys = { Key.W, Key.A, Key.S, Key.D };

        /// <summary>
        /// Frames on which each movement key was observed held, cumulative since the session started.
        ///
        /// The instantaneous reading in <see cref="Describe"/> is not enough on its own: it samples one
        /// frame per heartbeat, so "the key was never pressed" and "the sample missed the press" look
        /// identical. A cumulative counter cannot be fooled that way — hold W for three seconds and this
        /// reads ~180 while the instantaneous reading could read 0 every single time.
        /// </summary>
        private static readonly int[] MovementFrames = new int[4];

        private static int _framesSampled;
        private static int _anyMovementFrames;
        private static int _mouseMoveFrames;
        private static float _mouseTravel;

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

        /// <summary>
        /// Count this frame's movement keys so the heartbeat can report totals instead of a snapshot.
        /// Called once per frame from the mod's tick, before the rig reads anything.
        /// </summary>
        internal static void Sample()
        {
            _framesSampled++;

            bool any = false;

            for (int i = 0; i < MovementKeys.Length; i++)
            {
                if (!Held(MovementKeys[i]))
                    continue;

                MovementFrames[i]++;
                any = true;
            }

            if (any)
                _anyMovementFrames++;

            // Mouse travel is the control sample. Look works, so if the Input System is processing events
            // at all then a permanently-zero keyboard is about the keyboard specifically and not about
            // input updates not running.
            try
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var delta = mouse.delta.ReadValue();
                    float travel = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                    _mouseTravel += travel;
                    if (travel > 0.01f)
                        _mouseMoveFrames++;
                }
            }
            catch (System.Exception)
            {
                // Ignore - a diagnostic must never break the frame.
            }
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

        /// <summary>
        /// Compact input state, for the heartbeat.
        ///
        /// Two halves, and the distinction matters. The bare <c>W0 A0 S0 D0</c> is an instantaneous
        /// snapshot. The <c>held=</c> figures are cumulative frame counts since the session began, and
        /// <c>mouse=</c> is the control: it proves the Input System is delivering events at all.
        /// </summary>
        internal static string Describe()
        {
            int keyboards = 0;
            int devices = -1;

            try
            {
                var list = InputSystem.devices;
                devices = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is Keyboard)
                        keyboards++;
                }
            }
            catch (System.Exception)
            {
                // Report -1 rather than throwing from a diagnostic.
                return "devices=?";
            }

            return $"kb={keyboards}/{devices} " +
                   $"W{(Held(Key.W) ? 1 : 0)} A{(Held(Key.A) ? 1 : 0)} " +
                   $"S{(Held(Key.S) ? 1 : 0)} D{(Held(Key.D) ? 1 : 0)} " +
                   $"frames={_framesSampled} any={_anyMovementFrames} " +
                   $"held=W{MovementFrames[0]}/A{MovementFrames[1]}/S{MovementFrames[2]}/D{MovementFrames[3]} " +
                   $"mouse={_mouseMoveFrames}f/{_mouseTravel:F0}px";
        }

        /// <summary>
        /// Everything about the input devices that could explain a dead keyboard, dumped once.
        ///
        /// If the keyboard is present, enabled and added, and the mouse is moving, then the Input System is
        /// healthy and the fault is in which device carries the events — not in event processing. The
        /// <c>updateMode</c> value covers the other case: if BTD6 moved input updates to
        /// <c>ProcessEventsInFixedUpdate</c> or <c>Manual</c>, states would be read at the wrong time.
        /// </summary>
        internal static void LogDeviceDiagnostics()
        {
            try
            {
                var settings = InputSystem.settings;
                MelonLogger.Msg(
                    $"[BloonsVR] input: updateMode={settings.updateMode} " +
                    $"devices={InputSystem.devices.Count} current={(Keyboard.current == null ? "null" : "yes")}");
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[BloonsVR] input settings unreadable: {e.GetType().Name}");
            }

            try
            {
                var list = InputSystem.devices;
                for (int i = 0; i < list.Count; i++)
                {
                    var device = list[i];

                    if (device is Keyboard keyboard)
                    {
                        MelonLogger.Msg(
                            $"[BloonsVR] keyboard[{i}]: layout={keyboard.layout} " +
                            $"id={keyboard.deviceId} enabled={keyboard.enabled} added={keyboard.added} " +
                            $"display={keyboard.displayName} isCurrent={ReferenceEquals(keyboard, Keyboard.current)}");
                    }
                    else
                    {
                        MelonLogger.Msg(
                            $"[BloonsVR] device[{i}]: {device.GetType().Name} layout={device.layout} " +
                            $"enabled={device.enabled} added={device.added}");
                    }
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[BloonsVR] device list unreadable: {e.GetType().Name}");
            }
        }

        internal static void LogLegacyStatus()
        {
            // Nothing to report any more: the legacy path is intentionally unused.
        }
    }
}