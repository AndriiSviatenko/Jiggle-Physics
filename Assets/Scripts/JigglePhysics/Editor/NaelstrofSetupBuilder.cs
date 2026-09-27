using System.Collections.Generic;
using CharacterLogic;
using JigglePhysics.Demo;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using NaelstrofCollider = GatorDragonGames.JigglePhysics.JiggleCollider;
using NaelstrofRig = GatorDragonGames.JigglePhysics.JiggleRig;

namespace JigglePhysics.EditorTools
{
    public static class NaelstrofSetupBuilder
    {
        private sealed class BreastCollisionSetup
        {
            public Transform Transform;
            public float Radius;
            public float Height;
        }

        private const string ScenePath = "Assets/Scenes/naelstrofSetup.unity";
        private const string ModelPath = "Assets/Models/Henita.fbx";
        private const string AnimatorControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";
        private const string ControllerConfigPath = "Assets/Settings/Character/CharacterControllerConfig.asset";
        private const string CameraConfigPath = "Assets/Settings/Character/CameraOrbitConfig.asset";
        private const string InputAssetPath = "Assets/Settings/Character/CharacterInput.inputactions";
        private const string UpdaterPrefabPath = "Packages/com.gator-dragon-games.jigglephysics/Prefabs/JigglePhysicsUpdater.prefab";
        private const string VisualRootName = "Visual Animation Root";

        private static readonly string[] BreastBoneNames = { "Breast_L", "Breast_R", "J_Sec_L_Bust1", "J_Sec_R_Bust1" };

        [MenuItem("Tools/Jiggle Physics/Build Naelstrof Setup Scene")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildMainCamera();
            BuildSun();
            BuildGround();
            BuildParkourProps();

            GameObject playerCamera = BuildPlayerCamera();
            GameObject character = BuildCharacter(playerCamera);
            InstantiateUpdater();
            WireCameraTargets(playerCamera, character);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[NaelstrofSetup] Playable scene built: " + ScenePath);
        }

        private static void BuildMainCamera()
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CinemachineBrain>();
            cameraGo.transform.position = new Vector3(0.2f, 2.4f, -5.5f);
            cameraGo.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        }

