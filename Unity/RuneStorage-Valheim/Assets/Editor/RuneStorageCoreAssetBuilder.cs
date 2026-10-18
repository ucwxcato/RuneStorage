using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RuneStorage.EditorTools
{
    public static class RuneStorageCoreAssetBuilder
    {
        private const string SourcePath = "Assets/RuneStorage/Core/Source/cyber_core.glb";
        private const string PrefabPath = "Assets/RuneStorage/Core/Prefabs/RuneStorage_Core_Visual.prefab";
        private const string ControllerPath = "Assets/RuneStorage/Core/Animation/CyberCore_Idle.controller";
        private const string ClipPath = "Assets/RuneStorage/Core/Animation/CyberCore_Idle.anim";
        private const string IconPath = "Assets/RuneStorage/Core/Icons/RuneStorage_Core_Icon.png";
        private const string BundleName = "runestorage_core";
        private const string BundleSourcePath = "Assets/AssetBundles/runestorage_core";
        private const int IconSize = 256;

        [MenuItem("RuneStorage/Build Core AssetBundle")]
        private static void BuildCoreAssetBundle()
        {
            try
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
                if (source == null)
                {
                    EditorUtility.DisplayDialog("RuneStorage Core", $"Core GLB was not imported:\n{SourcePath}", "OK");
                    return;
                }

                AnimationClip sourceClip = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => clip != null && clip.name == "Animation");
                if (sourceClip == null)
                {
                    EditorUtility.DisplayDialog("RuneStorage Core", "The imported core has no clip named 'Animation'. Check its GLB Animation import setting.", "OK");
                    return;
                }

                EnsureFolder("Assets/RuneStorage/Core/Animation");
                EnsureFolder("Assets/RuneStorage/Core/Prefabs");
                EnsureFolder("Assets/RuneStorage/Core/Icons");
                EnsureFolder("Assets/AssetBundles");
                AnimationClip idleClip = CreateLoopingClip(sourceClip);
                AnimatorController controller = CreateIdleController(idleClip);

                Scene previewScene = EditorSceneManager.NewPreviewScene();
                GameObject root = null;
                GameObject model = null;

                try
                {
                    root = new GameObject("RuneStorage_Core_Visual");
                    SceneManager.MoveGameObjectToScene(root, previewScene);

                    model = UnityEngine.Object.Instantiate(source);
                    model.name = "CyberCore_Model";
                    SceneManager.MoveGameObjectToScene(model, previewScene);
                    model.transform.SetParent(root.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.one;

                    Animator animator = model.GetComponent<Animator>();
                    if (animator == null) animator = model.GetComponentInChildren<Animator>(true);
                    if (animator == null) animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;

                    Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0)
                    {
                        EditorUtility.DisplayDialog("RuneStorage Core", "The imported core has no renderers, so a visual prefab cannot be created.", "OK");
                        return;
                    }

                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    Vector3 localBoundsSize = bounds.size;
                    Vector3 visualOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                    model.transform.position += visualOffset;

                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    RenderIcon(PrefabPath);
                    SetBundleLabel(PrefabPath, BundleName);
                    SetBundleLabel(IconPath, BundleName);
                    AssetDatabase.SaveAssets();

                    BuildAndCopyBundle();

                    Debug.Log(
                        $"RuneStorage Core assets prepared. Clip='{idleClip.name}', duration={idleClip.length:F2}s, " +
                        $"bounds={localBoundsSize}, modelOffset={visualOffset}, prefab='{PrefabPath}', icon='{IconPath}', " +
                        $"bundle='{BundleName}', target={EditorUserBuildSettings.activeBuildTarget}.");

                    EditorUtility.DisplayDialog(
                        "RuneStorage Core",
                        $"Core visual, looping animation, icon, and AssetBundle are ready.\n\n" +
                        $"Prefab: {PrefabPath}\nIcon: {IconPath}\nBundle copied to: {BundleSourcePath}\n\n" +
                        $"Model bounds: {localBoundsSize}",
                        "OK");
                }
                finally
                {
                    if (model != null) UnityEngine.Object.DestroyImmediate(model);
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
                    if (previewScene.IsValid() && previewScene.isLoaded)
                    {
                        EditorSceneManager.ClosePreviewScene(previewScene);
                    }

                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("RuneStorage Core", $"Core asset build failed. Check the Unity Console.\n\n{exception.Message}", "OK");
            }
        }

        private static AnimationClip CreateLoopingClip(AnimationClip sourceClip)
        {
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (idleClip == null)
            {
                idleClip = UnityEngine.Object.Instantiate(sourceClip);
                idleClip.name = "CyberCore_Idle";
                AssetDatabase.CreateAsset(idleClip, ClipPath);
            }
            else
            {
                EditorUtility.CopySerialized(sourceClip, idleClip);
                idleClip.name = "CyberCore_Idle";
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(idleClip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(idleClip, settings);
            EditorUtility.SetDirty(idleClip);
            AssetDatabase.SaveAssets();
            return idleClip;
        }

        private static AnimatorController CreateIdleController(AnimationClip idleClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            if (controller.layers == null || controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state != null && state.name == "CyberCore_Idle");

            if (idleState == null) idleState = stateMachine.AddState("CyberCore_Idle");
            idleState.motion = idleClip;
            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void RenderIcon(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException($"Core visual prefab not found at {prefabPath}.");

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject previewRoot = null;
            Camera previewCamera = null;
            RenderTexture renderTexture = null;
            Texture2D iconTexture = null;
            RenderTexture previousActiveTexture = null;

            try
            {
                previewRoot = new GameObject("RuneStorage_Core_Icon_Preview");
                SceneManager.MoveGameObjectToScene(previewRoot, previewScene);
                GameObject model = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(model, previewScene);
                model.transform.SetParent(previewRoot.transform, true);

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Core visual prefab has no renderers for icon rendering.");
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                Vector3 target = bounds.center + Vector3.up * (bounds.extents.y * 0.05f);
                GameObject cameraObject = new GameObject("RuneStorage_Core_Icon_Camera");
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

                CreateLight(previewRoot.transform, "RuneStorage_Core_Icon_Key", new Vector3(35f, -35f, 0f), 1.25f, new Color(1f, 0.91f, 0.78f));
                CreateLight(previewRoot.transform, "RuneStorage_Core_Icon_Fill", new Vector3(55f, 145f, 0f), 0.65f, new Color(0.72f, 0.84f, 1f));
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

                string fullIconPath = Path.Combine(Application.dataPath, "RuneStorage/Core/Icons/RuneStorage_Core_Icon.png");
                Directory.CreateDirectory(Path.GetDirectoryName(fullIconPath));
                File.WriteAllBytes(fullIconPath, iconTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
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
            }
            finally
            {
                if (previousActiveTexture != null) RenderTexture.active = previousActiveTexture;
                if (previewCamera != null) previewCamera.targetTexture = null;
                if (iconTexture != null) UnityEngine.Object.DestroyImmediate(iconTexture);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (previewRoot != null) UnityEngine.Object.DestroyImmediate(previewRoot);
                if (previewScene.IsValid() && previewScene.isLoaded) EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void BuildAndCopyBundle()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.NoTarget) throw new InvalidOperationException("Unity has no active build target.");

            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds"));
            Directory.CreateDirectory(outputDirectory);
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputDirectory,
                BuildAssetBundleOptions.ChunkBasedCompression,
                target);
            if (manifest == null) throw new InvalidOperationException($"Unity failed to build AssetBundles for {target}.");

            string builtBundlePath = Path.Combine(outputDirectory, BundleName);
            if (!File.Exists(builtBundlePath)) throw new FileNotFoundException("The Core AssetBundle output is missing.", builtBundlePath);

            string sourceBundlePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../src/RuneStorage/Assets/AssetBundles/runestorage_core"));
            Directory.CreateDirectory(Path.GetDirectoryName(sourceBundlePath));
            File.Copy(builtBundlePath, sourceBundlePath, true);
        }

        private static void SetBundleLabel(string assetPath, string bundleName)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null) throw new InvalidOperationException($"Cannot set AssetBundle label; asset not found: {assetPath}");
            importer.assetBundleName = bundleName;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folder = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            {
                throw new DirectoryNotFoundException($"Unity asset parent folder is missing: {parent}");
            }

            AssetDatabase.CreateFolder(parent, folder);
        }

        private static void CreateLight(Transform parent, string name, Vector3 rotation, float intensity, Color color)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, true);
            lightObject.transform.rotation = Quaternion.Euler(rotation);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.Soft;
        }
    }
}
