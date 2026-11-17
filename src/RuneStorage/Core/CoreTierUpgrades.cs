namespace RuneStorage.Core
{
    // Approved in-place upgrade costs (owner decision, 2026-11-17). Paid from the Core's stored items, the same way
    // crafting uses Core ingredients. Item prefab names match the names the Core and Terminal recipes already use;
    // Iron and BlackMetal are confirmed against ObjectDB at runtime before any upgrade is accepted.
    internal readonly struct TierUpgradeRequirement
    {
        internal TierUpgradeRequirement(string itemName, int amount)
        {
            ItemName = itemName;
            Amount = amount;
        }

        internal string ItemName { get; }
        internal int Amount { get; }
    }

    internal static class CoreTierUpgrades
    {
        internal const int MaxTier = 3;

        internal static bool IsNextTier(int currentTier, int targetTier)
        {
            return currentTier >= 1 && targetTier == currentTier + 1 && targetTier <= MaxTier;
        }

        internal static bool TryGetUpgrade(int targetTier, out string stationName, out TierUpgradeRequirement[] requirements)
        {
            switch (targetTier)
            {
                case 2:
                    stationName = "Forge";
                    requirements = new[]
                    {
                        new TierUpgradeRequirement("Iron", 10),
                        new TierUpgradeRequirement("FineWood", 10),
                        new TierUpgradeRequirement("SurtlingCore", 4)
                    };
                    return true;
                case 3:
                    stationName = "BlackForge";
                    requirements = new[]
                    {
                        new TierUpgradeRequirement("BlackMetal", 10),
                        new TierUpgradeRequirement("FineWood", 10),
                        new TierUpgradeRequirement("SurtlingCore", 6)
                    };
                    return true;
                default:
                    stationName = null;
                    requirements = null;
                    return false;
            }
        }

        internal static string RomanNumeral(int tier)
        {
            switch (tier)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return tier.ToString();
            }
        }
    }
}
