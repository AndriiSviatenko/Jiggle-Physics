using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class AnimationSetupTool
    {
        private const string ModelPath = "Assets/Models/anime-girl-rigged-anime-model/source/Henita/Henita/Henita.fbx";
        private const string ControllerFolder = "Assets/Settings/Character";
        private const string ControllerPath = ControllerFolder + "/HenitaAnimator.controller";

        private static readonly Dictionary<string, string> BoneToHuman = new Dictionary<string, string>
        {
            { "Hips", "Hips" },
            { "Spine", "Spine" },
            { "Chest", "Chest" },
            { "Neck", "Neck" },
            { "Head", "Head" },
            { "Left shoulder", "LeftShoulder" },
            { "Left arm", "LeftUpperArm" },
            { "Left elbow", "LeftLowerArm" },
            { "Left wrist", "LeftHand" },
            { "Left leg", "LeftUpperLeg" },
            { "Left knee", "LeftLowerLeg" },
            { "Left ankle", "LeftFoot" },
            { "Left toe", "LeftToeBase" },
            { "Right shoulder", "RightShoulder" },
            { "Right arm", "RightUpperArm" },
            { "Right elbow", "RightLowerArm" },
            { "Right wrist", "RightHand" },
            { "Right leg", "RightUpperLeg" },
            { "Right knee", "RightLowerLeg" },
            { "Right ankle", "RightFoot" },
            { "Right toe", "RightToeBase" }
        };

        [MenuItem("Tools/Character/Setup Humanoid Avatar And Animator")]
        public static void Setup()
        {
            ConfigureHumanoid();
            AnimatorController controller = LoadOrCreateController();
            WireAnimator(controller);
            Debug.Log("[AnimationSetup] Humanoid avatar + animator controller ready.");
        }

        [MenuItem("Tools/Character/Retarget Clips As Humanoid (Own Avatars)")]
        public static void RetargetClipsHumanoid()
        {
            ConfigureHumanoidAtPath(ModelPath);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.IdleClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.WalkClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.RunClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.JumpClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.AttackClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.Attack2Clip);

            MoveAnimatorToModelRoot();

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                AnimationClipSetupTool.RebuildMotions(controller);
            }

            Debug.Log("[AnimationSetup] Clips retargeted as humanoid with own avatars.");
        }

        public static void ConfigureHumanoidAtPath(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[AnimationSetup] importer not found at " + path);
                return;
            }

            GameObject root = LoadRootAtPath(path);
            if (root == null)
            {
                Debug.LogError("[AnimationSetup] root not found at " + path);
                return;
            }

            Transform hips = FindHips(root.transform);
            if (hips == null)
            {
                Debug.LogError("[AnimationSetup] Hips not found under " + path);
                return;
            }

            HumanDescription description = new HumanDescription();
            List<SkeletonBone> skeleton = new List<SkeletonBone>();
            List<HumanBone> human = new List<HumanBone>();
            CollectSkeleton(hips, root.transform, skeleton, human);

            description.skeleton = skeleton.ToArray();
            description.human = human.ToArray();
            description.lowerArmTwist = 0.5f;
            description.upperArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }

        private static Transform FindHips(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "Hips")
                {
                    return all[i];
                }
            }

            return null;
        }

        private static GameObject LoadRootAtPath(string path)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i] as GameObject;
                if (candidate != null && candidate.transform.parent == null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void MoveAnimatorToModelRoot()
        {
            GameObject character = GameObject.Find("Henita");
            if (character == null)
            {
                return;
            }

            Animator existing = character.GetComponentInChildren<Animator>();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (existing != null && existing.transform != character.transform)
            {
                Object.DestroyImmediate(existing);
                existing = null;
            }

            if (existing == null)
            {
                existing = character.AddComponent<Animator>();
            }

            Avatar modelAvatar = LoadModelAvatar();
            if (modelAvatar != null)
            {
                existing.avatar = modelAvatar;
            }

            existing.runtimeAnimatorController = controller;
            existing.applyRootMotion = true;
            EditorUtility.SetDirty(existing);

            CharacterBridge bridge = character.GetComponent<CharacterBridge>();
            if (bridge != null)
            {
                SerializedObject bridgeObject = new SerializedObject(bridge);
                bridgeObject.FindProperty("animator").objectReferenceValue = existing;
                bridgeObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bridge);
            }
        }

        [MenuItem("Tools/Character/Setup Humanoid Retarget (Model + Clips Own Avatars)")]
        public static void SetupHumanoidRetarget()
        {
            ConfigureHumanoidAtPath(ModelPath);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.IdleClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.WalkClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.RunClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.JumpClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.AttackClip);
            ConfigureHumanoidAtPath(AnimationClipSetupTool.Attack2Clip);

            BakeRootInPlace(AnimationClipSetupTool.IdleClip);
            BakeRootInPlace(AnimationClipSetupTool.WalkClip);
            BakeRootInPlace(AnimationClipSetupTool.RunClip);
            BakeRootInPlace(AnimationClipSetupTool.JumpClip);
            BakeRootInPlace(AnimationClipSetupTool.AttackClip);
            BakeRootInPlace(AnimationClipSetupTool.Attack2Clip);

            MoveAnimatorToModelRoot();

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                AnimationClipSetupTool.RebuildMotions(controller);
            }

            Avatar modelAvatar = LoadModelAvatar();
            Avatar runAvatar = LoadAvatarAtPath(AnimationClipSetupTool.RunClip);
            Debug.Log($"[Retarget] modelAvatarValid={modelAvatar != null && modelAvatar.isValid} runClipAvatarValid={runAvatar != null && runAvatar.isValid}");
        }

        private static void BakeRootInPlace(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] templates = importer.clipAnimations;
            if (templates == null || templates.Length == 0)
            {
                templates = importer.defaultClipAnimations;
            }

            for (int i = 0; i < templates.Length; i++)
            {
                templates[i].keepOriginalPositionY = true;
                templates[i].keepOriginalPositionXZ = true;
                templates[i].keepOriginalOrientation = true;
                templates[i].lockRootHeightY = false;
                templates[i].lockRootPositionXZ = false;
                templates[i].lockRootRotation = false;
            }

            importer.clipAnimations = templates;
            importer.SaveAndReimport();
        }

        private static Avatar LoadAvatarAtPath(string path)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                Avatar avatar = all[i] as Avatar;
                if (avatar != null)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static Avatar LoadModelAvatar()
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            for (int i = 0; i < all.Length; i++)
            {
                Avatar avatar = all[i] as Avatar;
                if (avatar != null)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static void ConfigureHumanoid()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[AnimationSetup] Model importer not found at " + ModelPath);
                return;
            }

            GameObject root = LoadModelRoot();
            if (root == null)
            {
                Debug.LogError("[AnimationSetup] Could not load model root.");
                return;
            }

            Transform armature = root.transform.Find("Armature");
            Transform hips = armature != null ? armature.Find("Hips") : null;
            if (hips == null)
            {
                Debug.LogError("[AnimationSetup] Armature/Hips not found.");
                return;
            }

            HumanDescription description = new HumanDescription();
            List<SkeletonBone> skeleton = new List<SkeletonBone>();
            List<HumanBone> human = new List<HumanBone>();

            CollectSkeleton(hips, root.transform, skeleton, human);

            description.skeleton = skeleton.ToArray();
            description.human = human.ToArray();
            description.lowerArmTwist = 0.5f;
            description.upperArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();

            GameObject reimported = LoadModelRoot();
            Animator preview = reimported != null ? reimported.GetComponentInChildren<Animator>() : null;
            if (preview != null && preview.avatar != null)
            {
                Debug.Log($"[AnimationSetup] avatar valid={preview.avatar.isValid} human={preview.avatar.isHuman}");
            }
            else
            {
                Debug.LogWarning("[AnimationSetup] Avatar not produced; check humanoid mapping.");
            }
        }

        private static void CollectSkeleton(Transform bone, Transform avatarRoot, List<SkeletonBone> skeleton, List<HumanBone> human)
        {
            SkeletonBone skeletonBone = new SkeletonBone
            {
                name = bone.name,
                position = bone.localPosition,
                rotation = bone.localRotation,
                scale = bone.localScale
            };
            skeleton.Add(skeletonBone);

            string humanBone;
            if (BoneToHuman.TryGetValue(bone.name, out humanBone))
            {
                human.Add(new HumanBone
                {
                    boneName = bone.name,
                    humanName = humanBone,
                    limit = new HumanLimit()
                });
            }

            for (int i = 0; i < bone.childCount; i++)
            {
                CollectSkeleton(bone.GetChild(i), avatarRoot, skeleton, human);
            }
        }

        private static GameObject LoadModelRoot()
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i] as GameObject;
                if (candidate != null && candidate.transform.parent == null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static AnimatorController LoadOrCreateController()
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null)
            {
                return existing;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sit", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine layer = controller.layers[0].stateMachine;

            AnimatorState idle = layer.AddState("Idle");
            AnimatorState run = layer.AddState("Run");
            AnimatorState jump = layer.AddState("Jump");
            AnimatorState attack = layer.AddState("Attack");
            AnimatorState sit = layer.AddState("Sit");

            layer.defaultState = idle;

            AnimatorStateTransition idleToRun = idle.AddTransition(run);
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition runToIdle = run.AddTransition(idle);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition anyToJump = layer.AddAnyStateTransition(jump);
            anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            anyToJump.canTransitionToSelf = false;

            AnimatorStateTransition jumpToIdle = jump.AddTransition(idle);
            jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            jumpToIdle.hasExitTime = true;
            jumpToIdle.exitTime = 0.9f;

            AnimatorStateTransition anyToAttack = layer.AddAnyStateTransition(attack);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            anyToAttack.canTransitionToSelf = false;

            AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;

            AnimatorStateTransition idleToSit = idle.AddTransition(sit);
            idleToSit.AddCondition(AnimatorConditionMode.If, 0f, "Sit");

            AnimatorStateTransition sitToIdle = sit.AddTransition(idle);
            sitToIdle.AddCondition(AnimatorConditionMode.If, 0f, "Sit");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void WireAnimator(RuntimeAnimatorController controller)
        {
            GameObject character = GameObject.Find("Henita");
            if (character == null)
            {
                Debug.LogError("[AnimationSetup] Henita not found in scene.");
                return;
            }

            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = character.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);

            CharacterBridge bridge = character.GetComponent<CharacterBridge>();
            if (bridge != null)
            {
                SerializedObject bridgeObject = new SerializedObject(bridge);
                bridgeObject.FindProperty("animator").objectReferenceValue = animator;
                bridgeObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bridge);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);
        }
    }
}
