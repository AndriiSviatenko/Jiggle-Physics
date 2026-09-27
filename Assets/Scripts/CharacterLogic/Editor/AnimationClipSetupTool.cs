using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class AnimationClipSetupTool
    {
        private const string ModelPath = "Assets/Models/anime-girl-rigged-anime-model/source/Henita/Henita/Henita.fbx";
        private const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";

        public const string IdleClip = "Assets/Animation/Henita@Happy Idle.fbx";
        public const string WalkClip = "Assets/Animation/Henita@Slow Run.fbx";
        public const string RunClip = "Assets/Animation/Henita@Fast Run.fbx";
        public const string JumpClip = "Assets/Animation/Henita@Jump.fbx";
        public const string AttackClip = "Assets/Animation/Henita@Zombie Punching.fbx";
        public const string Attack2Clip = "Assets/Animation/Henita@Zombie Punching (1).fbx";

        private static readonly HashSet<string> LoopingClips = new HashSet<string>
        {
            "Happy Idle",
            "Slow Run",
            "Fast Run"
        };

        [MenuItem("Tools/Character/Import Clips And Build Locomotion Controller")]
        public static void Setup()
        {
            Avatar modelAvatar = LoadModelAvatar();
            if (modelAvatar == null)
            {
                Debug.LogError("[AnimationClipSetup] Henita avatar not found; run 'Setup Humanoid Avatar And Animator' first.");
                return;
            }

            RetargetClip(IdleClip, modelAvatar);
            RetargetClip(WalkClip, modelAvatar);
            RetargetClip(RunClip, modelAvatar);
            RetargetClip(JumpClip, modelAvatar);
            RetargetClip(AttackClip, modelAvatar);
            RetargetClip(Attack2Clip, modelAvatar);

            BuildController(
                LoadClip(IdleClip),
                LoadClip(WalkClip),
                LoadClip(RunClip),
                LoadClip(JumpClip),
                LoadClip(AttackClip),
                LoadClip(Attack2Clip));

            Debug.Log("[AnimationClipSetup] Clips retargeted and controller rebuilt.");
        }

        [MenuItem("Tools/Character/Use Generic Direct Playback")]
        public static void SetupGeneric()
        {
            RevertToGeneric(ModelPath);
            RevertToGeneric(IdleClip);
            RevertToGeneric(WalkClip);
            RevertToGeneric(RunClip);
            RevertToGeneric(JumpClip);
            RevertToGeneric(AttackClip);
            RevertToGeneric(Attack2Clip);

            BuildController(
                LoadClip(IdleClip),
                LoadClip(WalkClip),
                LoadClip(RunClip),
                LoadClip(JumpClip),
                LoadClip(AttackClip),
                LoadClip(Attack2Clip));

            Debug.Log("[AnimationClipSetup] Generic direct playback configured.");
        }

        public static void RebuildMotions(AnimatorController controller)
        {
            BuildController(
                LoadClip(IdleClip),
                LoadClip(WalkClip),
                LoadClip(RunClip),
                LoadClip(JumpClip),
                LoadClip(AttackClip),
                LoadClip(Attack2Clip));
        }

        public static void RevertToGeneric(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;

            ModelImporterClipAnimation[] templates = importer.defaultClipAnimations;
            for (int i = 0; i < templates.Length; i++)
            {
                bool looping = LoopingClips.Contains(templates[i].name);
                templates[i].loopTime = looping;
                templates[i].loopPose = looping;
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

        private static void RetargetClip(string path, Avatar modelAvatar)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[AnimationClipSetup] Missing clip at " + path);
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = modelAvatar;

            ModelImporterClipAnimation[] templates = importer.defaultClipAnimations;
            for (int i = 0; i < templates.Length; i++)
            {
                templates[i].loopTime = LoopingClips.Contains(templates[i].name);
                templates[i].loopPose = LoopingClips.Contains(templates[i].name);
            }

            importer.clipAnimations = templates;
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadClip(string path)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < all.Length; i++)
            {
                AnimationClip clip = all[i] as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void BuildController(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip jump,
            AnimationClip attack,
            AnimationClip attack2)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[AnimationClipSetup] Controller not found at " + ControllerPath);
                return;
            }

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack2", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine layer = controller.layers[0].stateMachine;
            ClearStates(layer);

            AnimatorState locomotion = layer.AddState("Locomotion");
            locomotion.motion = BuildLocomotionBlend(layer, idle, walk, run);
            layer.defaultState = locomotion;

            AnimatorState jumpState = layer.AddState("Jump");
            jumpState.motion = jump;

            AnimatorState attackState = layer.AddState("Attack");
            attackState.motion = attack;

            AnimatorState attack2State = layer.AddState("Attack2");
            attack2State.motion = attack2;

            AnimatorStateTransition locoToJump = locomotion.AddTransition(jumpState);
            locoToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            locoToJump.hasExitTime = false;
            locoToJump.duration = 0.1f;

            AnimatorStateTransition jumpToLoco = jumpState.AddTransition(locomotion);
            jumpToLoco.hasExitTime = false;
            jumpToLoco.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            jumpToLoco.duration = 0.15f;

            AnimatorStateTransition locoToAttack = locomotion.AddTransition(attackState);
            locoToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            locoToAttack.hasExitTime = false;
            locoToAttack.duration = 0.1f;

            AnimatorStateTransition attackToLoco = attackState.AddTransition(locomotion);
            attackToLoco.hasExitTime = true;
            attackToLoco.exitTime = 0.9f;

            AnimatorStateTransition locoToAttack2 = locomotion.AddTransition(attack2State);
            locoToAttack2.AddCondition(AnimatorConditionMode.If, 0f, "Attack2");
            locoToAttack2.hasExitTime = false;
            locoToAttack2.duration = 0.1f;

            AnimatorStateTransition attack2ToLoco = attack2State.AddTransition(locomotion);
            attack2ToLoco.hasExitTime = true;
            attack2ToLoco.exitTime = 0.45f;
            attack2ToLoco.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static BlendTree BuildLocomotionBlend(AnimatorStateMachine layer, AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            BlendTree tree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(tree, layer);

            if (idle != null)
            {
                tree.AddChild(idle, 0f);
            }

            if (walk != null)
            {
                tree.AddChild(walk, 0.5f);
            }

            if (run != null)
            {
                tree.AddChild(run, 1f);
            }

            return tree;
        }

        private static void ClearStates(AnimatorStateMachine layer)
        {
            AnimatorState[] existing = layer.states != null
                ? System.Array.ConvertAll(layer.states, entry => entry.state)
                : new AnimatorState[0];

            for (int i = 0; i < existing.Length; i++)
            {
                layer.RemoveState(existing[i]);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters != null)
            {
                for (int i = 0; i < controller.parameters.Length; i++)
                {
                    if (controller.parameters[i].name == name)
                    {
                        return;
                    }
                }
            }

            controller.AddParameter(name, type);
        }
    }
}
