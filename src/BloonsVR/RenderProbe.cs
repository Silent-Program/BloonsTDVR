using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace BloonsVR
{
    /// <summary>
    /// Reports which cameras Unity is actually being asked to render, once a second, while the rig is on.
    ///
    /// This exists because the symptom in game was <i>"pressing V just pauses the screen while the
    /// simulation goes on in the background"</i>. That combination has exactly one explanation: the frame is
    /// being submitted, but no camera in it draws anything, so the back buffer keeps the last image Unity
    /// rendered and the picture freezes while the simulation carries on. Every earlier hypothesis (wrong
    /// culling mask, stolen transform, URP renderer index) was inferred from the log rather than observed,
    /// and all of them were wrong or untestable.
    ///
    /// <c>UniversalRenderPipeline.Render</c> is the last point before the pipeline decides what to draw,
    /// and BTD6 uses the stock pipeline — there is no subclass of <c>UniversalRenderPipeline</c> or
    /// <c>RenderPipeline</c> anywhere in <c>Assembly-CSharp</c>, so this sees the real list. If our camera
    /// is missing from it, the pipeline is skipping us; if it is present, the problem is inside the render
    /// itself (layers, renderer index, or the camera drawing off-screen).
    ///
    /// Patching a plain method works in IL2CPP. It is Unity <i>message</i> methods that silently do not,
    /// which is why this is hooked on the pipeline rather than on some <c>LateUpdate</c>.
    ///
    /// The explicit argument types are load-bearing. <c>Render</c> is declared twice on the way up:
    /// <c>RenderPipeline.Render</c> is virtual and <c>UniversalRenderPipeline.Render</c> overrides it, so a
    /// name-only lookup finds both and Harmony throws
    /// <c>AmbiguousMatchException: Ambiguous match for HarmonyMethod[(class=..., methodname=Render)]</c>.
    /// The first run of this probe failed to apply for exactly that reason and logged nothing at all.
    /// </summary>
    [HarmonyPatch(
        typeof(UnityEngine.Rendering.Universal.UniversalRenderPipeline),
        nameof(UnityEngine.Rendering.Universal.UniversalRenderPipeline.Render),
        new[]
        {
            typeof(UnityEngine.Rendering.ScriptableRenderContext),
            typeof(Il2CppSystem.Collections.Generic.List<Camera>),
        })]
    internal static class RenderProbe
    {
        private const int LogEveryNCalls = 60;

        private static int _calls;
        private static bool _active;

        /// <summary>Set by the mod so the probe stays quiet when first person is off.</summary>
        internal static bool Active
        {
            get => _active;
            set => _active = value;
        }

        private static void Postfix(
            UnityEngine.Rendering.ScriptableRenderContext renderContext,
            Il2CppSystem.Collections.Generic.List<Camera> cameras)
        {
            if (!_active || cameras == null)
                return;

            // Throttled: this runs once per camera per frame, and an unthrottled log would bury the
            // heartbeat that carries the actually useful per-frame state.
            if (++_calls % LogEveryNCalls != 1)
                return;

            int count = 0;
            string summary;

            try
            {
                // An Il2Cpp list: iterate with `var`, never assign it to System.Collections.Generic.List.
                var parts = new System.Text.StringBuilder();
                parts.Append('[');

                foreach (var camera in cameras)
                {
                    if (camera == null)
                    {
                        count++;
                        continue;
                    }

                    if (count > 0)
                        parts.Append(" | ");

                    var rect = camera.rect;
                    parts.Append(camera.name);
                    parts.Append(" rect=");
                    parts.Append(rect.x.ToString("0.00"));
                    parts.Append(',');
                    parts.Append(rect.y.ToString("0.00"));
                    parts.Append(' ');
                    parts.Append(rect.width.ToString("0.00"));
                    parts.Append('x');
                    parts.Append(rect.height.ToString("0.00"));
                    parts.Append(" mask=");
                    parts.Append(camera.cullingMask);

                    count++;
                }

                parts.Append(']');
                summary = parts.ToString();
            }
            catch (System.Exception e)
            {
                summary = $"<unreadable: {e.GetType().Name}: {e.Message}>";
            }

            MelonLogger.Msg($"[BloonsVR] URP rendering {count} camera(s): {summary}");
        }
    }
}