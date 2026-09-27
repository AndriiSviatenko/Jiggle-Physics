using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JigglePhysics.EditorTools
{
    public static class JiggleDemoSetup
    {
        private const string ProfileFolder = "Assets/Settings/JigglePhysics";
        private const string ProfilePath = ProfileFolder + "/BreastProfile.asset";

        private static readonly string[] BreastRootNames = { "Breast_L", "Breast_R", "J_Sec_L_Bust1", "J_Sec_R_Bust1" };

        [MenuItem("Tools/Jiggle Physics/Setup Breast Rig On Selection")]
        public static void SetupOnSelection()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                Debug.LogError("[JigglePhysics] Select the character root in the Hierarchy first.");
                return;
            }

            Setup(target);
        }

        [MenuItem("Tools/Jiggle Physics/Setup Breast Rig On Henita")]
        public static void SetupHenita()
        {
            GameObject target = GameObject.Find("Henita");
            if (target == null)
            {
                Debug.LogError("[JigglePhysics] No 'Henita' object found in the active scene.");
                return;
            }

            Setup(target);
        }

        private static void Setup(GameObject character)
        {
            List<Transform> roots = FindBreastRoots(character.transform);
            if (roots.Count == 0)
            {
                Debug.LogError($"[JigglePhysics] No breast bones found under '{character.name}'. Looked for: {string.Join(", ", BreastRootNames)}.");
                return;
            }

            JiggleProfile profile = LoadOrCreateProfile();

            Undo.RegisterCreatedObjectUndo(profile, "Create Jiggle Profile");
            JiggleRig rig = Undo.AddComponent<JiggleRig>(character);

            SerializedObject rigObject = new SerializedObject(rig);
            rigObject.FindProperty("defaultProfile").objectReferenceValue = profile;
            rigObject.FindProperty("teleportThreshold").floatValue = 0.5f;
            rigObject.FindProperty("drawGizmos").boolValue = true;

            SerializedProperty chains = rigObject.FindProperty("chains");
            chains.ClearArray();
            chains.arraySize = roots.Count;
            for (int i = 0; i < roots.Count; i++)
            {
                SerializedProperty element = chains.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("RootBone").objectReferenceValue = roots[i];
                element.FindPropertyRelative("EndBone").objectReferenceValue = ResolveEndBone(roots[i]);
                element.FindPropertyRelative("ProfileOverride").objectReferenceValue = null;
            }

            rigObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);

            List<MonoBehaviour> colliders = CreateColliders(character, roots);

            SerializedObject rigObjectAgain = new SerializedObject(rig);
            SerializedProperty colliderProperty = rigObjectAgain.FindProperty("colliderBehaviours");
            colliderProperty.ClearArray();
            colliderProperty.arraySize = colliders.Count;
            for (int i = 0; i < colliders.Count; i++)
            {
                colliderProperty.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
            }

            rigObjectAgain.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);

            if (character.GetComponent<JigglePhysics.Demo.JiggleDemoDriver>() == null)
            {
                Undo.AddComponent<JigglePhysics.Demo.JiggleDemoDriver>(character);
            }

            EnsureSimulation();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);

            Debug.Log($"[JigglePhysics] Rig created on '{character.name}' with {roots.Count} chains and {colliders.Count} colliders. Profile: {ProfilePath}");
        }

        private static JiggleProfile LoadOrCreateProfile()
        {
            JiggleProfile existing = AssetDatabase.LoadAssetAtPath<JiggleProfile>(ProfilePath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "JigglePhysics");
            }

            JiggleProfile profile = ScriptableObject.CreateInstance<JiggleProfile>();
            SerializedObject profileObject = new SerializedObject(profile);
            profileObject.FindProperty("frequency").floatValue = 3f;
            profileObject.FindProperty("dampingRatio").floatValue = 0.3f;
            profileObject.FindProperty("gravityMultiplier").floatValue = 1f;
            profileObject.FindProperty("maxAngle").floatValue = 45f;
            profileObject.FindProperty("maxOffset").floatValue = 0.09f;
            profileObject.FindProperty("pointRadius").floatValue = 0.04f;
            profileObject.FindProperty("maxSubsteps").intValue = 4;
            profileObject.FindProperty("blend").floatValue = 1f;
            profileObject.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static List<MonoBehaviour> CreateColliders(GameObject character, List<Transform> breastRoots)
        {
            List<MonoBehaviour> colliders = new List<MonoBehaviour>();
            Transform chest = FindAncestorNamed(breastRoots[0], "Chest");
            Transform host = chest != null ? chest : character.transform;

            Vector3 averageRoot = Vector3.zero;
            for (int i = 0; i < breastRoots.Count; i++)
            {
                averageRoot += breastRoots[i].position;
            }

            averageRoot /= breastRoots.Count;
            Vector3 localAverage = host.InverseTransformPoint(averageRoot);

            GameObject torsoObject = new GameObject("JiggleTorsoCollider");
            Undo.RegisterCreatedObjectUndo(torsoObject, "Create Torso Collider");
            torsoObject.transform.SetParent(host, false);
            torsoObject.transform.localPosition = localAverage + new Vector3(0f, -0.05f, -0.045f);
            torsoObject.transform.localRotation = Quaternion.identity;

            JiggleSphereCollider sphere = torsoObject.AddComponent<JiggleSphereCollider>();
            SerializedObject sphereObject = new SerializedObject(sphere);
            sphereObject.FindProperty("radius").floatValue = 0.085f;
            sphereObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sphere);

            colliders.Add(sphere);
            return colliders;
        }

        private static List<Transform> FindBreastRoots(Transform root)
        {
            List<Transform> result = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                for (int n = 0; n < BreastRootNames.Length; n++)
                {
                    if (all[i].name == BreastRootNames[n])
                    {
                        result.Add(all[i]);
                        break;
                    }
                }
            }

            return result;
        }

        private static Transform ResolveEndBone(Transform breastRoot)
        {
            if (breastRoot.childCount == 1)
            {
                return breastRoot.GetChild(0);
            }

            return null;
        }

        private static Transform FindAncestorNamed(Transform start, string name)
        {
            Transform current = start;
            while (current != null)
            {
                if (current.name == name)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private static void EnsureSimulation()
        {
            if (Object.FindFirstObjectByType<JiggleSimulation>() != null)
            {
                return;
            }

            GameObject host = new GameObject(nameof(JiggleSimulation));
            Undo.RegisterCreatedObjectUndo(host, "Create Jiggle Simulation");
            host.AddComponent<JiggleSimulation>();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        }
    }
}
