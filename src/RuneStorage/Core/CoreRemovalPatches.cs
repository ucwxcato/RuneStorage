using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RuneStorage
{
    // A RuneStorage Core that still holds items cannot be removed with the Hammer. Valheim's removal check runs on
    // both the Piece and the Container, so both are guarded. Other pieces and containers are untouched.
    internal static class CoreRemovalGuard
    {
        private const float NoticeCooldownSeconds = 2f;
        private static float _lastNoticeTime = -10f;

        // Tells the local player why removal was refused. Shown only while a hammer is in hand, and at most once
        // every couple of seconds, so hover checks do not spam the screen.
        internal static void NoticeBlocked()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;

            ItemDrop.ItemData tool = player.GetCurrentWeapon();
            string toolName = tool?.m_shared?.m_name;
            if (string.IsNullOrEmpty(toolName) || toolName.IndexOf("hammer", System.StringComparison.OrdinalIgnoreCase) < 0) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastNoticeTime < NoticeCooldownSeconds) return;

            _lastNoticeTime = now;
            player.Message(MessageHud.MessageType.Center, "This RuneStorage Core still holds items. Empty it before removing it.");
        }

        internal static bool BlocksRemoval(Component root)
        {
            if (root == null) return false;

            RuneStorageCoreMarker marker = root.GetComponent<RuneStorageCoreMarker>()
                ?? root.GetComponentInChildren<RuneStorageCoreMarker>(true)
                ?? root.GetComponentInParent<RuneStorageCoreMarker>();
            if (marker == null) return false;

            Container container = root.GetComponent<Container>() ?? root.GetComponentInChildren<Container>(true);
            if (container == null) return false;

            Inventory inventory = container.GetInventory();
            return inventory != null && inventory.GetAllItems().Any(item => item != null && item.m_stack > 0);
        }
    }

    [HarmonyPatch(typeof(Piece), "CanBeRemoved")]
    internal static class CorePieceRemovalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance, ref bool __result)
        {
            if (!__result || !CoreRemovalGuard.BlocksRemoval(__instance)) return;

            __result = false;
            CoreRemovalGuard.NoticeBlocked();
        }
    }

    [HarmonyPatch(typeof(Container), "CanBeRemoved")]
    internal static class CoreContainerRemovalPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Container __instance, ref bool __result)
        {
            if (!__result || !CoreRemovalGuard.BlocksRemoval(__instance)) return;

            __result = false;
            CoreRemovalGuard.NoticeBlocked();
        }
    }
}
