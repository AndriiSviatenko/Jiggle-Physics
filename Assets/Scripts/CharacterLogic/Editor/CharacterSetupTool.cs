using CharacterLogic;
using JigglePhysics;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterLogic.EditorTools
{
    public static class CharacterSetupTool
    {
        private const string SettingsFolder = "Assets/Settings/Character";
        private const string ControllerConfigPath = SettingsFolder + "/CharacterControllerConfig.asset";
        private const string CameraConfigPath = SettingsFolder + "/CameraOrbitConfig.asset";
        private const string SolverConfigPath = SettingsFolder + "/JiggleSolverConfig.asset";
        private const string InputAssetPath = SettingsFolder + "/CharacterInput.inputactions";

        [MenuItem("Tools/Character/Setup Playable Test Rig On Henita")]
        public static void SetupHenita()
        {
            GameObject character = GameObject.Find("Henita");
            if (character == null)
            {
                Debug.LogError("[CharacterSetup] No 'Henita' object in the active scene.");
                return;
            }

            Setup(character);
        }

        private static void Setup(GameObject character)
        {
            EnsureFolder();

            CharacterControllerConfig controllerConfig = LoadOrCreate(ControllerConfigPath, () =>
            {
                CharacterControllerConfig config = ScriptableObject.CreateInstance<CharacterControllerConfig>();
                return config;
            });

            CameraOrbitConfig cameraConfig = LoadOrCreate(CameraConfigPath, () => ScriptableObject.CreateInstance<CameraOrbitConfig>());
            JiggleSolverConfig solverConfig = LoadOrCreate(SolverConfigPath, () => ScriptableObject.CreateInstance<JiggleSolverConfig>());
            JiggleSolverConfig.Active = solverConfig;

            InputActionAsset inputAsset = LoadOrCreateInputAsset();

            DisableDemoDriver(character);

            CharacterController controller = character.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CharacterController>(character);
            }

            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.skinWidth = 0.02f;
            EditorUtility.SetDirty(controller);

            Transform followTarget = character.transform.Find("CameraFollowTarget");
            if (followTarget == null)
            {
                GameObject targetObject = new GameObject("CameraFollowTarget");
                Undo.RegisterCreatedObjectUndo(targetObject, "Create Camera Follow Target");
                targetObject.transform.SetParent(character.transform, false);
                targetObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                followTarget = targetObject.transform;
            }

            CinemachineOrbitalFollow orbital = SetupCamera(followTarget, cameraConfig);

            Animator animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                animator = character.GetComponentInChildren<Animator>();
            }
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            CharacterBridge bridge = character.GetComponent<CharacterBridge>();
            if (bridge == null)
            {
                bridge = Undo.AddComponent<CharacterBridge>(character);
            }

            SerializedObject bridgeObject = new SerializedObject(bridge);
            bridgeObject.FindProperty("controllerConfig").objectReferenceValue = controllerConfig;
            bridgeObject.FindProperty("cameraConfig").objectReferenceValue = cameraConfig;
            bridgeObject.FindProperty("inputAsset").objectReferenceValue = inputAsset;
            bridgeObject.FindProperty("orbitalFollow").objectReferenceValue = orbital;
            bridgeObject.FindProperty("animator").objectReferenceValue = animator;
            bridgeObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);

            JiggleRig rig = character.GetComponent<JiggleRig>();
            if (rig != null)
            {
                SerializedObject rigObject = new SerializedObject(rig);
                rigObject.FindProperty("solverConfig").objectReferenceValue = solverConfig;
                rigObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rig);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);
            Debug.Log("[CharacterSetup] Playable test rig assembled on '" + character.name + "'.");
        }

        private static void DisableDemoDriver(GameObject character)
        {
            MonoBehaviour[] behaviours = character.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == "JiggleDemoDriver")
                {
                    Undo.DestroyObjectImmediate(behaviours[i]);
                }
            }
        }

        private static CinemachineOrbitalFollow SetupCamera(Transform followTarget, CameraOrbitConfig cameraConfig)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);
            }

            GameObject vcamObject = GameObject.Find("PlayerCamera");
            if (vcamObject == null)
            {
                vcamObject = new GameObject("PlayerCamera");
                Undo.RegisterCreatedObjectUndo(vcamObject, "Create Player Camera");
            }

            CinemachineCamera vcam = vcamObject.GetComponent<CinemachineCamera>();
            if (vcam == null)
            {
                vcam = vcamObject.AddComponent<CinemachineCamera>();
            }

            vcam.Follow = followTarget;
            vcam.LookAt = followTarget;

            CinemachineOrbitalFollow orbital = vcamObject.GetComponent<CinemachineOrbitalFollow>();
            if (orbital == null)
            {
                orbital = vcamObject.AddComponent<CinemachineOrbitalFollow>();
            }

            orbital.Radius = cameraConfig.Radius;
            orbital.TargetOffset = Vector3.zero;

            if (vcamObject.GetComponent<CinemachineRotationComposer>() == null)
            {
                vcamObject.AddComponent<CinemachineRotationComposer>();
            }

            EditorUtility.SetDirty(vcamObject);
            return orbital;
        }

        private static InputActionAsset LoadOrCreateInputAsset()
        {
            InputActionAsset existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (existing != null)
            {
                return existing;
            }

            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "CharacterInput";

            InputActionMap map = new InputActionMap("Main");
            asset.AddActionMap(map);

            InputAction move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            InputAction look = map.AddAction("Look", InputActionType.Value);
            look.expectedControlType = "Vector2";
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            InputAction jump = map.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            InputAction run = map.AddAction("Run", InputActionType.Button);
            run.AddBinding("<Keyboard>/leftShift");
            run.AddBinding("<Gamepad>/leftStickPress");

            InputAction sit = map.AddAction("Sit", InputActionType.Button);
            sit.AddBinding("<Keyboard>/c");
            sit.AddBinding("<Gamepad>/buttonEast");

            InputAction attack = map.AddAction("Attack", InputActionType.Button);
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");

            System.IO.File.WriteAllText(InputAssetPath, asset.ToJson());
            AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceUpdate);
            InputActionAsset imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (imported == null)
            {
                Debug.LogError("[CharacterSetup] CharacterInput.inputactions failed to import as InputActionAsset.");
            }

            return imported != null ? imported : asset;
        }

        private static T LoadOrCreate<T>(string path, System.Func<T> factory) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T instance = factory();
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            return instance;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Character");
            }
        }
    }
}
