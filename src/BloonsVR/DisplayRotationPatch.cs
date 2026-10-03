using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Display;

namespace BloonsVR
{
    /// <summary>
    /// Runs the billboard immediately after BTD6 writes a display node's rotation.
    ///
    /// Doing this from the mod's own per-frame tick did not work: BTD6 sets display rotations later in the
    /// frame than BTD6 Mod Helper's OnUpdate, so the change was overwritten every frame and sprites never
    /// turned. Hooking the setter means we run last.
    ///
    /// Patching a plain method works fine in IL2CPP. It is Unity *message* methods (Update, LateUpdate)
    /// that silently do not.
    /// </summary>
    [HarmonyPatch(typeof(UnityDisplayNode), nameof(UnityDisplayNode.SetQuaternionRotation))]
    internal static class DisplayRotationPatch
    {
        private static void Postfix(UnityDisplayNode __instance)
        {
            SpriteBillboard.OnRotationSet(__instance);
        }
    }

    /// <summary>
    /// The per-frame transform writer, if <c>SetPosition</c> turns out to be it. Harmless if not: it only
    /// ever billboards, and reports whether it was reached at all.
    /// </summary>
    [HarmonyPatch(typeof(UnityDisplayNode), nameof(UnityDisplayNode.SetPosition))]
    internal static class DisplayPositionPatch
    {
        private static void Postfix(UnityDisplayNode __instance)
        {
            SpriteBillboard.OnPositionSet(__instance);
        }
    }
}