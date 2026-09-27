using System.Collections.Generic;
using JigglePhysics.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JigglePhysics.EditorTools
{
    public static class JiggleLabSetup
    {
        private const string ScenePath = "Assets/Scenes/JiggleLab.unity";
        private const string CharacterNameA = "Henita_A";
        private const string CharacterNameB = "Henita_B";
        private const string LabRootName = "JiggleLab";
        private static readonly Vector3 CharacterAPosition = new Vector3(-1.7f, 0f, 0f);
        private static readonly Vector3 CharacterBPosition = new Vector3(1.7f, 0f, 0f);
        private static readonly Vector3 WideCameraPosition = new Vector3(0.95f, 2.2f, -7.4f);
        private static readonly Vector3 WideCameraEuler = new Vector3(5f, 0f, 0f);

        private static readonly string[] PresetNames = { "1_Zhele", "2_Breast", "3_Pruzhno", "4_Vidskok0" };
        private static readonly Vector3[] PresetPositions =
        {
            new Vector3(-3.3f, 2.4f, 1.6f),
            new Vector3(-0.8f, 2.4f, 1.6f),
            new Vector3(1.7f, 2.4f, 1.6f),
            new Vector3(4.2f, 2.4f, 1.6f),
        };

        [MenuItem("Tools/Jiggle Physics/Apply Jiggle Lab Layout")]
        public static void ApplyLayout()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            GameObject source = FindCharacter();
            if (source == null)
            {
                Debug.LogError("[JiggleLab] Не знайдено модель персонажа з Animator + JiggleRig у сцені.");
                return;
            }

            RemoveLegacyComponents(source.transform.root.gameObject);
            GameObject characterA = EnsureCharacterA(source);
            GameObject characterB = EnsureCharacterB(source);
            RemoveLegacyComponents(characterB);

            JiggleRig rigA = EnsureRig(characterA);
            JiggleRig rigB = EnsureRig(characterB);

            List<JiggleRig> presetRigs = new List<JiggleRig>();
            for (int i = 0; i < PresetNames.Length; i++)
            {
                GameObject preset = FindRoot(PresetNames[i]);
                if (preset == null)
                {
                    continue;
                }

                preset.transform.position = PresetPositions[i];
                RemoveTipTrail(preset);
                presetRigs.Add(EnsureRig(preset));
            }

            GameObject labRoot = EnsureLabRoot();
            JiggleLabController controller = EnsureComponent<JiggleLabController>(labRoot);
            JiggleLabCharacterDriver driver = EnsureComponent<JiggleLabCharacterDriver>(labRoot);
            JiggleLabCameraRig cameraRig = EnsureComponent<JiggleLabCameraRig>(labRoot);
            EnsureComponent<JiggleLabHud>(labRoot);

            WireController(controller, rigA, rigB, presetRigs, driver, cameraRig);
            ApplyWideCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[JiggleLab] Layout applied: A='{characterA.name}', B='{characterB.name}', presets={presetRigs.Count}, lab='{labRoot.name}'.");
        }

        private static GameObject FindCharacter()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<Animator>() == null || root.GetComponentInChildren<JiggleRig>() == null)
                {
                    continue;
                }

                if (root.name.StartsWith("Henita"))
                {
                    return root;
                }

                if (fallback == null)
                {
                    fallback = root;
                }
            }

            return fallback;
        }

        private static GameObject EnsureCharacterA(GameObject source)
        {
            if (source.name != CharacterNameA)
            {
                source.name = CharacterNameA;
            }

            source.transform.position = CharacterAPosition;
            return source;
        }

        private static GameObject EnsureCharacterB(GameObject source)
        {
            GameObject existing = FindRoot(CharacterNameB);
            if (existing != null)
            {
                existing.transform.position = CharacterBPosition;
                existing.transform.rotation = source.transform.rotation;
                return existing;
            }

            GameObject clone = Object.Instantiate(source);
            clone.name = CharacterNameB;
            clone.transform.SetParent(source.transform.parent, false);
            clone.transform.position = CharacterBPosition;
            clone.transform.rotation = source.transform.rotation;
            return clone;
        }

        private static JiggleRig EnsureRig(GameObject target)
        {
            JiggleRig rig = target.GetComponent<JiggleRig>();
            if (rig == null)
            {
                return null;
            }

            if (target.GetComponent<JiggleTargetVisualizer>() == null)
            {
                target.AddComponent<JiggleTargetVisualizer>();
            }

            return rig;
        }

        private static GameObject EnsureLabRoot()
        {
            GameObject existing = FindRoot(LabRootName);
            if (existing != null)
            {
                return existing;
            }

            GameObject labRoot = new GameObject(LabRootName);
            labRoot.transform.position = Vector3.zero;
            return labRoot;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void WireController(JiggleLabController controller, JiggleRig rigA, JiggleRig rigB, List<JiggleRig> presetRigs, JiggleLabCharacterDriver driver, JiggleLabCameraRig cameraRig)
        {
            SerializedObject serialized = new SerializedObject(controller);

            SerializedProperty duelProperty = serialized.FindProperty("duelRigs");
            duelProperty.arraySize = 2;
            duelProperty.GetArrayElementAtIndex(0).objectReferenceValue = rigA;
            duelProperty.GetArrayElementAtIndex(1).objectReferenceValue = rigB;

            SerializedProperty presetProperty = serialized.FindProperty("presetRigs");
            presetProperty.arraySize = presetRigs.Count;
            for (int i = 0; i < presetRigs.Count; i++)
            {
                presetProperty.GetArrayElementAtIndex(i).objectReferenceValue = presetRigs[i];
            }

            serialized.FindProperty("characterDriver").objectReferenceValue = driver;
            serialized.FindProperty("cameraRig").objectReferenceValue = cameraRig;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveTipTrail(GameObject preset)
        {
            TrailRenderer[] trails = preset.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                Object.DestroyImmediate(trails[i].gameObject);
            }
        }

        private static void RemoveLegacyComponents(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                if (typeName == "JiggleVisualExplainer" || typeName == "JiggleModelShowcase")
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static void ApplyWideCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.position = WideCameraPosition;
            camera.transform.rotation = Quaternion.Euler(WideCameraEuler);
        }

        private static GameObject FindRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }
    }
}
