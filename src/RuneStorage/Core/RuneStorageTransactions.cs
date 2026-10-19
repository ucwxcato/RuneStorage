using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RuneStorage
{
    internal static class RuneStorageTransactions
    {
        private static readonly HashSet<int> ActiveCoreOperations = new HashSet<int>();

        internal static bool TryBeginCoreOperation(RuneStorageCoreMarker marker, out int operationId)
        {
            operationId = marker != null ? marker.GetInstanceID() : 0;
            if (marker == null) return false;

            lock (ActiveCoreOperations)
            {
                return ActiveCoreOperations.Add(operationId);
            }
        }

        internal static void EndCoreOperation(int operationId)
        {
            lock (ActiveCoreOperations)
            {
                ActiveCoreOperations.Remove(operationId);
            }
        }

        internal static string DepositAll(Player player, RuneStorageTerminalMarker terminal)
        {
            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker marker, out Container core, out string reason))
            {
                return reason;
            }

            if (!core.IsOwner())
            {
                return "This Core is owned by another peer. Transfers are disabled here until an authoritative network request is available.";
            }

            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();
            if (playerInventory == null || coreInventory == null)
            {
                return "A player or Core inventory is unavailable. No items were moved.";
            }

            if (!TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling a transfer. Try again in a moment.";
            }

            try
            {
                int moved = 0;
                bool skippedEquipped = false;
                bool leftItemsBehind = false;

                foreach (ItemDrop.ItemData item in playerInventory.GetAllItems().ToArray())
                {
                    if (item == null || item.m_shared == null || item.m_stack <= 0) continue;
                    if (item.m_equipped)
                    {
                        skippedEquipped = true;
                        continue;
                    }

                    int before = item.m_stack;
                    int transferred = MoveAsMuchAsFits(playerInventory, coreInventory, item, before);
                    moved += transferred;
                    if (transferred < before) leftItemsBehind = true;
                }

                if (moved == 0)
                {
                    return skippedEquipped
                        ? "No unequipped items could be stored. Equipped items stay with you."
                        : "Nothing moved. The Core may be full or the items may no longer be in your inventory.";
                }

                if (skippedEquipped && leftItemsBehind)
                {
                    return $"Stored {moved} item(s). Some items did not fit, and equipped items stay with you.";
                }

                if (skippedEquipped) return $"Stored {moved} item(s). Equipped items stay with you.";
                if (leftItemsBehind) return $"Stored {moved} item(s). Some items remain because the Core is full or cannot accept them.";
                return $"Stored {moved} item(s).";
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage deposit failed: {exception}");
                return "The deposit stopped after an inventory error. Check both inventories before retrying.";
            }
            finally
            {
                EndCoreOperation(operationId);
            }
        }

        internal static string Deposit(
            Player player,
            RuneStorageTerminalMarker terminal,
            string itemIdentity,
            int requestedAmount,
            bool allMatching = false)
        {
            if (!allMatching && requestedAmount <= 0)
            {
                return "Enter an amount greater than zero.";
            }

            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker marker, out Container core, out string reason))
            {
                return reason;
            }

            if (!core.IsOwner())
            {
                return "This Core is owned by another peer. Transfers are disabled here until an authoritative network request is available.";
            }

            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();
            if (playerInventory == null || coreInventory == null)
            {
                return "A player or Core inventory is unavailable. No items were moved.";
            }

            if (!TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling a transfer. Try again in a moment.";
            }

            try
            {
                List<ItemDrop.ItemData> selected = playerInventory.GetAllItems()
                    .Where(item => item != null
                        && item.m_shared != null
                        && !item.m_equipped
                        && string.Equals(RuneStorageItemGroups.GetIdentity(item), itemIdentity, StringComparison.Ordinal))
                    .ToList();

                if (selected.Count == 0)
                {
                    return "That item changed or is no longer in your inventory. The list has been refreshed.";
                }

                long available = selected.Sum(item => (long)Math.Max(0, item.m_stack));
                long targetLong = allMatching ? available : Math.Min(available, requestedAmount);
                int target = (int)Math.Min(targetLong, int.MaxValue);
                int remaining = target;
                int moved = 0;

                foreach (ItemDrop.ItemData item in selected)
                {
                    if (remaining <= 0) break;
                    if (item.m_stack <= 0) continue;

                    int amount = Math.Min(remaining, item.m_stack);
                    int transferred = MoveAsMuchAsFits(playerInventory, coreInventory, item, amount);
                    moved += transferred;
                    remaining -= transferred;
                }

                if (moved == 0)
                {
                    return "Nothing moved. The Core may be full or unable to accept this item.";
                }

                if (moved == target)
                {
                    if (!allMatching && available < requestedAmount)
                    {
                        return $"Deposited {moved} item(s). Only {available} were in your inventory.";
                    }

                    return $"Deposited {moved} item(s).";
                }

                return $"Deposited {moved} item(s). The Core is full or cannot accept the rest.";
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage item deposit failed: {exception}");
                return "The deposit stopped after an inventory error. Check both inventories before retrying.";
            }
            finally
            {
                EndCoreOperation(operationId);
            }
        }

        internal static string Withdraw(
            Player player,
            RuneStorageTerminalMarker terminal,
            string itemIdentity,
            int requestedAmount)
        {
            if (requestedAmount <= 0)
            {
                return "Enter an amount greater than zero.";
            }

            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker marker, out Container core, out string reason))
            {
                return reason;
            }

            if (!core.IsOwner())
            {
                return "This Core is owned by another peer. Transfers are disabled here until an authoritative network request is available.";
            }

            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();
            if (playerInventory == null || coreInventory == null)
            {
                return "A player or Core inventory is unavailable. No items were moved.";
            }

            if (!TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling a transfer. Try again in a moment.";
            }

            try
            {
                List<ItemDrop.ItemData> selected = coreInventory.GetAllItems()
                    .Where(item => string.Equals(RuneStorageItemGroups.GetIdentity(item), itemIdentity, StringComparison.Ordinal))
                    .ToList();

                if (selected.Count == 0)
                {
                    return "That item changed or is no longer stored. The list has been refreshed.";
                }

                long available = selected.Sum(item => (long)Math.Max(0, item.m_stack));
                int remaining = (int)Math.Min(requestedAmount, Math.Min(available, int.MaxValue));
                int moved = 0;

                foreach (ItemDrop.ItemData item in selected)
                {
                    if (remaining <= 0) break;
                    if (item == null || item.m_stack <= 0) continue;

                    int amount = Math.Min(remaining, item.m_stack);
                    int transferred = MoveAsMuchAsFits(coreInventory, playerInventory, item, amount);
                    moved += transferred;
                    remaining -= transferred;
                }

                if (moved == 0)
                {
                    return "Nothing moved. Your inventory may be full.";
                }

                if (moved == requestedAmount) return $"Withdrew {moved} item(s).";
                if (available < requestedAmount) return $"Withdrew {moved} item(s). Only {available} were stored.";
                return $"Withdrew {moved} item(s). The requested amount did not all fit in your inventory.";
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage withdrawal failed: {exception}");
                return "The withdrawal stopped after an inventory error. Check both inventories before retrying.";
            }
            finally
            {
                EndCoreOperation(operationId);
            }
        }

        private static int MoveAsMuchAsFits(
            Inventory source,
            Inventory destination,
            ItemDrop.ItemData item,
            int requested)
        {
            if (source == null || destination == null || item == null || requested <= 0
                || !source.GetAllItems().Any(candidate => ReferenceEquals(candidate, item))) return 0;

            int remaining = Math.Min(requested, item.m_stack);
            int moved = 0;
            string identity = RuneStorageItemGroups.GetIdentity(item);

            while (remaining > 0 && item.m_stack > 0)
            {
                int amountAvailable = Math.Min(remaining, item.m_stack);
                bool movedThisPass = false;

                foreach (ItemDrop.ItemData stack in destination.GetAllItems()
                    .Where(candidate => candidate != null
                        && candidate.m_shared != null
                        && candidate.m_shared.m_maxStackSize > candidate.m_stack
                        && string.Equals(RuneStorageItemGroups.GetIdentity(candidate), identity, StringComparison.Ordinal))
                    .OrderBy(candidate => candidate.m_gridPos.y)
                    .ThenBy(candidate => candidate.m_gridPos.x)
                    .ToArray())
                {
                    int stackRoom = stack.m_shared.m_maxStackSize - stack.m_stack;
                    int amount = Math.Min(amountAvailable, stackRoom);
                    if (amount <= 0 || !destination.CanAddItem(item, amount)) continue;

                    if (!destination.MoveItemToThis(source, item, amount, stack.m_gridPos.x, stack.m_gridPos.y)) continue;

                    moved += amount;
                    remaining -= amount;
                    movedThisPass = true;
                    break;
                }

                if (movedThisPass) continue;

                if (!TryFindEmptyPosition(destination, out Vector2i position)) break;
                if (!destination.CanAddItem(item, amountAvailable)) break;
                if (!destination.MoveItemToThis(source, item, amountAvailable, position.x, position.y)) break;

                moved += amountAvailable;
                remaining -= amountAvailable;
            }

            return moved;
        }

        private static bool TryFindEmptyPosition(Inventory inventory, out Vector2i position)
        {
            for (int y = 0; y < inventory.GetHeight(); y++)
            {
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        position = new Vector2i(x, y);
                        return true;
                    }
                }
            }

            position = new Vector2i(-1, -1);
            return false;
        }
    }
}
