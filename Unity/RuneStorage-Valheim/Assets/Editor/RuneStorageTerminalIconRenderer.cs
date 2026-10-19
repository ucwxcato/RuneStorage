using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace RuneStorage.EditorTools
{
    public static class RuneStorageTerminalIconRenderer
    {
        private const string PrefabPath = "Assets/RuneStorage/Terminal/Prefabs/Terminal2_Visual.prefab";
        private const string PreviousPrefabPath = "Assets/RuneStorage/Terminal/Prefabs/RuneStorage_Terminal_Visual.prefab";
        private const string IconPath = "Assets/RuneStorage/Terminal/Icons/RuneStorage_Terminal_Icon.png";
        private const string BundleName = "runestorage_terminal";
        private const int IconSize = 256;

        [MenuItem("RuneStorage/Render Terminal Icon")]
        public static void RenderTerminalIcon()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                ShowDialog("RuneStorage icon", $"Prefab not found:\n{PrefabPath}", true);
                if (Application.isBatchMode) throw new FileNotFoundException("RuneStorage terminal visual prefab was not found.", PrefabPath);
                return;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();

            GameObject previewRoot = null;
            Camera previewCamera = null;
            RenderTexture renderTexture = null;
            Texture2D iconTexture = null;
            RenderTexture previousActiveTexture = null;

            try
            {
                previewRoot = new GameObject("RuneStorage_Terminal_Icon_Preview");
                SceneManager.MoveGameObjectToScene(previewRoot, previewScene);

                var model = Object.Instantiate(prefab);
                model.name = "RuneStorage_Terminal_Visual";
                SceneManager.MoveGameObjectToScene(model, previewScene);
                model.transform.SetParent(previewRoot.transform, true);

                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    ShowDialog("RuneStorage icon", "The terminal prefab has no visible renderers.", true);
                    if (Application.isBatchMode) throw new System.InvalidOperationException("The terminal prefab has no visible renderers.");
                    return;
                }

                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                var target = bounds.center + Vector3.up * (bounds.extents.y * 0.05f);
                var cameraObject = new GameObject("RuneStorage_Terminal_Icon_Camera");
                cameraObject.transform.SetParent(previewRoot.transform, true);
                previewCamera = cameraObject.AddComponent<Camera>();
                previewCamera.orthographic = true;
                previewCamera.orthographicSize = Mathf.Max(bounds.extents.magnitude * 1.05f, 0.5f);
                previewCamera.transform.position = target + new Vector3(-4.5f, 3.2f, 6.5f);
                previewCamera.transform.LookAt(target);
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                previewCamera.nearClipPlane = 0.01f;
                previewCamera.farClipPlane = 1000f;
                previewCamera.allowHDR = false;
                previewCamera.allowMSAA = true;

                CreateLight(previewRoot.transform, "RuneStorage_Terminal_Icon_Key", new Vector3(35f, -35f, 0f), 1.25f, new Color(1f, 0.91f, 0.78f));
                CreateLight(previewRoot.transform, "RuneStorage_Terminal_Icon_Fill", new Vector3(55f, 145f, 0f), 0.65f, new Color(0.72f, 0.84f, 1f));
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f);
                RenderSettings.ambientIntensity = 1f;

                renderTexture = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                renderTexture.Create();
                previewCamera.targetTexture = renderTexture;

                previousActiveTexture = RenderTexture.active;
                RenderTexture.active = renderTexture;
                previewCamera.Render();

                iconTexture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false, false);
                iconTexture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                iconTexture.Apply(false, false);
                RenderTexture.active = previousActiveTexture;
                previousActiveTexture = null;

                var outputPath = Path.Combine(Application.dataPath, "RuneStorage/Terminal/Icons/RuneStorage_Terminal_Icon.png");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, iconTexture.EncodeToPNG());

                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }

                ShowDialog("RuneStorage icon", $"Created:\n{IconPath}");
            }
            finally
            {
                if (previousActiveTexture != null)
                {
                    RenderTexture.active = previousActiveTexture;
                    previousActiveTexture = null;
                }

                if (previewCamera != null)
                {
                    previewCamera.targetTexture = null;
                }

                if (iconTexture != null)
                {
                    Object.DestroyImmediate(iconTexture);
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }

                if (previewRoot != null)
                {
                    Object.DestroyImmediate(previewRoot);
                }

                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
        }

        [MenuItem("RuneStorage/Prepare Terminal Bundle Assets")]
        private static void PrepareTerminalBundleAssets()
        {
            var paths = new[] { PrefabPath, IconPath };
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    ShowDialog("RuneStorage bundle", $"Asset not found:\n{path}", true);
                    return;
                }

                importer.assetBundleName = BundleName;
                importer.SaveAndReimport();
            }

            var previousPrefabImporter = AssetImporter.GetAtPath(PreviousPrefabPath);
            if (previousPrefabImporter != null && previousPrefabImporter.assetBundleName == BundleName)
            {
                previousPrefabImporter.assetBundleName = string.Empty;
                previousPrefabImporter.SaveAndReimport();
            }

            AssetDatabase.SaveAssets();
            ShowDialog("RuneStorage bundle", $"Prepared prefab and icon with bundle name:\n{BundleName}");
        }

        [MenuItem("RuneStorage/Build Terminal AssetBundle")]
        public static void BuildTerminalAssetBundle()
        {
            PrepareTerminalBundleAssetsForBuild();

            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.NoTarget)
            {
                ShowDialog("RuneStorage bundle", "Unity has no active build target. Select a Windows build target first.", true);
                if (Application.isBatchMode) throw new System.InvalidOperationException("Unity has no active AssetBundle build target.");
                return;
            }

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
            Directory.CreateDirectory(outputDirectory);

            var manifest = BuildPipeline.BuildAssetBundles(
                outputDirectory,
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);

            if (manifest == null)
            {
                ShowDialog("RuneStorage bundle", $"Build failed for target {target}. Check the Console for details.", true);
                if (Application.isBatchMode) throw new System.InvalidOperationException($"Unity failed to build the terminal AssetBundle for {target}.");
                return;
            }

            var builtBundlePath = Path.Combine(outputDirectory, BundleName);
            if (!File.Exists(builtBundlePath))
            {
                throw new FileNotFoundException("The RuneStorage terminal AssetBundle output is missing.", builtBundlePath);
            }

            var sourceBundlePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../src/RuneStorage/Assets/AssetBundles/runestorage_terminal"));
            Directory.CreateDirectory(Path.GetDirectoryName(sourceBundlePath));
            File.Copy(builtBundlePath, sourceBundlePath, true);

            var message = $"Built terminal bundle for {target}:\n{outputDirectory}\n\nCopied to:\n{sourceBundlePath}";
            ShowDialog("RuneStorage bundle", message);
            Debug.Log(message);
        }

        [MenuItem("RuneStorage/Rebuild Terminal Icon and AssetBundle")]
        public static void RebuildTerminalAssetsForRuneStorage()
        {
            RenderTerminalIcon();
            BuildTerminalAssetBundle();
        }

        private static void PrepareTerminalBundleAssetsForBuild()
        {
            var prefabImporter = AssetImporter.GetAtPath(PrefabPath);
            var iconImporter = AssetImporter.GetAtPath(IconPath);
            if (prefabImporter == null || iconImporter == null)
            {
                throw new FileNotFoundException($"Terminal prefab or icon is missing. Prefab: {PrefabPath}; icon: {IconPath}.");
            }

            prefabImporter.assetBundleName = BundleName;
            iconImporter.assetBundleName = BundleName;

            var previousPrefabImporter = AssetImporter.GetAtPath(PreviousPrefabPath);
            if (previousPrefabImporter != null && previousPrefabImporter.assetBundleName == BundleName)
            {
                previousPrefabImporter.assetBundleName = string.Empty;
                previousPrefabImporter.SaveAndReimport();
            }

            prefabImporter.SaveAndReimport();
            iconImporter.SaveAndReimport();
            AssetDatabase.SaveAssets();
        }

        private static void ShowDialog(string title, string message, bool isError = false)
        {
            if (Application.isBatchMode)
            {
                if (isError) Debug.LogError($"{title}: {message}");
                else Debug.Log($"{title}: {message}");
                return;
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private static void CreateLight(Transform parent, string name, Vector3 rotation, float intensity, Color color)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, true);
            lightObject.transform.rotation = Quaternion.Euler(rotation);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.Soft;
        }
    }
}
