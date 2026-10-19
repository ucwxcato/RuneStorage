using System;
using System.Linq;
using HarmonyLib;

namespace RuneStorage
{
    [HarmonyPatch(typeof(Container), "Load")]
    internal static class CoreContainerLoadPatch
    {
        [HarmonyPrefix]
        private static bool BeforeLoad(Container __instance)
        {
            try
            {
                return CoreTierRuntime.PrepareInventoryForLoad(__instance);
            }
            catch (Exception exception)
            {
                RuneStorageCoreMarker marker = CoreTierRuntime.FindMarker(__instance);
                if (marker != null)
                {
                    marker.SetReadOnly(
                        $"Tier setup failed before item loading ({exception.GetType().Name}: {exception.Message}).");
                    return false;
                }

                throw;
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class CoreContainerInteractionPatch
    {
        [HarmonyPrefix]
        private static bool BeforeInteract(Container __instance, object[] __args)
        {
            RuneStorageTerminalMarker terminal = __instance.GetComponentInParent<RuneStorageTerminalMarker>();
            if (terminal != null)
            {
                Player player = __args?.OfType<Player>().FirstOrDefault() ?? Player.m_localPlayer;
                RuneStorageTerminalPanel.Open(player, terminal);
                return false;
            }

            return CoreTierRuntime.FindMarker(__instance) == null;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverName))]
    internal static class CoreContainerHoverNamePatch
    {
        [HarmonyPrefix]
        private static bool BeforeGetHoverName(Container __instance, ref string __result)
        {
            if (CoreTierRuntime.FindMarker(__instance) == null) return true;
            __result = "RuneStorage Core";
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class CoreContainerHoverTextPatch
    {
        [HarmonyPrefix]
        private static bool BeforeGetHoverText(Container __instance, ref string __result)
        {
            RuneStorageCoreMarker marker = CoreTierRuntime.FindMarker(__instance);
            if (marker == null) return true;

            __result = marker.IsReadOnly
                ? $"RuneStorage Core is unavailable: {marker.StatusMessage}"
                : "Access storage through a nearby RuneStorage Terminal.";
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverName))]
    internal static class TerminalContainerHoverNamePatch
    {
        [HarmonyPrefix]
        private static bool BeforeGetHoverName(Container __instance, ref string __result)
        {
            if (__instance.GetComponentInParent<RuneStorageTerminalMarker>() == null) return true;
            __result = "RuneStorage Terminal";
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class TerminalContainerHoverTextPatch
    {
        [HarmonyPrefix]
        private static bool BeforeGetHoverText(Container __instance, ref string __result)
        {
            if (__instance.GetComponentInParent<RuneStorageTerminalMarker>() == null) return true;

            string useHint = "Open RuneStorage";
            if (Localization.instance != null)
            {
                useHint = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Open RuneStorage");
            }

            __result = useHint;
            return false;
        }
    }
}
