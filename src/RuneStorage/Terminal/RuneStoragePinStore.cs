using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;

namespace RuneStorage
{
    // Client-side, per-character view preference. Pins never affect storage, transfers, or crafting, so the
    // server does not read this file. The file lives under the BepInEx config folder and is keyed by the
    // character's player ID, so pins are not shared between characters or worlds.
    //
    // File format: one "playerId<TAB>prefabName" pair per line. Lines that cannot be parsed are preserved
    // as-is on save and are never discarded automatically.
    internal static class RuneStoragePinStore
    {
        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "RuneStorage", "pins.txt");
        private static readonly Dictionary<long, HashSet<string>> Pins = new Dictionary<long, HashSet<string>>();
        private static readonly List<string> PreservedLines = new List<string>();
        private static bool _loaded;

        internal static bool IsPinned(long playerId, string prefabKey)
        {
            return !string.IsNullOrEmpty(prefabKey) && PinsFor(playerId).Contains(prefabKey);
        }

        internal static void SetPinned(long playerId, string prefabKey, bool pinned)
        {
            if (string.IsNullOrEmpty(prefabKey)) return;

            HashSet<string> set = PinsFor(playerId);
            bool changed = pinned ? set.Add(prefabKey) : set.Remove(prefabKey);
            if (changed) Save();
        }

        // Prefab name is the pin key, so every quality and durability of an item pins together (decision D1).
        internal static string PrefabKey(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return null;
            return item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
        }

        private static HashSet<string> PinsFor(long playerId)
        {
            EnsureLoaded();
            if (!Pins.TryGetValue(playerId, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                Pins[playerId] = set;
            }

            return set;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            if (!File.Exists(FilePath)) return;

            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length == 2
                        && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long playerId)
                        && !string.IsNullOrEmpty(parts[1]))
                    {
                        if (!Pins.TryGetValue(playerId, out HashSet<string> set))
                        {
                            set = new HashSet<string>(StringComparer.Ordinal);
                            Pins[playerId] = set;
                        }

                        set.Add(parts[1]);
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        PreservedLines.Add(line);
                        Plugin.Log.LogWarning("RuneStorage pins file has a line that could not be read; it is kept unchanged.");
                    }
                }
            }
            catch (IOException exception)
            {
                Plugin.Log.LogWarning($"RuneStorage could not read pins: {exception.Message}");
            }
        }

        private static void Save()
        {
            var lines = new List<string>(PreservedLines);
            foreach (KeyValuePair<long, HashSet<string>> entry in Pins)
            {
                foreach (string prefab in entry.Value)
                {
                    lines.Add(entry.Key.ToString(CultureInfo.InvariantCulture) + "\t" + prefab);
                }
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, lines);
            }
            catch (IOException exception)
            {
                Plugin.Log.LogWarning($"RuneStorage could not save pins: {exception.Message}");
            }
        }
    }
}
