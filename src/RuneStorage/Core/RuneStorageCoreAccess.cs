using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneStorage
{
    internal static class RuneStorageCoreAccess
    {
        internal const float TerminalUseDistance = 5f;
        internal static float CoreRange => Plugin.CoreRangeMeters.Value;
        private static readonly MethodInfo CheckAccessMethod = AccessTools.Method(typeof(Container), "CheckAccess");
        private static readonly FieldInfo PrivateAreaList = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
        private static readonly MethodInfo PrivateAreaIsEnabled = AccessTools.Method(typeof(PrivateArea), "IsEnabled");
        private static readonly MethodInfo PrivateAreaIsInside = AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });
        private static readonly MethodInfo PrivateAreaIsPermitted = AccessTools.Method(typeof(PrivateArea), "IsPermitted", new[] { typeof(long) });

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

            // Same rule as chests: anyone can use the terminal unless a ward or private area denies them.
            if (!HasAreaAccess(terminal.transform.position, player.GetPlayerID()))
            {
                reason = "This RuneStorage Terminal is inside a ward you do not have access to.";
                return false;
            }

            RuneStorageCoreMarker[] nearbyCores = UnityEngine.Object.FindObjectsByType<RuneStorageCoreMarker>(FindObjectsSortMode.None)
                .Where(candidate => candidate != null
                    && Vector3.Distance(candidate.transform.position, terminal.transform.position) <= CoreRange)
                .ToArray();

            if (nearbyCores.Length == 0)
            {
                reason = $"No RuneStorage Core found within {CoreRange:0.#} meters of this terminal.";
                Plugin.DebugLog($"Terminal {terminal.name}: {reason}");
                return false;
            }

            if (nearbyCores.Length > 1)
            {
                reason = "More than one RuneStorage Core is in range. Keep only one near this terminal.";
                Plugin.DebugLog($"Terminal {terminal.name}: {nearbyCores.Length} Cores in range; refusing ambiguous access.");
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

        // Valheim's static PrivateArea.CheckAccess only answers for the local player, so the owner checks each
        // enabled area that covers the point against the requesting player's permission list. The PrivateArea
        // members are not public, so they are read through Harmony's AccessTools. Missing members fail closed.
        internal static bool HasAreaAccess(Vector3 point, long playerId)
        {
            if (PrivateAreaList == null || PrivateAreaIsEnabled == null || PrivateAreaIsInside == null || PrivateAreaIsPermitted == null)
            {
                Plugin.Log.LogWarning("RuneStorage could not verify ward access (PrivateArea members not found); denying terminal and Core access.");
                return false;
            }

            try
            {
                if (!(PrivateAreaList.GetValue(null) is IEnumerable areas)) return false;

                foreach (object candidate in areas)
                {
                    if (!(candidate is PrivateArea area)) continue;
                    if (!Convert.ToBoolean(PrivateAreaIsEnabled.Invoke(area, null))) continue;
                    if (!Convert.ToBoolean(PrivateAreaIsInside.Invoke(area, new object[] { point, 0f }))) continue;
                    if (!Convert.ToBoolean(PrivateAreaIsPermitted.Invoke(area, new object[] { playerId }))) return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"RuneStorage ward access check failed: {exception.GetBaseException()}");
                return false;
            }
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
