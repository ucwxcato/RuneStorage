namespace RuneStorage.Core
{
    internal static class CoreTierCapacity
    {
        public const int InventoryWidth = 16;

        public static bool TryGetDimensions(int tier, out int width, out int height)
        {
            width = InventoryWidth;

            switch (tier)
            {
                case 1:
                    height = 16;
                    return true;
                case 2:
                    height = 32;
                    return true;
                case 3:
                    height = 64;
                    return true;
                default:
                    width = 0;
                    height = 0;
                    return false;
            }
        }

        public static bool TryGetSlotCount(int tier, out int slots)
        {
            if (!TryGetDimensions(tier, out int width, out int height))
            {
                slots = 0;
                return false;
            }

            slots = width * height;
            return true;
        }
    }
}