        private static void BuildSun()
        {
            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.95f;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50f, 324f, 0f);
        }

        private static void BuildGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            SetColor(ground, new Color(0.18f, 0.19f, 0.22f));
        }

        private static void BuildParkourProps()
        {
            GameObject props = new GameObject("Parkour Props");
            CreateBox(props.transform, "Vault Low", new Vector3(2.4f, 0.6f, 0.8f), new Vector3(0f, 0.3f, 7f));
            CreateBox(props.transform, "Vault High", new Vector3(2.4f, 1f, 0.8f), new Vector3(0f, 0.5f, 12f));
            CreateBox(props.transform, "Wall Run Left", new Vector3(0.4f, 4f, 16f), new Vector3(-1.5f, 2f, 20f));
            CreateBox(props.transform, "Wall Run Right", new Vector3(0.4f, 4f, 16f), new Vector3(1.5f, 2f, 20f));
        }

        private static void CreateBox(Transform parent, string name, Vector3 size, Vector3 position)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            SetColor(box, new Color(0.3f, 0.33f, 0.4f));
        }

        private static GameObject BuildPlayerCamera()
        {
            GameObject cameraObject = new GameObject("PlayerCamera");

            CinemachineCamera virtualCamera = cameraObject.AddComponent<CinemachineCamera>();
            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = 40f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 5000f;
            virtualCamera.Lens = lens;

            CinemachineOrbitalFollow orbital = cameraObject.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius = 2.6f;
            orbital.TargetOffset = new Vector3(0.2f, 0f, 0f);
            orbital.HorizontalAxis.Wrap = true;
            orbital.HorizontalAxis.Recentering.Enabled = false;
            orbital.VerticalAxis.Range = new Vector2(-25f, 60f);
            orbital.VerticalAxis.Value = 10f;
            orbital.VerticalAxis.Wrap = false;
            orbital.VerticalAxis.Recentering.Enabled = false;

            cameraObject.AddComponent<CinemachineRotationComposer>();
            cameraObject.AddComponent<CameraShake>();
            return cameraObject;
        }

        private static GameObject BuildCharacter(GameObject playerCamera)
        {
            GameObject character = new GameObject("Henita");
            character.transform.position = Vector3.zero;
            character.transform.rotation = Quaternion.identity;

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject visualRoot = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visualRoot.name = VisualRootName;
            visualRoot.transform.SetParent(character.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            Animator animator = visualRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visualRoot.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            animator.applyRootMotion = false;

            CharacterController controller = character.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.skinWidth = 0.02f;

            GameObject followTarget = new GameObject("CameraFollowTarget");
            followTarget.transform.SetParent(character.transform, false);
            followTarget.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            List<Transform> breasts = FindBones(visualRoot.transform, BreastBoneNames);
            BreastCollisionSetup breastCollision = BuildBreastCollision(breasts);
            for (int i = 0; i < breasts.Count; i++)
            {
                AttachBreastRig(breasts[i], breastCollision);
            }

            CharacterBridge bridge = character.AddComponent<CharacterBridge>();
            SerializedObject bridgeObject = new SerializedObject(bridge);
            bridgeObject.FindProperty("controllerConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterControllerConfig>(ControllerConfigPath);
            bridgeObject.FindProperty("cameraConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CameraOrbitConfig>(CameraConfigPath);
            bridgeObject.FindProperty("inputAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            bridgeObject.FindProperty("orbitalFollow").objectReferenceValue = playerCamera.GetComponent<CinemachineOrbitalFollow>();
            bridgeObject.FindProperty("animator").objectReferenceValue = animator;
            bridgeObject.ApplyModifiedPropertiesWithoutUndo();

            character.AddComponent<SlowMotionToggle>();
            character.AddComponent<NaelstrofTeleportSettle>();
            character.AddComponent<NaelstrofShowcaseCamera>();

            Debug.Log("[NaelstrofSetup] Breast rigs: " + breasts.Count);
            return character;
        }

        private static void WireCameraTargets(GameObject playerCamera, GameObject character)
        {
            Transform followTarget = character.transform.Find("CameraFollowTarget");
            CinemachineCamera virtualCamera = playerCamera.GetComponent<CinemachineCamera>();
            virtualCamera.Follow = followTarget;
            virtualCamera.LookAt = followTarget;
            EditorUtility.SetDirty(virtualCamera);
        }

        private static List<Transform> FindBones(Transform root, string[] boneNames)
        {
            List<Transform> result = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                for (int n = 0; n < boneNames.Length; n++)
                {
                    if (all[i].name == boneNames[n])
                    {
                        result.Add(all[i]);
                        break;
                    }
                }
            }

            return result;
        }

        private static BreastCollisionSetup BuildBreastCollision(List<Transform> breasts)
        {
            if (breasts == null || breasts.Count == 0)
            {
                return null;
            }

            Transform parent = FindCommonAncestor(breasts);
            Vector3 center = Vector3.zero;
            Vector3 forward = Vector3.zero;
            float chainLength = 0f;
            float maxSpacing = 0f;

            for (int i = 0; i < breasts.Count; i++)
            {
                Transform bone = breasts[i];
                center += bone.position;

                if (bone.childCount > 0)
                {
                    Vector3 toTip = bone.GetChild(0).position - bone.position;
                    chainLength += toTip.magnitude;
                    forward += toTip.normalized;
                }

                for (int n = i + 1; n < breasts.Count; n++)
                {
                    maxSpacing = Mathf.Max(maxSpacing, Vector3.Distance(bone.position, breasts[n].position));
                }
            }

            center /= breasts.Count;
            chainLength = chainLength > 0f ? chainLength / breasts.Count : 0.09f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : parent.forward;

            Vector3 side = breasts.Count > 1 ? breasts[1].position - breasts[0].position : parent.right;
            side = Vector3.ProjectOnPlane(side, forward).normalized;
            if (side.sqrMagnitude < 0.0001f)
            {
                side = parent.right;
            }

            Vector3 up = Vector3.Cross(forward, side).normalized;
            if (up.sqrMagnitude < 0.0001f)
            {
                up = parent.up;
            }

            GameObject collisionObject = new GameObject("Breast Torso Collision");
            Transform collision = collisionObject.transform;
            collision.SetParent(parent, true);
            collision.SetPositionAndRotation(
                center - forward * (chainLength * 0.35f),
                Quaternion.LookRotation(forward, up));

            // The package stores personal colliders inside the rig data, so this
            // transform only supplies the animated torso-space pose of the shape.
            collision.localScale = Vector3.one;
            float radius = chainLength * 0.5f;
            float height = Mathf.Max(chainLength * 0.5f, maxSpacing);
            return new BreastCollisionSetup
            {
                Transform = collision,
                Radius = radius,
                Height = height
            };
        }

        private static Transform FindCommonAncestor(List<Transform> transforms)
        {
            Transform candidate = transforms[0].parent;
            while (candidate != null)
            {
                bool containsAll = true;
                for (int i = 1; i < transforms.Count; i++)
                {
                    if (!transforms[i].IsChildOf(candidate))
                    {
                        containsAll = false;
                        break;
                    }
                }

                if (containsAll)
                {
                    return candidate;
                }

                candidate = candidate.parent;
            }

            return transforms[0].root;
        }

        private static void AttachBreastRig(Transform bone, BreastCollisionSetup torsoCollision)
        {
            GameObject host = bone.gameObject;
            bool wasActive = host.activeSelf;
            host.SetActive(false);

            NaelstrofRig rig = host.AddComponent<NaelstrofRig>();

            SerializedObject rigObject = new SerializedObject(rig);
            SerializedProperty data = rigObject.FindProperty("jiggleRigData");
            data.FindPropertyRelative("hasSerializedData").boolValue = true;
            data.FindPropertyRelative("serializedVersion").stringValue = "v0.0.2";
            data.FindPropertyRelative("rootBone").objectReferenceValue = bone;
            data.FindPropertyRelative("excludeRoot").boolValue = false;
            data.FindPropertyRelative("excludedTransforms").ClearArray();
            SerializedProperty colliders = data.FindPropertyRelative("jiggleColliders");
            colliders.ClearArray();
            if (torsoCollision != null && torsoCollision.Transform != null)
            {
                colliders.arraySize = 1;
                SerializedProperty collider = colliders.GetArrayElementAtIndex(0);
                collider.FindPropertyRelative("transform").objectReferenceValue = torsoCollision.Transform;

                SerializedProperty shape = collider.FindPropertyRelative("collider");
                shape.FindPropertyRelative("type").enumValueIndex = (int)NaelstrofCollider.JiggleColliderType.Capsule;
                shape.FindPropertyRelative("capsuleAxis").enumValueIndex = (int)NaelstrofCollider.CapsuleAxis.X;

                shape.FindPropertyRelative("radius").floatValue = torsoCollision.Radius;
                shape.FindPropertyRelative("height").floatValue = torsoCollision.Height;
            }

            SerializedProperty parameters = data.FindPropertyRelative("jiggleTreeInputParameters");
            parameters.FindPropertyRelative("advancedToggle").boolValue = true;
            parameters.FindPropertyRelative("collisionToggle").boolValue = true;
            parameters.FindPropertyRelative("angleLimitToggle").boolValue = true;
            parameters.FindPropertyRelative("soften").floatValue = 0.85f;
            parameters.FindPropertyRelative("angleLimitSoften").floatValue = 0.35f;
            parameters.FindPropertyRelative("rootStretch").floatValue = 0f;
            parameters.FindPropertyRelative("ignoreRootMotion").floatValue = 0.462f;
            parameters.FindPropertyRelative("blend").floatValue = 1f;
            SetCurvedFloat(parameters.FindPropertyRelative("stiffness"), 0.75f);
            SetCurvedFloat(parameters.FindPropertyRelative("angleLimit"), 0.42f);
            SetCurvedFloat(parameters.FindPropertyRelative("stretch"), 0.15f);
            SetCurvedFloat(parameters.FindPropertyRelative("drag"), 0.1f);
            SetCurvedFloat(parameters.FindPropertyRelative("airDrag"), 0f);
            SetCurvedFloat(parameters.FindPropertyRelative("gravity"), 1f);
            SetCurvedFloat(parameters.FindPropertyRelative("collisionRadius"), 0.018f);

            rigObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);

            host.SetActive(wasActive);
        }

        private static void SetCurvedFloat(SerializedProperty property, float value)
        {
            property.FindPropertyRelative("value").floatValue = value;
            property.FindPropertyRelative("curveEnabled").boolValue = false;
            property.FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        }

        private static void InstantiateUpdater()
        {
            GameObject updaterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(UpdaterPrefabPath);
            GameObject updater = (GameObject)PrefabUtility.InstantiatePrefab(updaterAsset);
            updater.name = "JigglePhysicsUpdater";
            updater.transform.position = Vector3.zero;
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            renderer.sharedMaterial = material;
        }
    }
}
