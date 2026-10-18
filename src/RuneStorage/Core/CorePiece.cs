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
    internal static class CorePiece
    {
        private const string PieceName = "piece_runestorage_core";
        private const string VisualPrefabName = "RuneStorage_Core_Visual";
        private const string IconAssetPath = "Assets/RuneStorage/Core/Icons/RuneStorage_Core_Icon.png";
        private const string BundleResourceSuffix = ".Assets.AssetBundles.runestorage_core";

        private static bool _registered;
        private static AssetBundle _visualBundle;

        public static void Register()
        {
            if (_registered) return;

            string basePrefabName = FindVerifiedBuildPiecePrefabName();
            if (basePrefabName == null) return;
            if (!LoadBundle()) return;

            GameObject visualSource = _visualBundle.LoadAsset<GameObject>(VisualPrefabName);
            Sprite icon = _visualBundle.LoadAsset<Sprite>(IconAssetPath);
            if (visualSource == null)
            {
                Plugin.Log.LogError($"RuneStorage Core visual '{VisualPrefabName}' was not found in the embedded AssetBundle.");
                return;
            }

            if (icon == null)
            {
                Plugin.Log.LogError($"RuneStorage Core icon '{IconAssetPath}' was not found in the embedded AssetBundle.");
                return;
            }

            var core = new CustomPiece(PieceName, basePrefabName, new PieceConfig
            {
                PieceTable = "Hammer",
                Category = "RuneStorage",
                Name = "RuneStorage Core",
                Description = "The storage core for a nearby RuneStorage Terminal.",
                Icon = icon,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "SwordCheat", Amount = 1 }
                }
            });

            AttachVisual(core.PiecePrefab, visualSource);

            foreach (Container container in core.PiecePrefab.GetComponentsInChildren<Container>(true))
            {
                UnityEngine.Object.Destroy(container);
            }

            Piece piece = core.PiecePrefab.GetComponent<Piece>();
            if (piece == null)
            {
                Plugin.Log.LogError($"RuneStorage Core clone '{PieceName}' has no Piece component after named cloning.");
                return;
            }

            piece.m_canBeRemoved = true;
            PieceManager.Instance.AddPiece(core);
            _registered = true;

            Plugin.Log.LogInfo(
                $"Registered {PieceName} with verified base prefab '{basePrefabName}', visual '{VisualPrefabName}', " +
                "Core icon, and no inherited chest container.");
        }

        private static string FindVerifiedBuildPiecePrefabName()
        {
            const string candidate = "piece_chest_wood";
            GameObject prefab = PrefabManager.Cache.GetPrefab<GameObject>(candidate);
            if (prefab == null)
            {
                Plugin.Log.LogError($"No verified RuneStorage Core build-piece base was found in PrefabManager.Cache. Probed: {candidate}");
                return null;
            }

            if (prefab.GetComponent<Piece>() == null)
            {
                Plugin.Log.LogError($"PrefabManager.Cache contains '{candidate}', but it has no Piece component; refusing to register the Core.");
                return null;
            }

            Plugin.Log.LogInfo($"Verified RuneStorage Core base prefab in PrefabManager.Cache: '{candidate}'.");
            return candidate;
        }

        private static bool LoadBundle()
        {
            if (_visualBundle != null) return true;

            Assembly assembly = typeof(CorePiece).Assembly;
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(BundleResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                Plugin.Log.LogError(
                    $"Embedded Core AssetBundle ending with '{BundleResourceSuffix}' was not found. " +
                    $"Resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
                return false;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (var buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    Plugin.Log.LogError($"Embedded Core AssetBundle resource '{resourceName}' returned a null stream.");
                    return false;
                }

                stream.CopyTo(buffer);
                _visualBundle = AssetBundle.LoadFromMemory(buffer.ToArray());
            }

            if (_visualBundle == null)
            {
                Plugin.Log.LogError("AssetBundle.LoadFromMemory returned null for the RuneStorage Core bundle.");
                return false;
            }

            Plugin.Log.LogInfo($"Loaded RuneStorage Core AssetBundle resource '{resourceName}'.");
            return true;
        }

        private static void AttachVisual(GameObject functionalPrefab, GameObject visualSource)
        {
            GameObject visual = UnityEngine.Object.Instantiate(visualSource);
            visual.name = VisualPrefabName;
            visual.SetActive(true);
            visual.transform.SetParent(functionalPrefab.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (Renderer renderer in functionalPrefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform == visual.transform || renderer.transform.IsChildOf(visual.transform)) continue;
                renderer.enabled = false;
            }
        }
    }
}
