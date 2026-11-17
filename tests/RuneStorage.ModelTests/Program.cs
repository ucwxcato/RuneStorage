using System;
using System.Collections.Generic;
using System.Linq;
using RuneStorage;
using RuneStorage.Core;

internal static class Program
{
    private static int Main()
    {
        try
        {
            TierCapacitiesMapToExpectedDimensions();
            UnsupportedTiersAreRejected();
            Console.WriteLine("RuneStorage Core tier-capacity tests passed.");

            EmptyGridFillsRowMajorWithStackLimit();
            CompatibleStacksMergeBeforeNewCells();
            DifferentIdentitiesNeverMerge();
            NonStackableItemsNeedOwnCells();
            ShortfallIsReportedAndNotPlaced();
            SecondChunkSeesFirstChunkPlacements();
            Console.WriteLine("RuneStorage transfer-planner tests passed.");

            UpgradeCostsMatchApprovedValues();
            UpgradesOnlyAdvanceOneTierAtATime();
            Console.WriteLine("RuneStorage tier-upgrade tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void TierCapacitiesMapToExpectedDimensions()
    {
        AssertTier(1, 16, 16, 256);
        AssertTier(2, 16, 32, 512);
        AssertTier(3, 16, 64, 1024);
    }

    private static void UnsupportedTiersAreRejected()
    {
        if (CoreTierCapacity.TryGetDimensions(0, out int width, out int height))
        {
            throw new InvalidOperationException("Tier 0 must not have dimensions.");
        }

        Equal(0, width);
        Equal(0, height);

        if (CoreTierCapacity.TryGetSlotCount(4, out int slots))
        {
            throw new InvalidOperationException("Tier 4 must not have a slot count.");
        }

        Equal(0, slots);
    }

    private static void AssertTier(int tier, int expectedWidth, int expectedHeight, int expectedSlots)
    {
        if (!CoreTierCapacity.TryGetDimensions(tier, out int width, out int height))
        {
            throw new InvalidOperationException("Tier " + tier + " was rejected.");
        }

        Equal(expectedWidth, width);
        Equal(expectedHeight, height);

        if (!CoreTierCapacity.TryGetSlotCount(tier, out int slots))
        {
            throw new InvalidOperationException("Tier " + tier + " slot count was rejected.");
        }

        Equal(expectedSlots, slots);
    }

    private static void EmptyGridFillsRowMajorWithStackLimit()
    {
        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            3, 2, Array.Empty<StorageCell>(), new[] { new StorageChunk(0, "stone", 10, 25) });

        Equal(25, plan.TotalPlanned);
        Equal(true, plan.IsComplete);
        Equal(3, plan.Placements.Count);
        Equal(0, plan.Placements[0].X); Equal(0, plan.Placements[0].Y); Equal(10, plan.Placements[0].Amount);
        Equal(1, plan.Placements[1].X); Equal(0, plan.Placements[1].Y); Equal(10, plan.Placements[1].Amount);
        Equal(2, plan.Placements[2].X); Equal(0, plan.Placements[2].Y); Equal(5, plan.Placements[2].Amount);
    }

    private static void CompatibleStacksMergeBeforeNewCells()
    {
        var occupied = new[] { new StorageCell(2, 1, "wood", 7, 10) };
        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            3, 2, occupied, new[] { new StorageChunk(0, "wood", 10, 8) });

        Equal(true, plan.IsComplete);
        Equal(2, plan.Placements.Count);
        Equal(true, plan.Placements[0].MergesIntoExisting);
        Equal(2, plan.Placements[0].X); Equal(1, plan.Placements[0].Y); Equal(3, plan.Placements[0].Amount);
        Equal(false, plan.Placements[1].MergesIntoExisting);
        Equal(0, plan.Placements[1].X); Equal(0, plan.Placements[1].Y); Equal(5, plan.Placements[1].Amount);
    }

    private static void DifferentIdentitiesNeverMerge()
    {
        var occupied = new[] { new StorageCell(0, 0, "wood-quality-1", 3, 10) };
        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            2, 1, occupied, new[] { new StorageChunk(0, "wood-quality-2", 10, 4) });

        Equal(true, plan.IsComplete);
        Equal(1, plan.Placements.Count);
        Equal(false, plan.Placements[0].MergesIntoExisting);
        Equal(1, plan.Placements[0].X);
    }

    private static void NonStackableItemsNeedOwnCells()
    {
        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            2, 1, Array.Empty<StorageCell>(), new[] { new StorageChunk(0, "sword", 1, 3) });

        Equal(2, plan.TotalPlanned);
        Equal(false, plan.IsComplete);
    }

    private static void ShortfallIsReportedAndNotPlaced()
    {
        var occupied = new[]
        {
            new StorageCell(0, 0, "stone", 10, 10),
            new StorageCell(1, 0, "other", 1, 1)
        };

        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            2, 1, occupied, new[] { new StorageChunk(0, "stone", 10, 5) });

        Equal(0, plan.TotalPlanned);
        Equal(5, plan.TotalRequested);
        Equal(false, plan.IsComplete);
        Equal(0, plan.Placements.Count);
    }

    private static void SecondChunkSeesFirstChunkPlacements()
    {
        StorageTransferPlan plan = StorageTransferPlanner.Plan(
            2, 1,
            Array.Empty<StorageCell>(),
            new[] { new StorageChunk(0, "ore", 10, 4), new StorageChunk(1, "ore", 10, 4) });

        Equal(true, plan.IsComplete);
        Equal(2, plan.Placements.Count);
        Equal(0, plan.Placements[0].X);
        Equal(8, plan.Placements.Sum(p => p.Amount));
        Equal(true, plan.Placements[1].MergesIntoExisting);
        Equal(0, plan.Placements[1].X);
    }

    private static void UpgradeCostsMatchApprovedValues()
    {
        Equal(true, CoreTierUpgrades.TryGetUpgrade(2, out string station2, out TierUpgradeRequirement[] tier2));
        Equal("Forge", station2);
        Equal(3, tier2.Length);
        Equal("Iron", tier2[0].ItemName); Equal(10, tier2[0].Amount);
        Equal("FineWood", tier2[1].ItemName); Equal(10, tier2[1].Amount);
        Equal("SurtlingCore", tier2[2].ItemName); Equal(4, tier2[2].Amount);

        Equal(true, CoreTierUpgrades.TryGetUpgrade(3, out string station3, out TierUpgradeRequirement[] tier3));
        Equal("BlackForge", station3);
        Equal(3, tier3.Length);
        Equal("BlackMetal", tier3[0].ItemName); Equal(10, tier3[0].Amount);
        Equal("FineWood", tier3[1].ItemName); Equal(10, tier3[1].Amount);
        Equal("SurtlingCore", tier3[2].ItemName); Equal(6, tier3[2].Amount);

        Equal(false, CoreTierUpgrades.TryGetUpgrade(1, out _, out _));
        Equal(false, CoreTierUpgrades.TryGetUpgrade(4, out _, out _));
    }

    private static void UpgradesOnlyAdvanceOneTierAtATime()
    {
        Equal(true, CoreTierUpgrades.IsNextTier(1, 2));
        Equal(true, CoreTierUpgrades.IsNextTier(2, 3));
        Equal(false, CoreTierUpgrades.IsNextTier(1, 3));
        Equal(false, CoreTierUpgrades.IsNextTier(3, 4));
        Equal(false, CoreTierUpgrades.IsNextTier(2, 2));
        Equal(false, CoreTierUpgrades.IsNextTier(0, 1));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException("Expected " + expected + ", got " + actual + ".");
        }
    }
}
