using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class AnimatorLibrarySetup
    {
        private const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";

        private static readonly string[] ClipPaths =
        {
            "Assets/ThirdParty/QuaterniusUAL2/UAL2_Standard.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementBasic.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementAdvanced.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_CombatMelee.fbx",
            "Assets/Animation/Henita@Happy Idle.fbx",
            "Assets/Animation/Henita@Slow Run.fbx",
            "Assets/Animation/Henita@Fast Run.fbx",
            "Assets/Animation/Henita@Jump.fbx",
            "Assets/Animation/Henita@Zombie Punching.fbx",
            "Assets/Animation/Henita@Zombie Punching (1).fbx"
        };

        [MenuItem("Tools/Character/Use Best Animation Library")]
        public static void Apply()
        {
            Dictionary<string, AnimationClip> clips = LoadClips();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[AnimatorLibrary] Controller not found: " + ControllerPath);
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            AnimationClip idle = Get(clips, "Idle_No_Loop");
            AnimationClip walk = Get(clips, "Walking_A");
            AnimationClip run = Get(clips, "Running_A");
            if (locomotion != null && locomotion.motion is BlendTree blendTree && idle != null && walk != null && run != null)
            {
                blendTree.blendParameter = "Speed";
                blendTree.children = new ChildMotion[0];
                blendTree.AddChild(idle, 0f);
                blendTree.AddChild(walk, 0.4f);
                blendTree.AddChild(run, 1f);
            }

            SetMotion(stateMachine, "Jump", Get(clips, "Jump_Full_Short"), 1.0f);
            SetMotion(stateMachine, "Land", Get(clips, "Jump_Land"), 1.15f);
            SetMotion(stateMachine, "Vault", Get(clips, "ClimbUp_1m"), 1.1f);
            SetMotion(stateMachine, "Dash", Get(clips, "Sword_Dash"), 1.4f);
            SetMotion(stateMachine, "Slide", Get(clips, "Slide_Loop"), 1.2f);
            SetMotion(stateMachine, "Stumble", Get(clips, "Hit_Knockback"), 1.2f);
            SetMotion(stateMachine, "Attack", Get(clips, "Melee_Unarmed_Attack_Punch_A"), 1.1f);
            SetMotion(stateMachine, "Attack2", Get(clips, "Melee_Unarmed_Attack_Kick"), 1.1f);

            EnableIkPass(controller);
            SmoothTransitions(stateMachine);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[AnimatorLibrary] Locomotion, jump, landing, attacks and parkour states now use the third-party animation library.");
        }

        private static void EnableIkPass(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length == 0)
            {
                return;
            }

            layers[0].iKPass = true;
            controller.layers = layers;
        }

        private static void SmoothTransitions(AnimatorStateMachine stateMachine)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                child.state.writeDefaultValues = true;
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    if (transition.duration < 0.14f)
                    {
                        transition.duration = 0.14f;
                    }
                }
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.duration < 0.12f)
                {
                    transition.duration = 0.12f;
                }
            }

            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
            {
                SmoothTransitions(childMachine.stateMachine);
            }
        }

        private static void SetMotion(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, float speed)
        {
            if (clip == null)
            {
                return;
            }

            AnimatorState state = FindState(stateMachine, stateName);
            if (state == null)
            {
                return;
            }

            state.motion = clip;
            state.speed = speed;
            state.writeDefaultValues = true;
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

        private static AnimationClip Get(Dictionary<string, AnimationClip> clips, string name)
        {
            return clips.TryGetValue(name, out AnimationClip clip) ? clip : null;
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
            foreach (string path in ClipPaths)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        int separator = clip.name.LastIndexOf('|');
                        string shortName = separator >= 0 ? clip.name.Substring(separator + 1) : clip.name;
                        result[shortName] = clip;
                    }
                }
            }

            return result;
        }
    }
}
