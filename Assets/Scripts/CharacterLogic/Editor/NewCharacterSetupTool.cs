using JigglePhysics;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class NewCharacterSetupTool
    {
        private const string NewModelPath = "Assets/Models/Henita.fbx";
        private const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";
        private const string SettingsFolder = "Assets/Settings/Character";
        private const string JiggleFolder = "Assets/Settings/JigglePhysics";

        [MenuItem("Tools/Character/Build New Mixamo Character")]
        public static void Build()
        {
            PrepareClips();

            GameObject old = GameObject.Find("Henita");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            GameObject assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(NewModelPath);
            if (assetRoot == null)
            {
                Debug.LogError("[NewChar] model not found at " + NewModelPath);
                return;
            }

            GameObject character = PrefabUtility.InstantiatePrefab(assetRoot) as GameObject;
            if (character == null)
            {
                Debug.LogError("[NewChar] instantiate failed");
                return;
            }

            character.name = "Henita";
            character.transform.position = Vector3.zero;
            character.transform.rotation = Quaternion.identity;

            Unpack(character);

            Transform armature = character.transform.Find("Armature");
            Transform body = character.transform.Find("Body");
            if (armature == null || body == null)
            {
                Debug.LogError("[NewChar] Armature/Body not found");
                return;
            }

            body.SetParent(armature, false);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = Vector3.one;

            Animator animator = armature.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.enabled = true;

            CharacterController controller = character.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.skinWidth = 0.02f;

            GameObject targetObject = new GameObject("CameraFollowTarget");
            targetObject.transform.SetParent(character.transform, false);
            targetObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            GameObject vcam = GameObject.Find("PlayerCamera");
            Unity.Cinemachine.CinemachineOrbitalFollow orbital = vcam != null ? vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>() : null;
            Unity.Cinemachine.CinemachineCamera cinemachineCamera = vcam != null ? vcam.GetComponent<Unity.Cinemachine.CinemachineCamera>() : null;
            if (cinemachineCamera != null)
            {
                cinemachineCamera.Follow = targetObject.transform;
                cinemachineCamera.LookAt = targetObject.transform;
                EditorUtility.SetDirty(cinemachineCamera);
            }

            CharacterControllerConfig controllerConfig = AssetDatabase.LoadAssetAtPath<CharacterControllerConfig>(SettingsFolder + "/CharacterControllerConfig.asset");
            if (controllerConfig != null)
            {
                SerializedObject configObject = new SerializedObject(controllerConfig);
                configObject.FindProperty("animationDrivenLocomotion").boolValue = false;
                configObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controllerConfig);
            }

            CharacterBridge bridge = character.AddComponent<CharacterBridge>();
            SerializedObject bridgeObject = new SerializedObject(bridge);
            bridgeObject.FindProperty("controllerConfig").objectReferenceValue = controllerConfig;
            bridgeObject.FindProperty("cameraConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CameraOrbitConfig>(SettingsFolder + "/CameraOrbitConfig.asset");
            bridgeObject.FindProperty("inputAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(SettingsFolder + "/CharacterInput.inputactions");
            bridgeObject.FindProperty("orbitalFollow").objectReferenceValue = orbital;
            bridgeObject.FindProperty("animator").objectReferenceValue = animator;
            bridgeObject.ApplyModifiedPropertiesWithoutUndo();

            SetupJiggle(character, armature);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);
            Debug.Log("[NewChar] TPS character built: controller on root, in-place animations on Armature.");
        }

        private static void PrepareClips()
        {
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.IdleClip);
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.WalkClip);
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.RunClip);
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.JumpClip);
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.AttackClip);
            AnimationClipSetupTool.RevertToGeneric(AnimationClipSetupTool.Attack2Clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                AnimationClipSetupTool.RebuildMotions(controller);
            }
        }

        private static void SetupJiggle(GameObject character, Transform armature)
        {
            JiggleProfile profile = AssetDatabase.LoadAssetAtPath<JiggleProfile>(JiggleFolder + "/BreastProfile.asset");
            JiggleSolverConfig solverConfig = AssetDatabase.LoadAssetAtPath<JiggleSolverConfig>(SettingsFolder + "/JiggleSolverConfig.asset");

            Transform breastLeft = FindByName(armature, "Breast_L");
            Transform breastRight = FindByName(armature, "Breast_R");
            Transform bustLeft = FindByName(armature, "J_Sec_L_Bust2");
            Transform bustRight = FindByName(armature, "J_Sec_R_Bust2");
            Transform chest = FindByName(armature, "Chest");

            if (breastLeft == null || breastRight == null)
            {
                Debug.LogWarning("[NewChar] breast bones not found; jiggle skipped.");
                return;
            }

            JiggleRig rig = character.AddComponent<JiggleRig>();

            GameObject colliderObject = new GameObject("JiggleTorsoCollider");
            colliderObject.transform.SetParent(chest != null ? chest : armature, false);
            colliderObject.transform.localPosition = new Vector3(0f, 0.03f, 0.03f);
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            SerializedObject sphereObject = new SerializedObject(sphere);
            sphereObject.FindProperty("radius").floatValue = 0.085f;
            sphereObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject rigObject = new SerializedObject(rig);
            rigObject.FindProperty("defaultProfile").objectReferenceValue = profile;
            rigObject.FindProperty("solverConfig").objectReferenceValue = solverConfig;

            SerializedProperty chains = rigObject.FindProperty("chains");
            chains.arraySize = 2;
            chains.GetArrayElementAtIndex(0).FindPropertyRelative("RootBone").objectReferenceValue = breastLeft;
            chains.GetArrayElementAtIndex(0).FindPropertyRelative("EndBone").objectReferenceValue = bustLeft;
            chains.GetArrayElementAtIndex(1).FindPropertyRelative("RootBone").objectReferenceValue = breastRight;
            chains.GetArrayElementAtIndex(1).FindPropertyRelative("EndBone").objectReferenceValue = bustRight;

            SerializedProperty colliders = rigObject.FindProperty("colliderBehaviours");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = sphere;
            rigObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);
        }

        private static Transform FindByName(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void Unpack(GameObject instance)
        {
            MethodInfo method = null;
            MethodInfo[] methods = typeof(PrefabUtility).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "UnpackPrefabInstance" && methods[i].GetParameters().Length == 3)
                {
                    method = methods[i];
                    break;
                }
            }

            if (method == null)
            {
                Debug.LogWarning("[NewChar] UnpackPrefabInstance not found; continuing as prefab instance.");
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();
            object scope = System.Enum.ToObject(parameters[1].ParameterType, 0);
            method.Invoke(null, new object[] { instance, scope, InteractionMode.AutomatedAction });
        }
    }
}
