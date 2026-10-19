using System;
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

    private static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException("Expected " + expected + ", got " + actual + ".");
        }
    }
}
