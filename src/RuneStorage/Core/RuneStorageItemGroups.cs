using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RuneStorage
{
    internal sealed class RuneStorageItemGroup
    {
        internal string Identity { get; set; }
        internal string ItemId { get; set; }
        internal string DisplayName { get; set; }
        internal Sprite Icon { get; set; }
        internal long Count { get; set; }
        internal List<ItemDrop.ItemData> Items { get; } = new List<ItemDrop.ItemData>();
    }

    internal static class RuneStorageItemGroups
    {
        internal static List<RuneStorageItemGroup> Build(Inventory inventory, string search, bool excludeEquipped = false)
        {
            var groups = new Dictionary<string, RuneStorageItemGroup>(StringComparer.Ordinal);
            string normalizedSearch = (search ?? string.Empty).Trim();

            if (inventory == null) return new List<RuneStorageItemGroup>();

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (excludeEquipped && item != null && item.m_equipped) continue;

                string identity = GetIdentity(item);
                if (identity == null) continue;

                if (!groups.TryGetValue(identity, out RuneStorageItemGroup group))
                {
                    group = new RuneStorageItemGroup
                    {
                        Identity = identity,
                        ItemId = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name,
                        DisplayName = GetDisplayName(item),
                        Icon = item.GetIcon()
                    };
                    groups.Add(identity, group);
                }

                group.Count += Math.Max(0, item.m_stack);
                group.Items.Add(item);
            }

            return groups.Values
                .Where(group => normalizedSearch.Length == 0
                    || group.DisplayName.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                    || group.ItemId.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Identity, StringComparer.Ordinal)
                .ToList();
        }

        internal static string GetIdentity(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;

            var builder = new StringBuilder(128);
            Append(builder, item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name);
            Append(builder, item.m_shared.m_name);
            Append(builder, item.m_quality.ToString(CultureInfo.InvariantCulture));
            Append(builder, item.m_variant.ToString(CultureInfo.InvariantCulture));
            Append(builder, item.m_worldLevel.ToString(CultureInfo.InvariantCulture));
            Append(builder, item.m_durability.ToString("R", CultureInfo.InvariantCulture));
            Append(builder, item.m_crafterID.ToString(CultureInfo.InvariantCulture));
            Append(builder, item.m_crafterName ?? string.Empty);
            Append(builder, item.m_equipped ? "1" : "0");
            Append(builder, item.m_cheated ? "1" : "0");

            if (item.m_customData != null)
            {
                foreach (KeyValuePair<string, string> value in item.m_customData.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    Append(builder, value.Key ?? string.Empty);
                    Append(builder, value.Value ?? string.Empty);
                }
            }

            return builder.ToString();
        }

        private static string GetDisplayName(ItemDrop.ItemData item)
        {
            string rawName = item.m_shared != null ? item.m_shared.m_name : null;
            if (string.IsNullOrEmpty(rawName))
            {
                return item.m_dropPrefab != null ? item.m_dropPrefab.name : "Unknown item";
            }

            try
            {
                if (Localization.instance != null)
                {
                    string localized = Localization.instance.Localize(rawName);
                    if (!string.IsNullOrEmpty(localized)) return localized;
                }
            }
            catch (Exception)
            {
                // Keep the source name visible if localization is not ready yet.
            }

            return rawName;
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }
    }
}
