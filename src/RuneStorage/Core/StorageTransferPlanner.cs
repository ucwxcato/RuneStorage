using System;
using System.Collections.Generic;
using System.Linq;

namespace RuneStorage
{
    // Pure grid model used to decide whether a transfer fits before any item is moved.
    // Valheim inventory items occupy one grid cell each; stacks merge into compatible cells first,
    // then fill empty cells in row-major order. It has no Unity or Valheim dependencies so it can be
    // covered by the model tests.
    internal readonly struct StorageCell
    {
        internal StorageCell(int x, int y, string identity, int stack, int maxStack)
        {
            X = x;
            Y = y;
            Identity = identity;
            Stack = stack;
            MaxStack = maxStack;
        }

        internal int X { get; }
        internal int Y { get; }
        internal string Identity { get; }
        internal int Stack { get; }
        internal int MaxStack { get; }
    }

    internal readonly struct StorageChunk
    {
        internal StorageChunk(int index, string identity, int maxStack, int amount)
        {
            Index = index;
            Identity = identity;
            MaxStack = maxStack;
            Amount = amount;
        }

        internal int Index { get; }
        internal string Identity { get; }
        internal int MaxStack { get; }
        internal int Amount { get; }
    }

    internal readonly struct StoragePlacement
    {
        internal StoragePlacement(int chunkIndex, int x, int y, int amount, bool mergesIntoExisting)
        {
            ChunkIndex = chunkIndex;
            X = x;
            Y = y;
            Amount = amount;
            MergesIntoExisting = mergesIntoExisting;
        }

        internal int ChunkIndex { get; }
        internal int X { get; }
        internal int Y { get; }
        internal int Amount { get; }
        internal bool MergesIntoExisting { get; }
    }

    internal sealed class StorageTransferPlan
    {
        internal StorageTransferPlan(IReadOnlyList<StoragePlacement> placements, int[] requestedByChunk, int[] plannedByChunk)
        {
            Placements = placements;
            RequestedByChunk = requestedByChunk;
            PlannedByChunk = plannedByChunk;
        }

        internal IReadOnlyList<StoragePlacement> Placements { get; }
        internal int[] RequestedByChunk { get; }
        internal int[] PlannedByChunk { get; }
        internal int TotalRequested => RequestedByChunk.Sum();
        internal int TotalPlanned => PlannedByChunk.Sum();
        internal bool IsComplete => TotalPlanned == TotalRequested;
    }

    internal static class StorageTransferPlanner
    {
        private sealed class SimulatedCell
        {
            internal string Identity;
            internal int Stack;
            internal int MaxStack;
        }

        internal static StorageTransferPlan Plan(
            int width,
            int height,
            IEnumerable<StorageCell> occupiedCells,
            IReadOnlyList<StorageChunk> chunks)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Inventory grid must be positive.");
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            var cells = new Dictionary<int, SimulatedCell>();
            foreach (StorageCell cell in occupiedCells ?? Enumerable.Empty<StorageCell>())
            {
                if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height) continue;
                cells[Key(width, cell.X, cell.Y)] = new SimulatedCell
                {
                    Identity = cell.Identity,
                    Stack = cell.Stack,
                    MaxStack = cell.MaxStack
                };
            }

            var placements = new List<StoragePlacement>();
            var requested = new int[chunks.Count];
            var planned = new int[chunks.Count];

            for (int chunkPosition = 0; chunkPosition < chunks.Count; chunkPosition++)
            {
                StorageChunk chunk = chunks[chunkPosition];
                requested[chunkPosition] = Math.Max(0, chunk.Amount);
                if (chunk.Amount <= 0 || chunk.MaxStack <= 0 || string.IsNullOrEmpty(chunk.Identity)) continue;

                int remaining = chunk.Amount;

                // Merge into compatible stacks first, in row-major order.
                foreach (int key in cells.Keys.OrderBy(k => k / width).ThenBy(k => k % width).ToArray())
                {
                    if (remaining <= 0) break;
                    SimulatedCell cell = cells[key];
                    if (!string.Equals(cell.Identity, chunk.Identity, StringComparison.Ordinal)) continue;
                    int room = chunk.MaxStack - cell.Stack;
                    if (room <= 0) continue;

                    int add = Math.Min(room, remaining);
                    cell.Stack += add;
                    remaining -= add;
                    placements.Add(new StoragePlacement(chunk.Index, key % width, key / width, add, true));
                }

                // Then fill empty cells in row-major order.
                for (int y = 0; y < height && remaining > 0; y++)
                {
                    for (int x = 0; x < width && remaining > 0; x++)
                    {
                        int key = Key(width, x, y);
                        if (cells.ContainsKey(key)) continue;

                        int add = Math.Min(chunk.MaxStack, remaining);
                        cells[key] = new SimulatedCell { Identity = chunk.Identity, Stack = add, MaxStack = chunk.MaxStack };
                        remaining -= add;
                        placements.Add(new StoragePlacement(chunk.Index, x, y, add, false));
                    }
                }

                planned[chunkPosition] = chunk.Amount - remaining;
            }

            return new StorageTransferPlan(placements, requested, planned);
        }

        private static int Key(int width, int x, int y) => y * width + x;
    }
}
