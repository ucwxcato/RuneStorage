using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace RuneStorage
{
    internal static class TerminalPiece
    {
        private const string PieceName = "piece_runestorage_terminal";
        private const string VisualAssetName = "Terminal2_Visual";
        private const string VisualInstanceName = "RuneStorage_Terminal_Visual";
        private const string IconAssetPath = "Assets/RuneStorage/Terminal/Icons/RuneStorage_Terminal_Icon.png";
        private const string BundleResourceSuffix = ".Assets.AssetBundles.runestorage_terminal";

        private static bool _registered;
        private static AssetBundle _visualBundle;

        public static void Register()
        {
            if (_registered) return;

            string basePrefabName = FindVerifiedBuildPiecePrefabName();
            if (basePrefabName == null) return;
            if (!LoadBundle()) return;

            GameObject visualSource = _visualBundle.LoadAsset<GameObject>(VisualAssetName);
            Sprite icon = _visualBundle.LoadAsset<Sprite>(IconAssetPath);
            if (visualSource == null)
            {
                Plugin.Log.LogError($"RuneStorage terminal visual '{VisualAssetName}' was not found in the embedded AssetBundle.");
                return;
            }

            if (icon == null)
            {
                Plugin.Log.LogError($"RuneStorage terminal icon '{IconAssetPath}' was not found in the embedded AssetBundle.");
                return;
            }

            var terminal = new CustomPiece(PieceName, basePrefabName, new PieceConfig
            {
                PieceTable = "Hammer",
                Category = "RuneStorage",
                Name = "RuneStorage Terminal",
                Description = "A runestone terminal for accessing a nearby RuneStorage Core.",
                Icon = icon,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "FineWood", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "Stone", Amount = 10, Recover = true },
                    new RequirementConfig { Item = "SurtlingCore", Amount = 2, Recover = true }
                }
            });

            AttachVisual(terminal.PiecePrefab, visualSource);

            Piece piece = terminal.PiecePrefab.GetComponent<Piece>();
            if (piece == null)
            {
                Plugin.Log.LogError($"RuneStorage terminal clone '{PieceName}' has no Piece component after named cloning.");
                return;
            }

            if (terminal.PiecePrefab.GetComponent<RuneStorageTerminalMarker>() == null)
            {
                terminal.PiecePrefab.AddComponent<RuneStorageTerminalMarker>();
            }

            piece.m_canBeRemoved = true;
            PieceManager.Instance.AddPiece(terminal);
            _registered = true;

            Plugin.Log.LogInfo(
                $"Registered {PieceName} with verified base prefab '{basePrefabName}', visual '{VisualAssetName}', terminal icon, " +
                "and Hammer costs 10 Fine Wood, 10 Stone, and 2 Surtling Cores.");
        }

        private static string FindVerifiedBuildPiecePrefabName()
        {
            string[] candidates =
            {
                // This is the verified vanilla build-piece name used by the existing
                // HouseholdChest registration in the same Valheim setup.
                "piece_chest_wood"
            };

            foreach (string candidate in candidates)
            {
                GameObject prefab = PrefabManager.Cache.GetPrefab<GameObject>(candidate);
                if (prefab == null) continue;
                if (prefab.GetComponent<Piece>() == null)
                {
                    Plugin.Log.LogWarning($"PrefabManager.Cache contains '{candidate}', but it has no Piece component; skipping it.");
                    continue;
                }

                Plugin.Log.LogInfo($"Verified RuneStorage terminal base prefab in PrefabManager.Cache: '{candidate}'.");
                return candidate;
            }

            Plugin.Log.LogError($"No verified RuneStorage terminal build-piece base was found in PrefabManager.Cache. Probed: {string.Join(", ", candidates)}");
            return null;
        }

        private static void AttachVisual(GameObject functionalPrefab, GameObject visualSource)
        {
            GameObject visual = UnityEngine.Object.Instantiate(visualSource);
            visual.name = VisualInstanceName;
            visual.SetActive(true);
            // Keep the prefab's authored scale and facing; TerminalVisualAnchor only recenters its position.
            visual.transform.SetParent(functionalPrefab.transform, false);

            TerminalVisualAnchor anchor = functionalPrefab.GetComponent<TerminalVisualAnchor>();
            if (anchor == null) anchor = functionalPrefab.AddComponent<TerminalVisualAnchor>();
            anchor.Initialize(visual.transform);

            foreach (Renderer renderer in functionalPrefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform == visual.transform || renderer.transform.IsChildOf(visual.transform)) continue;
                renderer.enabled = false;
            }
        }

        private static bool LoadBundle()
        {
            if (_visualBundle != null) return true;

            Assembly assembly = typeof(TerminalPiece).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(BundleResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                Plugin.Log.LogError($"Embedded terminal AssetBundle ending with '{BundleResourceSuffix}' was not found. Resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
                return false;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (var buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    Plugin.Log.LogError($"Embedded terminal AssetBundle resource '{resourceName}' returned a null stream.");
                    return false;
                }

                stream.CopyTo(buffer);
                _visualBundle = AssetBundle.LoadFromMemory(buffer.ToArray());
            }

            if (_visualBundle == null)
            {
                Plugin.Log.LogError("AssetBundle.LoadFromMemory returned null for the RuneStorage terminal bundle.");
                return false;
            }

            Plugin.Log.LogInfo($"Loaded RuneStorage terminal AssetBundle resource '{resourceName}'.");
            return true;
        }
    }
}
