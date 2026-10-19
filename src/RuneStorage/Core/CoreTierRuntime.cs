using UnityEngine;
using RuneStorage.Core;

namespace RuneStorage
{
    internal static class CoreTierRuntime
    {
        internal const string TierZdoKey = "RuneStorageCoreTier";
        internal const int InitialTier = 1;

        internal static bool PrepareInventoryForLoad(Container container)
        {
            RuneStorageCoreMarker marker = FindMarker(container);
            if (marker == null) return true;

            ZNetView networkView = FindNetworkView(container);
            if (networkView == null)
            {
                marker.SetReadOnly("The Core's ZNetView could not be found.");
                return false;
            }

            ZDO zdo = networkView.GetZDO();
            if (zdo == null)
            {
                marker.SetReadOnly("The Core's ZDO is not available yet.");
                return false;
            }

            bool hasSavedTier = zdo.GetInt(TierZdoKey, out int tier);
            if (!hasSavedTier) tier = InitialTier;

            if (!CoreTierCapacity.TryGetDimensions(tier, out int width, out int height))
            {
                marker.SetReadOnly($"Saved capacity tier {tier} is not supported.");
                return false;
            }

            Inventory inventory = container.GetInventory();
            if (inventory == null)
            {
                marker.SetReadOnly("The native Container has no Inventory.");
                return false;
            }

            if (inventory.GetWidth() != width)
            {
                marker.SetReadOnly(
                    $"The native inventory width is {inventory.GetWidth()}, but tier {tier} requires width {width}.");
                return false;
            }

            int currentHeight = inventory.GetHeight();
            if (currentHeight > height)
            {
                marker.SetReadOnly(
                    $"The live inventory has {currentHeight} rows, but saved tier {tier} allows only {height}; refusing to hide items.");
                return false;
            }

            if (currentHeight < height)
            {
                inventory.SetHeight(height);
            }

            if (!hasSavedTier && networkView.IsOwner())
            {
                zdo.Set(TierZdoKey, tier);
            }

            marker.ClearReadOnly();
            if (marker.ShouldLogTier(tier))
            {
                Plugin.Log.LogInfo($"RuneStorage Core '{marker.name}' loaded at tier {tier} ({width * height} slots).");
            }

            return true;
        }

        internal static RuneStorageCoreMarker FindMarker(Container container)
        {
            return container == null ? null : container.GetComponentInParent<RuneStorageCoreMarker>();
        }

        private static ZNetView FindNetworkView(Container container)
        {
            ZNetView networkView = container.GetComponent<ZNetView>();
            if (networkView != null) return networkView;

            networkView = container.GetComponentInParent<ZNetView>();
            if (networkView != null) return networkView;

            return container.GetComponentInChildren<ZNetView>(true);
        }
    }
}
