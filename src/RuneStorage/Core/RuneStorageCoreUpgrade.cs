using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuneStorage.Core;
using UnityEngine;

namespace RuneStorage
{
    // Where an upgrade's ingredients come from. The Core is the default; the player's own inventory is the
    // alternative.
    internal enum RuneStorageUpgradeSource
    {
        Core,
        Player
    }

    // In-place Core tier upgrades. The Core owner validates the station, the item names, and the payment, then
    // consumes the ingredients and records the new tier. Nothing is consumed until every check has passed.
    //
    // Player-paid remote upgrades: the requester removes the exact ingredients from its own inventory and sends them
    // with the request. The owner checks that they match the approved costs exactly, then accepts them. If the owner
    // refuses, the requester gets the items back. This is the same trust model as remote deposits.
    internal static class RuneStorageCoreUpgrade
    {
        internal static int CurrentTier(ZDO zdo)
        {
            return zdo != null && zdo.GetInt(CoreTierRuntime.TierZdoKey, out int tier) ? tier : CoreTierRuntime.InitialTier;
        }

        internal static string ItemNameOf(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;
            return item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
        }

        internal static long StoredCount(Inventory inventory, string itemName, bool excludeEquipped)
        {
            if (inventory == null || string.IsNullOrEmpty(itemName)) return 0;
            return inventory.GetAllItems()
                .Where(item => item != null
                    && item.m_stack > 0
                    && !(excludeEquipped && item.m_equipped)
                    && string.Equals(ItemNameOf(item), itemName, StringComparison.Ordinal))
                .Sum(item => (long)item.m_stack);
        }

        internal static string SourceLabel(RuneStorageUpgradeSource source)
        {
            return source == RuneStorageUpgradeSource.Player ? "your inventory" : "the Core";
        }

        // Player-facing text for the Upgrade tab: the next tier, its gate, and what the chosen source holds.
        internal static string Describe(Container core, Player player, int currentTier, RuneStorageUpgradeSource source)
        {
            CoreTierCapacity.TryGetSlotCount(currentTier, out int currentSlots);

            if (currentTier >= CoreTierUpgrades.MaxTier)
            {
                return $"The Core is at its highest tier: Tier {CoreTierUpgrades.RomanNumeral(currentTier)}, {currentSlots} slots.";
            }

            int next = currentTier + 1;
            if (!CoreTierUpgrades.TryGetUpgrade(next, out string station, out TierUpgradeRequirement[] requirements)
                || !CoreTierCapacity.TryGetSlotCount(next, out int nextSlots))
            {
                return "This tier has no upgrade defined.";
            }

            Inventory sourceInventory = source == RuneStorageUpgradeSource.Player
                ? (player != null ? player.GetInventory() : null)
                : (core != null ? core.GetInventory() : null);
            bool excludeEquipped = source == RuneStorageUpgradeSource.Player;

            var lines = new List<string>
            {
                $"Tier {CoreTierUpgrades.RomanNumeral(currentTier)} now: {currentSlots} slots. Tier {CoreTierUpgrades.RomanNumeral(next)}: {nextSlots} slots.",
                $"Needs a nearby {station}. Paid from {SourceLabel(source)}:"
            };

            foreach (TierUpgradeRequirement requirement in requirements)
            {
                long held = StoredCount(sourceInventory, requirement.ItemName, excludeEquipped);
                lines.Add($"  {requirement.Amount} {requirement.ItemName}  (in {SourceLabel(source)}: {held.ToString("N0", CultureInfo.InvariantCulture)})");
            }

            lines.Add(source == RuneStorageUpgradeSource.Player
                ? "Equipped items are never used. Upgrading keeps every stored item and its position."
                : "Upgrading keeps every stored item and its position.");
            return string.Join("\n", lines);
        }

        // Client side, before anything is removed: does this inventory hold every ingredient (unequipped)?
        internal static bool HasPayment(Inventory inventory, TierUpgradeRequirement[] requirements)
        {
            if (inventory == null || requirements == null) return false;
            return requirements.All(requirement => StoredCount(inventory, requirement.ItemName, true) >= requirement.Amount);
        }

