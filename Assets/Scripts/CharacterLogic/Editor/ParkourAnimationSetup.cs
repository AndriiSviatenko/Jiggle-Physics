using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CharacterLogic.Editor
{
    public static class ParkourAnimationSetup
    {
        private static readonly string[] ThirdPartyAnimationPaths =
        {
            "Assets/ThirdParty/QuaterniusUAL2/UAL2_Standard.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_CombatMelee.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementAdvanced.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementBasic.fbx"
        };

        private static readonly string[] HenitaAnimationPaths =
        {
            "Assets/Animation/Henita@Happy Idle.fbx",
            "Assets/Animation/Henita@Slow Run.fbx",
            "Assets/Animation/Henita@Fast Run.fbx",
            "Assets/Animation/Henita@Jump.fbx",
            "Assets/Animation/Henita@Zombie Punching.fbx",
            "Assets/Animation/Henita@Zombie Punching (1).fbx"
        };

        private const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";
        private const string VisualRootName = "Visual Animation Root";

        public static void Configure()
        {
            ConfigureThirdPartyImporters();
            ConfigureHenitaImporters();

            Dictionary<string, AnimationClip> clips = LoadClips();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new MissingReferenceException("Animator controller not found: " + ControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            AnimatorState jump = FindState(stateMachine, "Jump");
            if (locomotion == null || jump == null)
            {
                throw new MissingReferenceException("Expected Locomotion and Jump states in " + ControllerPath);
            }

            if (locomotion.motion is BlendTree blendTree)
            {
                blendTree.blendParameter = "Speed";
                blendTree.children = new ChildMotion[0];
                blendTree.AddChild(RequireClip(clips, "Happy Idle"), 0f);
                blendTree.AddChild(RequireClip(clips, "Slow Run"), 0.5f);
                blendTree.AddChild(RequireClip(clips, "Fast Run"), 1f);
            }

            jump.motion = RequireClip(clips, "Jump");
            jump.speed = 1.15f;

            AnimatorState attack = FindState(stateMachine, "Attack");
            AnimatorState attack2 = FindState(stateMachine, "Attack2");
            if (attack != null)
            {
                attack.motion = RequireClip(clips, "Zombie Punching");
                attack.speed = 1.25f;
            }
            if (attack2 != null)
            {
                attack2.motion = RequireClip(clips, "Melee_Unarmed_Attack_Kick");
                attack2.speed = 1.18f;
            }

            EnsureParameter(controller, "Sliding", AnimatorControllerParameterType.Bool);
            ConfigureOneShot(controller, stateMachine, locomotion, "Vault", "Vault", RequireClip(clips, "ClimbUp_1m"), new Vector3(470f, -80f), 1.08f, 0.86f);
            ConfigureOneShot(controller, stateMachine, locomotion, "Land", "Land", RequireClip(clips, "Jump_Land"), new Vector3(470f, 0f), 1.22f, 0.72f);
            ConfigureOneShot(controller, stateMachine, locomotion, "Dash", "Dash", RequireClip(clips, "Sword_Dash"), new Vector3(470f, 80f), 1.55f, 0.68f);
            ConfigureOneShot(controller, stateMachine, locomotion, "Slide", "Slide", RequireClip(clips, "Slide_Loop"), new Vector3(470f, 160f), 1.25f, 0.80f);
            ConfigureOneShot(controller, stateMachine, locomotion, "Stumble", "Stumble", RequireClip(clips, "Hit_Knockback"), new Vector3(470f, 240f), 1.35f, 0.62f);
            ConfigureWallRun(controller, stateMachine, locomotion, RequireClip(clips, "Running_Strafe_Left"), RequireClip(clips, "Running_Strafe_Right"));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            FixSceneAnimators(controller);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Parkour] Humanoid avatar, locomotion blend, attacks, jump, vault, slide, stumble, landing, blink and wall-run states configured.");
        }

        private static void ConfigureThirdPartyImporters()
        {
            foreach (string animationPath in ThirdPartyAnimationPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(animationPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
                for (int i = 0; i < takes.Length; i++)
                {
                    string clipName = takes[i].name;
                    bool loop = clipName.EndsWith("_Loop") || clipName.StartsWith("Running_") ||
                                clipName.StartsWith("Walking_") || clipName == "Jump_Idle" ||
                                clipName == "Crawling" || clipName == "Crouching" || clipName == "Sneaking" ||
                                clipName.Contains("Slide_Loop");
                    takes[i].loopTime = loop;
                    takes[i].loopPose = loop;
                    takes[i].lockRootRotation = true;
                    takes[i].lockRootHeightY = true;
                    takes[i].lockRootPositionXZ = true;
                }

                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureHenitaImporters()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Models/Henita.fbx");
            if (avatar == null)
            {
                return;
            }

            foreach (string path in HenitaAnimationPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.bakeAxisConversion = true;

                ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
                for (int i = 0; i < takes.Length; i++)
                {
                    string name = takes[i].name;
                    bool loop = name.Contains("Idle") || name.Contains("Run");
                    takes[i].loopTime = loop;
                    takes[i].loopPose = loop;
                    takes[i].lockRootRotation = true;
                    takes[i].lockRootHeightY = true;
                    takes[i].lockRootPositionXZ = true;
                }
                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();

            List<string> allPaths = new List<string>(ThirdPartyAnimationPaths);
            allPaths.AddRange(HenitaAnimationPaths);

            foreach (string animationPath in allPaths)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(animationPath))
                {
                    if (asset is not AnimationClip clip || clip.name.StartsWith("__preview__"))
                    {
                        continue;
                    }

                    int separator = clip.name.LastIndexOf('|');
                    string shortName = separator >= 0 ? clip.name.Substring(separator + 1) : clip.name;
                    result[shortName] = clip;
                }
            }

            return result;
        }

        private static AnimationClip RequireClip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (!clips.TryGetValue(name, out AnimationClip clip))
            {
                throw new MissingReferenceException("Required animation clip not found: " + name);
            }

            return clip;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }

        private static void ConfigureOneShot(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            AnimatorState locomotion,
            string stateName,
            string triggerName,
            AnimationClip clip,
            Vector3 position,
            float speed,
            float exitTime)
        {
            EnsureTrigger(controller, triggerName);
            AnimatorState state = FindState(stateMachine, stateName) ?? stateMachine.AddState(stateName, position);
            state.motion = clip;
            state.speed = speed;
            state.writeDefaultValues = true;

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == state)
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
            }

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.duration = 0.06f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                state.RemoveTransition(transition);
            }

            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = exitTime;
            exit.duration = 0.12f;
        }

        private static void EnsureTrigger(AnimatorController controller, string name)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static void ConfigureWallRun(
            AnimatorController controller,
            AnimatorStateMachine stateMachine,
            AnimatorState locomotion,
            AnimationClip leftClip,
            AnimationClip rightClip)
        {
            EnsureParameter(controller, "WallRunning", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "WallSide", AnimatorControllerParameterType.Float);
            ConfigureWallSide(stateMachine, locomotion, "Wall Run Left", leftClip, new Vector3(660f, -50f), false, AnimatorConditionMode.Less);
            ConfigureWallSide(stateMachine, locomotion, "Wall Run Right", rightClip, new Vector3(660f, 50f), false, AnimatorConditionMode.Greater);
        }

        private static void ConfigureWallSide(
            AnimatorStateMachine stateMachine,
            AnimatorState locomotion,
            string stateName,
            AnimationClip clip,
            Vector3 position,
            bool mirror,
            AnimatorConditionMode sideCondition)
        {
            AnimatorState state = FindState(stateMachine, stateName) ?? stateMachine.AddState(stateName, position);
            state.motion = clip;
            state.speed = 0.82f;
            state.mirror = mirror;
            state.writeDefaultValues = true;

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == state)
                {
                    stateMachine.RemoveAnyStateTransition(transition);
                }
            }

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "WallRunning");
            enter.AddCondition(sideCondition, 0f, "WallSide");
            enter.duration = 0.09f;
            enter.hasExitTime = false;
            enter.canTransitionToSelf = false;

            foreach (AnimatorStateTransition transition in state.transitions)
            {
                state.RemoveTransition(transition);
            }

            AnimatorStateTransition exit = state.AddTransition(locomotion);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "WallRunning");
            exit.hasExitTime = false;
            exit.duration = 0.1f;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static void FixSceneAnimators(RuntimeAnimatorController controller)
        {
            string[] scenePaths =
            {
                "Assets/Scenes/ParkourCity.unity",
                "Assets/Scenes/SkyTemple.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            foreach (string scenePath in scenePaths)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
                if (bridge == null)
                {
                    continue;
                }

                Animator avatarSource = null;
                foreach (Animator candidate in bridge.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.avatar != null && candidate.avatar.isValid && candidate.avatar.isHuman)
                    {
                        avatarSource = candidate;
                        if (candidate.transform == bridge.transform)
                        {
                            break;
                        }
                    }
                }

                if (avatarSource == null)
                {
                    throw new MissingReferenceException("A humanoid Avatar source is missing in " + scenePath);
                }

                Transform visualRoot = bridge.transform.Find(VisualRootName);
                if (visualRoot == null)
                {
                    GameObject visualRootObject = new GameObject(VisualRootName);
                    visualRoot = visualRootObject.transform;
                    visualRoot.SetParent(bridge.transform, false);
                }

                visualRoot.localPosition = new Vector3(0f, 0.90f, 0f);
                visualRoot.localRotation = Quaternion.identity;
                visualRoot.localScale = Vector3.one;

                Transform armature = bridge.transform.Find("Armature");
                if (armature != null)
                {
                    armature.SetParent(visualRoot, false);
                }

                Transform body = bridge.transform.Find("Body");
                if (body != null)
                {
                    body.SetParent(visualRoot, false);
                }

                Animator visualAnimator = visualRoot.GetComponent<Animator>();
                if (visualAnimator == null)
                {
                    visualAnimator = visualRoot.gameObject.AddComponent<Animator>();
                }

                foreach (Animator candidate in bridge.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate == visualAnimator)
                    {
                        continue;
                    }

                    candidate.runtimeAnimatorController = null;
                    candidate.enabled = false;
                    EditorUtility.SetDirty(candidate);
                }

                visualAnimator.avatar = avatarSource.avatar;
                visualAnimator.runtimeAnimatorController = controller;
                visualAnimator.applyRootMotion = false;
                visualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visualAnimator.enabled = true;
                EditorUtility.SetDirty(visualAnimator);

                SerializedObject bridgeObject = new SerializedObject(bridge);
                bridgeObject.FindProperty("animator").objectReferenceValue = visualAnimator;
                bridgeObject.ApplyModifiedPropertiesWithoutUndo();

                var jiggleRig = bridge.GetComponent<JigglePhysics.JiggleRig>();
                if (jiggleRig != null)
                {
                    Transform breastL = null, endL = null, breastR = null, endR = null;
                    foreach (var t in bridge.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Breast_L") breastL = t;
                        else if (t.name == "J_Sec_L_Bust2") endL = t;
                        else if (t.name == "Breast_R") breastR = t;
                        else if (t.name == "J_Sec_R_Bust2") endR = t;
                    }

                    var breastProfile = AssetDatabase.LoadAssetAtPath<JigglePhysics.JiggleProfile>("Assets/Settings/JigglePhysics/BreastProfile.asset");
                    var torsoCollider = bridge.GetComponentInChildren<JigglePhysics.JiggleSphereCollider>(true);

                    SerializedObject rigObject = new SerializedObject(jiggleRig);
                    rigObject.FindProperty("defaultProfile").objectReferenceValue = breastProfile;

                    var collidersProp = rigObject.FindProperty("colliderBehaviours");
                    collidersProp.arraySize = torsoCollider != null ? 1 : 0;
                    if (torsoCollider != null)
                    {
                        collidersProp.GetArrayElementAtIndex(0).objectReferenceValue = torsoCollider;
                    }

                    var chainsProp = rigObject.FindProperty("chains");
                    chainsProp.arraySize = 2;

                    var elem0 = chainsProp.GetArrayElementAtIndex(0);
                    elem0.FindPropertyRelative("RootBone").objectReferenceValue = breastL;
                    elem0.FindPropertyRelative("EndBone").objectReferenceValue = endL;
                    elem0.FindPropertyRelative("ProfileOverride").objectReferenceValue = breastProfile;

                    var elem1 = chainsProp.GetArrayElementAtIndex(1);
                    elem1.FindPropertyRelative("RootBone").objectReferenceValue = breastR;
                    elem1.FindPropertyRelative("EndBone").objectReferenceValue = endR;
                    elem1.FindPropertyRelative("ProfileOverride").objectReferenceValue = breastProfile;

                    rigObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(jiggleRig);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
