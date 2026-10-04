using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloonsVR
{
    /// <summary>
    /// Denies WASD and mouse-look to BTD6 while the rig is active, and gives them back afterwards.
    ///
    /// BTD6 runs **both** Unity input backends at once and reads different things through each: legacy
    /// <c>Input.GetKey</c> returns real states without throwing, and
    /// <c>Keyboard.current</c> is non-null with live keys. So hitting one backend proves nothing — the only
    /// evidence that matters is the symptom going away. Three layers, all scoped to "the rig is active and
    /// the cursor is locked", all restored on V / Tab / teardown:
    ///
    ///   Layer 1 — Harmony prefixes on the legacy getters. The <c>new[] { typeof(KeyCode) }</c> is
    ///             required to disambiguate from the string overload of GetKey.
    ///   Layer 2 — the game's own named actions, <c>m_Player_Move</c> and <c>m_Player_Look</c>, plus the
    ///             mouse-driven UI actions. <c>m_UI_Point</c> is the one that makes the selected tower spin
    ///             when the mouse moves.
    ///   Layer 3 — a scan of every enabled InputAction for a &lt;keyboard&gt;/w|a|s|d binding, re-asserted
    ///             each frame because BTD6 re-enables its action maps on input-mode changes.
    ///
    /// Important: our own input must come from the **raw device** (<c>Keyboard.current</c> /
    /// <c>InputSystem.devices</c>), never from legacy GetKey or from an InputAction, or Layer 1 would
    /// starve the consumer as well. See <see cref="InputReader"/>.
    /// </summary>
    internal static class InputOverride
    {
        private static bool _blocking;
        private static bool _attached;

        private static readonly System.Collections.Generic.List<InputAction> Held =
            new System.Collections.Generic.List<InputAction>();

        private static readonly System.Collections.Generic.HashSet<string> LegacyProbes =
            new System.Collections.Generic.HashSet<string>();

        internal static bool Blocking => _blocking;

        /// <summary>Frames spent blocking. Drives the periodic rescan.</summary>
        private static int _blockingFrames;

        /// <summary>Rescan cadence, in frames. Roughly once a second at 60 fps.</summary>
        private const int RescanEvery = 60;

        internal static void SetBlocking(bool blocking)
        {
            if (_blocking == blocking)
                return;

            _blocking = blocking;

            if (blocking)
            {
                _blockingFrames = 0;
                Attach();
                // Force immediate scan when cursor is locked to catch any new action maps
                ScanWasdActions();
            }
            else
            {
                Release();
            }
        }

        /// <summary>Disable-and-re-assert, once per frame, plus a periodic rescan.</summary>
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

            // Rescan periodically, not once. The first scan saw only 10 enabled actions, and BTD6 enables
            // more maps as the match goes on — switching in and out of a tower's range view, opening the
            // shop, entering placement mode. A single scan therefore misses every action map that was not
            // enabled yet, and those are exactly the ones still listening for WASD.
            if (++_blockingFrames % RescanEvery == 1)
                ScanWasdActions();
        }

        // ---------------------------------------------------------------- Layer 2

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

            Take(map.m_Player_Move, "m_Player_Move");
            Take(map.m_Player_Look, "m_Player_Look");

            // Mouse consumers. m_UI_Point is the one that rotates the selected tower under the cursor.
            // Left out on purpose: m_UI_Submit / m_UI_Click / m_UI_Navigate, because the player needs those
            // to click the shop once the cursor is released. Blocking is lifted entirely on Tab.
            Take(map.m_UI_Point, "m_UI_Point");
            Take(map.m_UI_MiddleClick, "m_UI_MiddleClick");
            Take(map.m_UI_RightClick, "m_UI_RightClick");
            Take(map.m_UI_ScrollWheel, "m_UI_ScrollWheel");

            _attached = true;
            MelonLogger.Msg($"[BloonsVR] Layer 2: took {Held.Count} BTD6 action(s) (Move, Look + mouse UI)");
        }

        private static void Take(InputAction action, string name)
        {
            if (action == null)
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

        // ---------------------------------------------------------------- Layer 3

        /// <summary>
        /// Disable any enabled action bound to a keyboard W/A/S/D.
        ///
        /// Runs repeatedly, because BTD6 enables action maps during the match. Only actions that are
        /// actually enabled are scanned, so an action map switched on later is picked up on the next pass
        /// instead of being missed for the rest of the session.
        ///
        /// IL2CPP gotcha: <c>ListEnabledActions()</c> returns an Il2Cpp list. Iterate it with <c>var</c> and
        /// its own enumerator — never assign it to a <c>System.Collections.Generic.List</c>.
        /// </summary>
        private static void ScanWasdActions()
        {
            try
            {
                var actions = InputSystem.ListEnabledActions();
                if (actions == null)
                {
                    MelonLogger.Warning("[BloonsVR] ListEnabledActions returned null");
                    return;
                }

                int found = 0;

                var enumerator = actions.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var action = enumerator.Current;
                    if (action == null || Held.Contains(action) || !BindsWasd(action))
                        continue;

                    string label;
                    try
                    {
                        var mapName = action.actionMap != null ? action.actionMap.name : "?";
                        label = $"{mapName}/{action.name}";
                    }
                    catch (System.Exception)
                    {
                        label = "?";
                    }

                    try
                    {
                        action.Disable();
                        Held.Add(action);
                        found++;
                        MelonLogger.Msg($"[BloonsVR] Layer 3: {label} <-- DISABLED");
                    }
                    catch (System.Exception e)
                    {
                        MelonLogger.Warning($"[BloonsVR] Layer 3: could not disable {label}: {e.Message}");
                    }
                }

                // Only speak up when something new was taken, or every tenth pass as a liveness check.
                // A line every second would bury the heartbeat.
                if (found > 0)
                    MelonLogger.Msg(
                        $"[BloonsVR] Layer 3: pass {_blockingFrames / RescanEvery + 1}, scanned " +
                        $"{actions.Count} enabled action(s), disabled {found} new WASD-bound action(s)");

                _lastScannedCount = actions.Count;
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning($"[BloonsVR] Layer 3 scan failed: {e.Message}");
            }
        }

        private static int _lastScannedCount = -1;

        private static bool BindsWasd(InputAction action)
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                string path;
                try
                {
                    path = bindings[i].effectivePath;
                    if (string.IsNullOrEmpty(path))
                        path = bindings[i].path;
                }
                catch (System.Exception)
                {
                    continue;
                }

                if (IsWasd(path))
                    return true;
            }

            return false;
        }

        private static bool IsWasd(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            var p = path.ToLowerInvariant();
            if (!p.StartsWith("<keyboard>"))
                return false;

            return p.EndsWith("/w") || p.EndsWith("/a") || p.EndsWith("/s") || p.EndsWith("/d") || p.EndsWith("/v");
        }

        // ---------------------------------------------------------------- shared

        internal static bool SuppressKey(KeyCode key, ref bool result)
        {
            if (!_blocking)
                return true;

            // KeyCode.W = 87, A = 65, S = 83, D = 68, V = 86.
            int k = (int)key;
            if (k != 87 && k != 65 && k != 83 && k != 68 && k != 86)
                return true;

            result = false;
            LogLegacyOnce();
            return false;
        }

        internal static bool SuppressAxis(string axisName, ref float result)
        {
            if (!_blocking)
                return true;

            if (axisName != "Horizontal" && axisName != "Vertical")
                return true;

            result = 0f;
            LogLegacyOnce();
            return false;
        }

        /// <summary>Log-only probe, so we can see whether BTD6 reads GetButton at all.</summary>
        internal static void LogButtonProbe(string buttonName)
        {
            if (!_blocking || string.IsNullOrEmpty(buttonName))
                return;

            if (LegacyProbes.Add(buttonName))
                MelonLogger.Msg($"[BloonsVR] Layer 1 probe: BTD6 called Input.GetButton(\"{buttonName}\")");
        }

        private static bool _legacyLogged;

        private static void LogLegacyOnce()
        {
            if (_legacyLogged)
                return;

            _legacyLogged = true;
            MelonLogger.Msg("[BloonsVR] Layer 1: legacy UnityEngine.Input prefixes are live");
        }

        internal static string HoldState()
        {
            if (!_blocking)
                return "released";

            if (!_attached)
                return "not-attached";

            for (int i = 0; i < Held.Count; i++)
            {
                var action = Held[i];
                if (action != null && action.enabled)
                    return "LEAKED";
            }

            // Holding N actions over M enabled ones. M growing while N stays put is the signature of
            // BTD6 enabling an action map we have not got to yet, which is what the periodic rescan is for.
            return $"blocked({Held.Count} held, {_lastScannedCount} enabled)";
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

            if (Held.Count > 0)
                MelonLogger.Msg($"[BloonsVR] released {Held.Count} BTD6 action(s) back to the game");

            Held.Clear();
            _attached = false;
            _lastScannedCount = -1;
            _blockingFrames = 0;
            LegacyProbes.Clear();

            // Force immediate rescan when cursor is released to catch any new action maps
            ScanWasdActions();
        }
    }

    // --- Layer 1: legacy UnityEngine.Input --------------------------------------
    // The explicit parameter list is required: GetKey is overloaded on (KeyCode) and (string), and
    // without it Harmony cannot tell them apart.

    [HarmonyPatch(typeof(Input), "GetKey", new[] { typeof(KeyCode) })]
    internal static class BlockLegacyGetKey
    {
        static BlockLegacyGetKey()
        {
            MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetKey patch class loaded");
            // Prevent static constructor optimization
            _ = typeof(BlockLegacyGetKey);
        }

        private static bool Prefix(KeyCode key, ref bool __result)
            => InputOverride.SuppressKey(key, ref __result);
    }

    [HarmonyPatch(typeof(Input), "GetKeyDown", new[] { typeof(KeyCode) })]
    internal static class BlockLegacyGetKeyDown
    {
        static BlockLegacyGetKeyDown()
        {
            MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetKeyDown patch class loaded");
            _ = typeof(BlockLegacyGetKeyDown);
        }

        private static bool Prefix(KeyCode key, ref bool __result)
            => InputOverride.SuppressKey(key, ref __result);
    }

    [HarmonyPatch(typeof(Input), "GetKeyUp", new[] { typeof(KeyCode) })]
    internal static class BlockLegacyGetKeyUp
    {
        static BlockLegacyGetKeyUp()
        {
            MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetKeyUp patch class loaded");
            _ = typeof(BlockLegacyGetKeyUp);
        }

        private static bool Prefix(KeyCode key, ref bool __result)
            => InputOverride.SuppressKey(key, ref __result);
    }

    [HarmonyPatch(typeof(Input), "GetAxis", new[] { typeof(string) })]
    internal static class BlockLegacyGetAxis
    {
        static BlockLegacyGetAxis()
        {
            MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetAxis patch class loaded");
            _ = typeof(BlockLegacyGetAxis);
        }

        private static bool Prefix(string axisName, ref float __result)
            => InputOverride.SuppressAxis(axisName, ref __result);
    }

    [HarmonyPatch(typeof(Input), "GetAxisRaw", new[] { typeof(string) })]
    internal static class BlockLegacyGetAxisRaw
    {
        static BlockLegacyGetAxisRaw()
        {
            MelonLogger.Msg("[BloonsVR] Layer 1: BlockLegacyGetAxisRaw patch class loaded");
            _ = typeof(BlockLegacyGetAxisRaw);
        }

        private static bool Prefix(string axisName, ref float __result)
            => InputOverride.SuppressAxis(axisName, ref __result);
    }

    /// <summary>Log only — we do not suppress, we just learn whether BTD6 uses GetButton.</summary>
    [HarmonyPatch(typeof(Input), "GetButton", new[] { typeof(string) })]
    internal static class ProbeLegacyGetButton
    {
        private static void Postfix(string buttonName)
            => InputOverride.LogButtonProbe(buttonName);
    }
}