using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CharacterLogic.Editor
{
    public static class ParkourSceneBaker
    {
        private const string MaterialFolder = "Assets/Settings/Parkour";

        [MenuItem("Tools/Parkour/Bake All Gameplay Scenes")]
        public static void BakeAll()
        {
            EnsureFolder();
            string[] sceneCandidates =
            {
                "Assets/Scenes/ParkourCity.unity",
                "Assets/Scenes/SkyTemple.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            foreach (string scenePath in sceneCandidates)
            {
                if (File.Exists(scenePath))
                {
                    BakeScene(scenePath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Parkour] Gameplay geometry baked into existing scenes.");
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ParkourCity.unity", OpenSceneMode.Single);
            GameObject cameraObject = new GameObject("Preview Camera");
            Camera previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.transform.position = new Vector3(0f, 10.5f, -17f);
            previewCamera.transform.LookAt(new Vector3(0f, 2.8f, 29f));
            previewCamera.fieldOfView = 63f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 180f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.012f, 0.008f, 0.045f);
            previewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            RenderTexture renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            previewCamera.targetTexture = renderTexture;
            previewCamera.Render();
            RenderTexture.active = renderTexture;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../ParkourPreview.png"));
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            RenderTexture.active = null;
            previewCamera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Debug.Log("[Parkour] Preview written to " + outputPath);
        }

        public static void ListQuaterniusClips()
        {
            const string path = "Assets/ThirdParty/QuaterniusUAL2/UAL2_Standard.fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    Debug.Log($"[UAL2] {clip.name} | {clip.length:F3}s | loop={clip.isLooping}");
                }
            }
        }

        private static void BakeScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject world = GameObject.Find("Parkour World");
            if (world == null)
            {
                throw new MissingReferenceException("Parkour World not found in " + scenePath);
            }

            if (!scenePath.Contains("SkyTemple"))
            {
                AddKenneyCity(world.transform, Path.GetFileNameWithoutExtension(scenePath));
            }
            EnsurePostProcessing(world.transform, Path.GetFileNameWithoutExtension(scenePath));
            PersistMaterials(world, Path.GetFileNameWithoutExtension(scenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void PersistMaterials(GameObject world, string sceneName)
        {
            Renderer[] renderers = world.GetComponentsInChildren<Renderer>(true);
            Dictionary<Material, Material> persistentMaterials = new Dictionary<Material, Material>();
            foreach (Renderer renderer in renderers)
            {
                Material source = renderer.sharedMaterial;
                if (source == null || AssetDatabase.Contains(source))
                {
                    continue;
                }

                if (!persistentMaterials.TryGetValue(source, out Material persistent))
                {
                    string safeName = source.name.Replace(' ', '_');
                    string assetPath = $"{MaterialFolder}/{sceneName}_{safeName}.mat";
                    persistent = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (persistent == null)
                    {
                        persistent = new Material(source);
                        AssetDatabase.CreateAsset(persistent, assetPath);
                    }
                    else
                    {
                        persistent.CopyPropertiesFromMaterial(source);
                        persistent.shaderKeywords = source.shaderKeywords;
                        EditorUtility.SetDirty(persistent);
                    }

                    persistentMaterials.Add(source, persistent);
                }

                renderer.sharedMaterial = persistent;
            }
        }

        private static void AddKenneyCity(Transform world, string sceneName)
        {

            List<GameObject> placeholders = new List<GameObject>();
            foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "City Tower") placeholders.Add(child.gameObject);
            }

            Material[] cityMaterials =
            {
                GetOrCreateCityMaterial(sceneName, 0, new Color(0.72f, 0.86f, 1f)),
                GetOrCreateCityMaterial(sceneName, 1, new Color(1f, 0.72f, 0.82f)),
                GetOrCreateCityMaterial(sceneName, 2, new Color(1f, 0.84f, 0.62f)),
                GetOrCreateCityMaterial(sceneName, 3, new Color(0.68f, 1f, 0.86f))
            };
            string[] models =
            {
                "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d",
                "building-skyscraper-e"
            };

            for (int i = 0; i < placeholders.Count; i++)
            {
                Transform placeholder = placeholders[i].transform;
                Vector3 footprint = placeholder.lossyScale;
                float bottomY = placeholder.position.y - footprint.y * 0.5f;
                Vector3 basePosition = new Vector3(placeholder.position.x, bottomY, placeholder.position.z);
                Object.DestroyImmediate(placeholders[i]);

                string assetPath = $"Assets/ThirdParty/KenneyCityCommercial/Models/{models[i % models.Length]}.fbx";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                building.name = "City Architecture " + i.ToString("00");
                building.transform.SetParent(world, true);
                float yaw = (i * 37 % 4) * 90f;
                building.transform.SetPositionAndRotation(basePosition, Quaternion.Euler(0f, yaw, 0f));

                Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                Bounds bounds = CombinedBounds(renderers);
                float scale = footprint.y / Mathf.Max(0.1f, bounds.size.y);
                building.transform.localScale = Vector3.one * scale;

                bounds = CombinedBounds(renderers);
                building.transform.position += new Vector3(basePosition.x - bounds.center.x, bottomY - bounds.min.y, basePosition.z - bounds.center.z);
                foreach (Renderer renderer in renderers)
                {
                    renderer.sharedMaterial = cityMaterials[i % cityMaterials.Length];
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
            return bounds;
        }

        private static Material GetOrCreateCityMaterial(string sceneName, int variant, Color tint)
        {
            string materialPath = $"{MaterialFolder}/{sceneName}_KenneyCity_{variant}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = sceneName + " Kenney City " + variant };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/KenneyCityCommercial/Textures/variation-a.png");
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsurePostProcessing(Transform world, string sceneName)
        {
            string profilePath = $"{MaterialFolder}/{sceneName}_ParkourPost.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(0.72f);
            bloom.scatter.Override(0.72f);
            bloom.clamp.Override(14f);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(12f);
            color.saturation.Override(18f);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0.01f, 0.005f, 0.025f));
            vignette.intensity.Override(0.23f);
            vignette.smoothness.Override(0.72f);
            EditorUtility.SetDirty(profile);

            Transform existingVolume = world.Find("Parkour Post Processing");
            GameObject volumeObject = existingVolume != null ? existingVolume.gameObject : new GameObject("Parkour Post Processing");
            if (existingVolume == null)
            {
                volumeObject.transform.SetParent(world, false);
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObject.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet(out T component) ? component : profile.Add<T>(true);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Parkour");
            }
        }
    }
}