        // Client side: removes the exact ingredients from the inventory and returns them as copies so they can be
        // refunded if the owner refuses. Returns null if the inventory does not hold the full payment.
        internal static List<ItemDrop.ItemData> TakePayment(Inventory inventory, TierUpgradeRequirement[] requirements)
        {
            if (!HasPayment(inventory, requirements)) return null;

            var copies = new List<ItemDrop.ItemData>();
            foreach (TierUpgradeRequirement requirement in requirements)
            {
                int remaining = requirement.Amount;
                foreach (ItemDrop.ItemData item in OrderedMatches(inventory, requirement.ItemName, true))
                {
                    if (remaining <= 0) break;

                    int take = Math.Min(remaining, item.m_stack);
                    ItemDrop.ItemData copy = item.Clone();
                    copy.m_stack = take;
                    copies.Add(copy);

                    item.m_stack -= take;
                    remaining -= take;
                    if (item.m_stack <= 0) inventory.RemoveItem(item);
                }
            }

            RuneStorageTransfers.NotifyChanged(inventory);
            return copies;
        }

        // Owner side: a player-paid payload is accepted only when it holds exactly the approved ingredients.
        internal static bool PaymentMatches(List<ItemDrop.ItemData> payment, TierUpgradeRequirement[] requirements)
        {
            if (payment == null || requirements == null) return false;
            if (payment.Any(item => item == null || item.m_shared == null || item.m_stack <= 0 || item.m_equipped)) return false;

            foreach (TierUpgradeRequirement requirement in requirements)
            {
                long paid = payment
                    .Where(item => string.Equals(ItemNameOf(item), requirement.ItemName, StringComparison.Ordinal))
                    .Sum(item => (long)item.m_stack);
                if (paid != requirement.Amount) return false;
            }

            int expectedItems = requirements.Sum(requirement => requirement.Amount);
            return payment.Sum(item => (long)item.m_stack) == expectedItems;
        }

        // Owner-only. Returns the status message; ok is true only when the Core was upgraded.
        internal static string Upgrade(
            Player player,
            RuneStorageTerminalMarker terminal,
            Container core,
            RuneStorageCoreMarker marker,
            RuneStorageUpgradeSource source,
            List<ItemDrop.ItemData> requesterPayment,
            out bool ok)
        {
            ok = false;
            if (player == null || terminal == null || core == null || marker == null)
            {
                return "The terminal or Core is no longer available. Nothing was upgraded.";
            }

            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker resolvedMarker, out Container resolvedCore, out string reason))
            {
                return reason;
            }

            if (resolvedMarker != marker || resolvedCore != core)
            {
                return "The Core connected to this terminal changed. Reopen the terminal and try again.";
            }

            if (!core.IsOwner())
            {
                return "The Core owner must run this upgrade. Nothing was upgraded.";
            }

            ZDO zdo = marker.Zdo;
            if (zdo == null)
            {
                return "The Core's saved data is not ready. Nothing was upgraded.";
            }

            Inventory coreInventory = core.GetInventory();
            if (coreInventory == null)
            {
                return "The Core's inventory is not ready. Nothing was upgraded.";
            }

            int current = CurrentTier(zdo);
            if (current >= CoreTierUpgrades.MaxTier)
            {
                return $"The Core is already at Tier {CoreTierUpgrades.RomanNumeral(current)}.";
            }

            int next = current + 1;
            if (!CoreTierUpgrades.IsNextTier(current, next)
                || !CoreTierUpgrades.TryGetUpgrade(next, out string station, out TierUpgradeRequirement[] requirements)
                || !CoreTierCapacity.TryGetDimensions(next, out int width, out int height))
            {
                return "This upgrade is not defined. Nothing was upgraded.";
            }

            string roman = CoreTierUpgrades.RomanNumeral(next);
            if (!RuneStorageCrafting.IsStationNearbyNamed(marker, terminal, station))
            {
                return $"A nearby {station} is required for Tier {roman}. No items were used.";
            }

