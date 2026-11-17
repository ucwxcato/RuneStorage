using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneStorage
{
    internal enum RuneStorageTransferKind
    {
        DepositAll,
        DepositIdentity,
        DepositExact,
        Withdraw
    }

    // Deposits and withdrawals between a player's inventory and the Core's native Container inventory.
    //
    // Who mutates what:
    //   - The Core owner (the peer whose ZDO owns the Core) mutates the Core inventory only.
    //   - The requesting player's inventory is changed by that player's own client, because a remote
    //     character's inventory is not safely writable from another peer. Remote deposits send copies of the
    //     selected items; the owner accepts what fits and reports the accepted amounts; the requester then
    //     removes exactly those amounts. Remote withdrawals ask the owner to remove items from the Core; the
    //     requester places the returned copies, or drops any remainder at its feet.
    //   - Capacity is checked with StorageTransferPlanner before any item moves.
    internal static class RuneStorageTransfers
    {
        internal const int MaxPayloadCharacters = 512 * 1024;
        internal const int MaxItemsPerPayload = 1024;

        private static readonly MethodInfo InventoryChanged = AccessTools.Method(typeof(Inventory), "Changed", new[] { typeof(bool), typeof(bool) });
        private static readonly Dictionary<string, PendingTransfer> Pending = new Dictionary<string, PendingTransfer>(StringComparer.Ordinal);

        private sealed class SourceChunk
        {
            internal ItemDrop.ItemData Original;
            internal int Amount;
        }

        private sealed class PendingTransfer
        {
            internal RuneStorageCoreMarker Core;
            internal Player Player;
            internal RuneStorageTransferKind Kind;
            internal List<SourceChunk> Sources;
        }

        internal static string WireName(RuneStorageTransferKind kind)
        {
            switch (kind)
            {
                case RuneStorageTransferKind.DepositAll: return "deposit-all";
                case RuneStorageTransferKind.DepositIdentity: return "deposit-identity";
                case RuneStorageTransferKind.DepositExact: return "deposit-exact";
                default: return "withdraw";
            }
        }

        internal static bool TryParseWireName(string value, out RuneStorageTransferKind kind)
        {
            switch (value)
            {
                case "deposit-all": kind = RuneStorageTransferKind.DepositAll; return true;
                case "deposit-identity": kind = RuneStorageTransferKind.DepositIdentity; return true;
                case "deposit-exact": kind = RuneStorageTransferKind.DepositExact; return true;
                case "withdraw": kind = RuneStorageTransferKind.Withdraw; return true;
                default: kind = default; return false;
            }
        }

        internal static bool HasPendingTransfer(RuneStorageCoreMarker core)
        {
            return Pending.Values.Any(pending => pending.Core == core);
        }

        // Entry point from the terminal UI. Returns a status message. waiting is true when the Core owner must answer.
        internal static string Start(
            Player player,
            RuneStorageTerminalMarker terminal,
            RuneStorageTransferKind kind,
            string identity,
            int amount,
            int expectedQuantity,
            out bool waiting)
        {
            waiting = false;
            if (kind == RuneStorageTransferKind.Withdraw || kind == RuneStorageTransferKind.DepositExact)
            {
                if (amount <= 0) return "Enter an amount greater than zero.";
            }

            if (!RuneStorageCoreAccess.TryResolve(player, terminal, out RuneStorageCoreMarker marker, out Container core, out string reason))
            {
                return reason;
            }

            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();
            if (playerInventory == null || coreInventory == null)
            {
                return "A player or Core inventory is unavailable. No items were moved.";
            }

            if (HasPendingTransfer(marker))
            {
                return "A transfer is already waiting for the Core owner. Wait for it to finish.";
            }

            if (core.IsOwner())
            {
                return RunOnOwnedCore(player, marker, core, kind, identity, amount, expectedQuantity);
            }

            return StartRemote(player, marker, core, kind, identity, amount, expectedQuantity, out waiting);
        }

        // Owner side, for the local player whose client is the Core owner.
        private static string RunOnOwnedCore(
            Player player,
            RuneStorageCoreMarker marker,
            Container core,
            RuneStorageTransferKind kind,
            string identity,
            int amount,
            int expectedQuantity)
        {
            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();

            if (!RuneStorageTransactions.TryBeginCoreOperation(marker, out int operationId))
            {
                return "The Core is already handling a transfer. Try again in a moment.";
            }

            try
            {
                if (kind == RuneStorageTransferKind.Withdraw)
                {
                    return LocalWithdraw(player, playerInventory, coreInventory, identity, amount, expectedQuantity);
                }

                List<SourceChunk> sources = CollectSources(playerInventory, kind, identity, amount, out string collectMessage);
                if (sources == null) return collectMessage;

                List<ItemDrop.ItemData> copies = sources.Select(source => CopyWithAmount(source.Original, source.Amount)).ToList();
                int[] accepted = CommitDepositToCore(coreInventory, copies, kind != RuneStorageTransferKind.DepositExact, out string shortfall);
                RemoveAcceptedFromSources(playerInventory, sources, accepted);
                return DepositMessage(accepted, collectMessage, shortfall);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage local transfer failed: {exception}");
                return "The transfer stopped after an inventory error. Check both inventories before retrying.";
            }
            finally
            {
                RuneStorageTransactions.EndCoreOperation(operationId);
            }
        }

        private static string LocalWithdraw(Player player, Inventory playerInventory, Inventory coreInventory, string identity, int amount, int expectedQuantity)
        {
            Vector3 dropAt = player.transform.position + player.transform.forward + Vector3.up * 0.5f;
            List<ItemDrop.ItemData> coreStacks = CoreStacks(coreInventory, identity);
            long available = coreStacks.Sum(item => (long)Math.Max(0, item.m_stack));
            if (coreStacks.Count == 0)
            {
                return "That item changed or is no longer stored. The list has been refreshed.";
            }

            if (expectedQuantity >= 0 && available != expectedQuantity)
            {
                return $"The stored quantity changed (now {available}). The list has been refreshed; try again.";
            }

            int target = (int)Math.Min(amount, available);
            int maxStack = coreStacks[0].m_shared.m_maxStackSize;
            StorageTransferPlan plan = StorageTransferPlanner.Plan(
                playerInventory.GetWidth(),
                playerInventory.GetHeight(),
                CellsOf(playerInventory),
                new[] { new StorageChunk(0, identity, maxStack, target) });

            if (!plan.IsComplete)
            {
                return $"Your inventory has room for only {plan.TotalPlanned} of {target}. No items were moved.";
            }

            List<ItemDrop.ItemData> copies = CopiesFromCore(coreStacks, target);
            RemoveFromCore(coreInventory, coreStacks, target);
            int leftover = PlaceCopies(playerInventory, copies, dropAt);
            string message = $"Withdrew {target - leftover} item(s).";
            if (available < amount) message = $"Withdrew {target - leftover} item(s). Only {available} were stored.";
            return leftover > 0 ? $"{message} {leftover} could not be placed." : message;
        }

        private static string StartRemote(
            Player player,
            RuneStorageCoreMarker marker,
            Container core,
            RuneStorageTransferKind kind,
            string identity,
            int amount,
            int expectedQuantity,
            out bool waiting)
        {
            waiting = false;
            Inventory playerInventory = player.GetInventory();
            Inventory coreInventory = core.GetInventory();
            string requestId = Guid.NewGuid().ToString("N");
            var pending = new PendingTransfer { Core = marker, Player = player, Kind = kind };

            if (kind == RuneStorageTransferKind.Withdraw)
            {
                List<ItemDrop.ItemData> coreStacks = CoreStacks(coreInventory, identity);
                if (coreStacks.Count == 0)
                {
                    return "That item changed or is no longer stored. The list has been refreshed.";
                }

                int target = (int)Math.Min(amount, coreStacks.Sum(item => (long)Math.Max(0, item.m_stack)));
                StorageTransferPlan plan = StorageTransferPlanner.Plan(
                    playerInventory.GetWidth(),
                    playerInventory.GetHeight(),
                    CellsOf(playerInventory),
                    new[] { new StorageChunk(0, identity, coreStacks[0].m_shared.m_maxStackSize, target) });

                if (!plan.IsComplete)
                {
                    return $"Your inventory has room for only {plan.TotalPlanned} of {target}. No items were moved.";
                }

                if (!marker.SendTransferRequest(WireName(kind), identity, target, expectedQuantity, string.Empty, requestId))
                {
                    return "The Core's network request path is not ready. No items were moved.";
                }

                Pending[requestId] = pending;
                waiting = true;
                return "Sent the withdrawal to the Core owner. Waiting for confirmation...";
            }

            List<SourceChunk> sources = CollectSources(playerInventory, kind, identity, amount, out string collectMessage);
            if (sources == null) return collectMessage;

            List<ItemDrop.ItemData> copies = sources.Select(source => CopyWithAmount(source.Original, source.Amount)).ToList();
            string payload = SerializeItems(copies);
            if (payload == null)
            {
                return "These items could not be packaged for the Core owner. No items were moved.";
            }

            pending.Sources = sources;
            Pending[requestId] = pending;
            if (!marker.SendTransferRequest(WireName(kind), identity ?? string.Empty, copies.Sum(copy => copy.m_stack), -1, payload, requestId))
            {
                Pending.Remove(requestId);
                return "The Core's network request path is not ready. No items were moved.";
            }

            waiting = true;
            return "Sent the deposit to the Core owner. Waiting for confirmation...";
        }

        // Owner side of a remote request. Runs on the Core owner after the requester is resolved and checked.
        internal static void HandleRemoteRequest(
            RuneStorageCoreMarker marker,
            Container core,
            Player player,
            string kindName,
            string identity,
            int amount,
            int expectedQuantity,
            string payload,
            out bool ok,
            out string message,
            out string responsePayload)
        {
            ok = false;
            message = "The request failed validation. No items were moved.";
            responsePayload = string.Empty;

            if (!TryParseWireName(kindName, out RuneStorageTransferKind kind))
            {
                message = "Unknown transfer type. No items were moved.";
                return;
            }

            if (!core.IsOwner())
            {
                message = "The Core owner must validate this transfer. No items were moved.";
                return;
            }

            Inventory coreInventory = core.GetInventory();
            if (coreInventory == null)
            {
                message = "The Core's native inventory could not be found. No items were moved.";
                return;
            }

            if (!RuneStorageTransactions.TryBeginCoreOperation(marker, out int operationId))
            {
                message = "The Core is already handling another operation. Try again in a moment.";
                return;
            }

            try
            {
                if (kind == RuneStorageTransferKind.Withdraw)
                {
                    List<ItemDrop.ItemData> coreStacks = CoreStacks(coreInventory, identity);
                    long available = coreStacks.Sum(item => (long)Math.Max(0, item.m_stack));
                    if (coreStacks.Count == 0)
                    {
                        message = "That item changed or is no longer stored. The list has been refreshed.";
                        return;
                    }

                    if (expectedQuantity >= 0 && available != expectedQuantity)
                    {
                        message = $"The stored quantity changed (now {available}). The list has been refreshed; try again.";
                        return;
                    }

                    int target = (int)Math.Min(amount, available);
                    if (target <= 0)
                    {
                        message = "Enter an amount greater than zero.";
                        return;
                    }

                    // Package first: if packaging fails, nothing has left the Core yet.
                    List<ItemDrop.ItemData> packed = CopiesFromCore(coreStacks, target);
                    string encoded = SerializeItems(packed);
                    if (encoded == null)
                    {
                        message = "These items could not be packaged for transfer. No items were moved.";
                        return;
                    }

                    RemoveFromCore(coreInventory, coreStacks, target);
                    responsePayload = encoded;
                    ok = true;
                    message = $"Sent {target} item(s) from the Core.";
                    return;
                }

                List<ItemDrop.ItemData> copies = DeserializeItems(payload);
                if (copies == null || copies.Count == 0)
                {
                    message = "The deposit could not be read. No items were moved.";
                    return;
                }

                if (kind != RuneStorageTransferKind.DepositAll
                    && copies.Any(copy => !string.Equals(RuneStorageItemGroups.GetIdentity(copy), identity, StringComparison.Ordinal)))
                {
                    message = "The deposited items did not match the requested item. No items were moved.";
                    return;
                }

                int[] accepted = CommitDepositToCore(coreInventory, copies, kind != RuneStorageTransferKind.DepositExact, out string shortfall);
                long total = accepted.Sum();
                ok = total > 0;
                responsePayload = EncodeAccepted(accepted);
                message = ok ? DepositMessage(accepted, null, shortfall) : (shortfall ?? "The Core could not accept these items. No items were moved.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage remote transfer failed: {exception}");
                message = "The transfer stopped after an inventory error. Check the Core before retrying.";
            }
            finally
            {
                RuneStorageTransactions.EndCoreOperation(operationId);
            }
        }

        // Requester side: applies the Core owner's answer to this player's inventory.
        internal static string Complete(RuneStorageCoreMarker marker, string requestId, bool ok, string message, string payload)
        {
            if (string.IsNullOrEmpty(requestId) || !Pending.TryGetValue(requestId, out PendingTransfer pending) || pending.Core != marker)
            {
                return "That transfer result was no longer expected and was ignored.";
            }

            Pending.Remove(requestId);
            if (pending.Player == null)
            {
                return "Your character was not available for the transfer result. Check both inventories.";
            }

            Inventory playerInventory = pending.Player.GetInventory();
            if (playerInventory == null)
            {
                return "Your inventory was not available for the transfer result. Check both inventories.";
            }

            try
            {
                if (pending.Kind == RuneStorageTransferKind.Withdraw)
                {
                    if (!ok) return message;

                    List<ItemDrop.ItemData> copies = DeserializeItems(payload);
                    if (copies == null)
                    {
                        return "The withdrawn items could not be read. Check the Core and your inventory.";
                    }

                    Vector3 dropAt = pending.Player.transform.position + pending.Player.transform.forward + Vector3.up * 0.5f;
                    int leftover = PlaceCopies(playerInventory, copies, dropAt);
                    return leftover > 0 ? message + " Items that did not fit were dropped at your feet." : message;
                }

                if (!ok) return message;

                int[] accepted = DecodeAccepted(payload, pending.Sources.Count);
                RemoveAcceptedFromSources(playerInventory, pending.Sources, accepted);
                return message;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage transfer result failed: {exception}");
                return "The transfer result stopped after an inventory error. Check both inventories before retrying.";
            }
        }

        private static List<SourceChunk> CollectSources(
            Inventory playerInventory,
            RuneStorageTransferKind kind,
            string identity,
            int amount,
            out string message)
        {
            message = null;
            bool skippedEquipped = false;
            var matching = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in playerInventory.GetAllItems())
            {
                if (item == null || item.m_shared == null || item.m_stack <= 0) continue;
                if (item.m_equipped)
                {
                    skippedEquipped = true;
                    continue;
                }

                if (kind != RuneStorageTransferKind.DepositAll
                    && !string.Equals(RuneStorageItemGroups.GetIdentity(item), identity, StringComparison.Ordinal)) continue;
                matching.Add(item);
            }

            long available = matching.Sum(item => (long)item.m_stack);
            if (available == 0)
            {
                message = kind == RuneStorageTransferKind.DepositAll
                    ? "No unequipped items to deposit."
                    : "That item changed or is no longer in your inventory. The list has been refreshed.";
                return null;
            }

            long target = kind == RuneStorageTransferKind.DepositExact ? Math.Min(available, amount) : available;
            int remaining = (int)target;
            var sources = new List<SourceChunk>();
            foreach (ItemDrop.ItemData item in matching)
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, item.m_stack);
                sources.Add(new SourceChunk { Original = item, Amount = take });
                remaining -= take;
            }

            var notes = new List<string>();
            if (kind == RuneStorageTransferKind.DepositExact && available < amount) notes.Add($"Only {available} were in your inventory.");
            if (skippedEquipped) notes.Add("Equipped items stay with you.");
            message = notes.Count > 0 ? string.Join(" ", notes) : null;
            return sources;
        }

        // Commits the copies that fit to the Core inventory. Returns the accepted amount per copy.
        private static int[] CommitDepositToCore(Inventory coreInventory, IReadOnlyList<ItemDrop.ItemData> copies, bool allowPartial, out string shortfall)
        {
            shortfall = null;
            var accepted = new int[copies.Count];
            var chunks = new StorageChunk[copies.Count];
            for (int index = 0; index < copies.Count; index++)
            {
                ItemDrop.ItemData copy = copies[index];
                chunks[index] = new StorageChunk(index, RuneStorageItemGroups.GetIdentity(copy), copy.m_shared.m_maxStackSize, copy.m_stack);
            }

            StorageTransferPlan plan = StorageTransferPlanner.Plan(
                coreInventory.GetWidth(),
                coreInventory.GetHeight(),
                CellsOf(coreInventory),
                chunks);

            if (!allowPartial && !plan.IsComplete)
            {
                shortfall = $"The Core has room for only {plan.TotalPlanned} of {plan.TotalRequested}. No items were moved.";
                return accepted;
            }

            foreach (StoragePlacement placement in plan.Placements)
            {
                ItemDrop.ItemData copy = copies[placement.ChunkIndex];
                if (placement.MergesIntoExisting)
                {
                    ItemDrop.ItemData existing = coreInventory.GetItemAt(placement.X, placement.Y);
                    if (existing == null || existing.m_shared == null
                        || existing.m_shared.m_maxStackSize - existing.m_stack < placement.Amount)
                    {
                        shortfall = "The Core changed while depositing; stopped with a partial deposit.";
                        break;
                    }

                    existing.m_stack += placement.Amount;
                }
                else
                {
                    ItemDrop.ItemData placed = copy.Clone();
                    placed.m_stack = placement.Amount;
                    if (!coreInventory.AddItem(placed, new Vector2i(placement.X, placement.Y)))
                    {
                        shortfall = "The Core rejected an item position; stopped with a partial deposit.";
                        break;
                    }
                }

                accepted[placement.ChunkIndex] += placement.Amount;
            }

            if (accepted.Any(amount => amount > 0)) NotifyChanged(coreInventory);
            return accepted;
        }

        // Places copies into an inventory using the planner. Returns the number of units that did not fit; those
        // are dropped at dropAt when a position is given.
        internal static int PlaceCopies(Inventory inventory, IReadOnlyList<ItemDrop.ItemData> copies, Vector3? dropAt)
        {
            var chunks = new StorageChunk[copies.Count];
            for (int index = 0; index < copies.Count; index++)
            {
                chunks[index] = new StorageChunk(index, RuneStorageItemGroups.GetIdentity(copies[index]), copies[index].m_shared.m_maxStackSize, copies[index].m_stack);
            }

            StorageTransferPlan plan = StorageTransferPlanner.Plan(inventory.GetWidth(), inventory.GetHeight(), CellsOf(inventory), chunks);
            var placedByChunk = new int[copies.Count];

            foreach (StoragePlacement placement in plan.Placements)
            {
                ItemDrop.ItemData copy = copies[placement.ChunkIndex];
                if (placement.MergesIntoExisting)
                {
                    ItemDrop.ItemData existing = inventory.GetItemAt(placement.X, placement.Y);
                    if (existing == null || existing.m_shared == null || existing.m_shared.m_maxStackSize - existing.m_stack < placement.Amount) break;
                    existing.m_stack += placement.Amount;
                }
                else
                {
                    ItemDrop.ItemData placed = copy.Clone();
                    placed.m_stack = placement.Amount;
                    if (!inventory.AddItem(placed, new Vector2i(placement.X, placement.Y))) break;
                }

                placedByChunk[placement.ChunkIndex] += placement.Amount;
            }

            NotifyChanged(inventory);

            int leftover = 0;
            for (int index = 0; index < copies.Count; index++)
            {
                int remainder = copies[index].m_stack - placedByChunk[index];
                if (remainder <= 0) continue;

                leftover += remainder;
                if (dropAt.HasValue)
                {
                    ItemDrop.ItemData dropped = copies[index].Clone();
                    dropped.m_stack = remainder;
                    ItemDrop.DropItem(dropped, remainder, dropAt.Value, Quaternion.identity);
                }
            }

            return leftover;
        }

        // Non-mutating: copies of the first target units across the given Core stacks.
        private static List<ItemDrop.ItemData> CopiesFromCore(List<ItemDrop.ItemData> coreStacks, int target)
        {
            var copies = new List<ItemDrop.ItemData>();
            int remaining = target;
            foreach (ItemDrop.ItemData item in coreStacks)
            {
                if (remaining <= 0) break;
                if (item.m_stack <= 0) continue;

                int take = Math.Min(remaining, item.m_stack);
                ItemDrop.ItemData copy = item.Clone();
                copy.m_stack = take;
                copies.Add(copy);
                remaining -= take;
            }

            return copies;
        }

        // Mutating: removes the same units that CopiesFromCore returns.
        private static void RemoveFromCore(Inventory coreInventory, List<ItemDrop.ItemData> coreStacks, int target)
        {
            int remaining = target;
            foreach (ItemDrop.ItemData item in coreStacks)
            {
                if (remaining <= 0) break;
                if (item.m_stack <= 0) continue;

                int take = Math.Min(remaining, item.m_stack);
                item.m_stack -= take;
                if (item.m_stack <= 0 && !coreInventory.RemoveItem(item))
                {
                    Plugin.Log.LogError("RuneStorage could not remove an emptied Core stack; it is left at zero quantity.");
                }

                remaining -= take;
            }

            NotifyChanged(coreInventory);
        }

        private static void RemoveAcceptedFromSources(Inventory playerInventory, List<SourceChunk> sources, int[] accepted)
        {
            for (int index = 0; index < sources.Count && index < accepted.Length; index++)
            {
                int amount = accepted[index];
                if (amount <= 0) continue;

                ItemDrop.ItemData item = sources[index].Original;
                if (!playerInventory.GetAllItems().Any(candidate => ReferenceEquals(candidate, item)))
                {
                    Plugin.Log.LogError($"RuneStorage could not find {amount} accepted item(s) in the player's inventory; check both inventories.");
                    continue;
                }

                int take = Math.Min(amount, item.m_stack);
                if (take >= item.m_stack)
                {
                    playerInventory.RemoveItem(item);
                }
                else
                {
                    item.m_stack -= take;
                }
            }

            NotifyChanged(playerInventory);
        }

        private static List<ItemDrop.ItemData> CoreStacks(Inventory coreInventory, string identity)
        {
            return coreInventory.GetAllItems()
                .Where(item => item != null
                    && item.m_shared != null
                    && item.m_stack > 0
                    && string.Equals(RuneStorageItemGroups.GetIdentity(item), identity, StringComparison.Ordinal))
                .OrderBy(item => item.m_gridPos.y)
                .ThenBy(item => item.m_gridPos.x)
                .ToList();
        }

        private static List<StorageCell> CellsOf(Inventory inventory)
        {
            return inventory.GetAllItems()
                .Where(item => item != null && item.m_shared != null && item.m_stack > 0)
                .Select(item => new StorageCell(
                    item.m_gridPos.x,
                    item.m_gridPos.y,
                    RuneStorageItemGroups.GetIdentity(item) ?? string.Empty,
                    item.m_stack,
                    item.m_shared.m_maxStackSize))
                .ToList();
        }

        private static ItemDrop.ItemData CopyWithAmount(ItemDrop.ItemData original, int amount)
        {
            ItemDrop.ItemData copy = original.Clone();
            copy.m_stack = amount;
            return copy;
        }

        private static string DepositMessage(int[] accepted, string note, string shortfall)
        {
            int total = accepted.Sum();
            var parts = new List<string>();
            parts.Add(shortfall != null && total == 0 ? shortfall : $"Deposited {total} item(s).");
            if (shortfall != null && total > 0) parts.Add(shortfall);
            if (!string.IsNullOrEmpty(note)) parts.Add(note);
            return string.Join(" ", parts);
        }

        internal static void NotifyChanged(Inventory inventory)
        {
            InventoryChanged?.Invoke(inventory, new object[] { true, false });
        }

        // Packages item copies with Valheim's own Inventory save format so every item field survives transport.
        internal static string SerializeItems(IReadOnlyList<ItemDrop.ItemData> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            if (items.Count > MaxItemsPerPayload) return null;

            var staging = new Inventory("RuneStorageTransfer", null, 64, (items.Count + 63) / 64);
            for (int index = 0; index < items.Count; index++)
            {
                if (!staging.AddItem(items[index], new Vector2i(index % 64, index / 64))) return null;
            }

            var package = new ZPackage();
            staging.Save(package);
            string encoded = Convert.ToBase64String(package.GetArray());
            return encoded.Length <= MaxPayloadCharacters ? encoded : null;
        }

        internal static List<ItemDrop.ItemData> DeserializeItems(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            if (encoded.Length > MaxPayloadCharacters) return null;

            try
            {
                var package = new ZPackage(Convert.FromBase64String(encoded));
                var staging = new Inventory("RuneStorageTransfer", null, 64, 64);
                staging.Load(package);
                List<ItemDrop.ItemData> items = staging.GetAllItems().Where(item => item != null && item.m_shared != null && item.m_stack > 0).ToList();
                return items.Count > MaxItemsPerPayload ? null : items;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage could not read a transfer payload: {exception.GetBaseException()}");
                return null;
            }
        }

        private static string EncodeAccepted(int[] accepted)
        {
            return string.Join(";", accepted.Select((amount, index) => index.ToString(CultureInfo.InvariantCulture) + ":" + amount.ToString(CultureInfo.InvariantCulture)));
        }

        private static int[] DecodeAccepted(string encoded, int count)
        {
            var accepted = new int[count];
            if (string.IsNullOrEmpty(encoded)) return accepted;

            foreach (string pair in encoded.Split(';'))
            {
                string[] parts = pair.Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)) continue;
                if (index >= 0 && index < count && amount > 0) accepted[index] = amount;
            }

            return accepted;
        }
    }
}
