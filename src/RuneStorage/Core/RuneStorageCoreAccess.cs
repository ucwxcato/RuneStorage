using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneStorage
{
    internal static class RuneStorageCoreAccess
    {
        internal const float TerminalUseDistance = 5f;
        internal const float CoreRange = 12f;
        private static readonly MethodInfo CheckAccessMethod = AccessTools.Method(typeof(Container), "CheckAccess");

        internal static bool TryResolve(
            Player player,
            RuneStorageTerminalMarker terminal,
            out RuneStorageCoreMarker marker,
            out Container container,
            out string reason)
        {
            marker = null;
            container = null;

            if (player == null || terminal == null)
            {
                reason = "The terminal or player is no longer available. Move closer and try again.";
                return false;
            }

            if (Vector3.Distance(player.transform.position, terminal.transform.position) > TerminalUseDistance)
            {
                reason = "Move closer to the RuneStorage Terminal.";
                return false;
            }

            RuneStorageCoreMarker[] nearbyCores = UnityEngine.Object.FindObjectsByType<RuneStorageCoreMarker>(FindObjectsSortMode.None)
                .Where(candidate => candidate != null
                    && Vector3.Distance(candidate.transform.position, terminal.transform.position) <= CoreRange)
                .ToArray();

            if (nearbyCores.Length == 0)
            {
                reason = "No RuneStorage Core found within 12 meters of this terminal.";
                return false;
            }

            if (nearbyCores.Length > 1)
            {
                reason = "More than one RuneStorage Core is in range. Keep only one near this terminal.";
                return false;
            }

            marker = nearbyCores[0];
            Container[] containers = marker.GetComponentsInChildren<Container>(true);
            if (containers.Length != 1)
            {
                reason = $"The RuneStorage Core has {containers.Length} native containers; exactly one is required.";
                marker = null;
                return false;
            }

            container = containers[0];
            if (marker.IsReadOnly)
            {
                reason = marker.StatusMessage ?? "The RuneStorage Core is read-only because its saved data could not be loaded safely.";
                return false;
            }

            if (container.GetInventory() == null)
            {
                reason = "The RuneStorage Core inventory is not ready. Close this panel and try again.";
                return false;
            }

            if (!HasAccess(container, player.GetPlayerID()))
            {
                reason = "You do not have access to this RuneStorage Core.";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool HasAccess(Container container, long playerId)
        {
            if (CheckAccessMethod == null) return false;

            try
            {
                return CheckAccessMethod.Invoke(container, new object[] { playerId }) is bool allowed && allowed;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"RuneStorage Core access check failed: {exception.GetBaseException()}");
                return false;
            }
        }
    }
}