            // Confirm each item name exists in Valheim before accepting the upgrade.
            foreach (TierUpgradeRequirement requirement in requirements)
            {
                if (ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(requirement.ItemName) == null)
                {
                    Plugin.Log.LogError($"RuneStorage tier upgrade blocked: ObjectDB has no item named '{requirement.ItemName}'.");
                    return $"Valheim has no item named {requirement.ItemName}, so Tier {roman} cannot be paid for yet. No items were used.";
                }
            }

            bool remotePlayerPayment = source == RuneStorageUpgradeSource.Player && requesterPayment != null;
            Inventory paymentInventory = null;
            bool excludeEquipped = source == RuneStorageUpgradeSource.Player;

            if (remotePlayerPayment)
            {
                if (!PaymentMatches(requesterPayment, requirements))
                {
                    return $"The ingredients sent for Tier {roman} did not match the approved cost. Nothing was upgraded.";
                }
            }
            else
            {
                paymentInventory = source == RuneStorageUpgradeSource.Player ? player.GetInventory() : coreInventory;
                if (paymentInventory == null)
                {
                    return "The payment inventory is not ready. Nothing was upgraded.";
                }

                foreach (TierUpgradeRequirement requirement in requirements)
                {
                    long held = StoredCount(paymentInventory, requirement.ItemName, excludeEquipped);
                    if (held < requirement.Amount)
                    {
                        return $"{SourceLabel(source)} needs {requirement.Amount - held} more {requirement.ItemName} for Tier {roman}. No items were used.";
                    }
                }
            }

            if (!RuneStorageTransactions.TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling another operation. Try again in a moment.";
            }

            try
            {
                if (paymentInventory != null)
                {
                    foreach (TierUpgradeRequirement requirement in requirements)
                    {
                        ConsumeItems(paymentInventory, requirement.ItemName, requirement.Amount, excludeEquipped);
                    }

                    RuneStorageTransfers.NotifyChanged(paymentInventory);
                }

                zdo.Set(CoreTierRuntime.TierZdoKey, next);
                if (coreInventory.GetHeight() < height)
                {
                    coreInventory.SetHeight(height);
                }

                Plugin.Log.LogInfo($"RuneStorage Core upgraded to Tier {roman} ({width * height} slots), paid from {SourceLabel(source)}.");
                ok = true;
                return $"Upgraded the Core to Tier {roman} ({width * height} slots), paid from {SourceLabel(source)}.";
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage tier upgrade failed: {exception}");
                marker.SetReadOnly("A tier upgrade stopped partway. The Core is read-only until its items are checked.");
                return "The upgrade stopped partway through. The Core is read-only; check its items before trying again.";
            }
            finally
            {
                RuneStorageTransactions.EndCoreOperation(operationId);
            }
        }

        private static IEnumerable<ItemDrop.ItemData> OrderedMatches(Inventory inventory, string itemName, bool excludeEquipped)
        {
            return inventory.GetAllItems()
                .Where(candidate => candidate != null
                    && candidate.m_stack > 0
                    && !(excludeEquipped && candidate.m_equipped)
                    && string.Equals(ItemNameOf(candidate), itemName, StringComparison.Ordinal))
                .OrderBy(candidate => candidate.m_gridPos.y)
                .ThenBy(candidate => candidate.m_gridPos.x)
                .ToList();
        }

        private static void ConsumeItems(Inventory inventory, string itemName, int amount, bool excludeEquipped)
        {
            int remaining = amount;
            foreach (ItemDrop.ItemData item in OrderedMatches(inventory, itemName, excludeEquipped).ToList())
            {
                if (remaining <= 0) break;

                int take = Math.Min(remaining, item.m_stack);
                item.m_stack -= take;
                remaining -= take;
                if (item.m_stack <= 0 && !inventory.RemoveItem(item))
                {
                    Plugin.Log.LogError($"RuneStorage could not remove an emptied '{itemName}' stack after a tier upgrade.");
                }
            }

            if (remaining > 0) throw new InvalidOperationException($"Only {amount - remaining} of {amount} {itemName} were available during the upgrade.");
        }
    }
}
