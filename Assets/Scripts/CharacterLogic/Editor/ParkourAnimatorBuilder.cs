using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.Editor
{
    public static class ParkourAnimatorBuilder
    {
        public const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";

        private static readonly string[] ClipSources =
        {
            "Assets/ThirdParty/QuaterniusUAL2/UAL2_Standard.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementBasic.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_MovementAdvanced.fbx",
            "Assets/ThirdParty/KayKitAnimations/Rig_Medium_CombatMelee.fbx",
            "Assets/Animation/Henita@Happy Idle.fbx",
            "Assets/Animation/Henita@Jump.fbx",
            "Assets/Animation/Henita@Zombie Punching.fbx",
            "Assets/Animation/Henita@Zombie Punching (1).fbx",
            "Assets/Settings/Character/Retargeted/Henita_Walk_Athletic.anim",
            "Assets/Settings/Character/Retargeted/Henita_Jog_Athletic.anim",
            "Assets/Settings/Character/Retargeted/Henita_Run_Athletic.anim",
            "Assets/Settings/Character/HenitaFlip.anim",
            "Assets/Settings/Character/HenitaRoll.anim",
            "Assets/Settings/Character/HenitaWallJump.anim"
        };

        private static readonly string[] LoopingClips =
        {
            "Happy Idle", "Henita_Walk_Athletic", "Henita_Jog_Athletic", "Henita_Run_Athletic", "Jump_Idle", "NinjaJump_Idle_Loop", "Slide_Loop",
            "Crouching", "Sneaking"
        };

        [MenuItem("Tools/Parkour/Rebuild Animator Controller")]
        public static void Build()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[ParkourAnimator] Controller not found: " + ControllerPath);
                return;
            }

            EnsureLooping();
            Dictionary<string, AnimationClip> clips = LoadClips();
            if (!HasAll(clips))
            {
                return;
            }

            AvatarMask upperBodyMask = EnsureUpperBodyMask();
            ResetParameters(controller);
            AnimatorStateMachine machine = ResetLayer(controller);

            AnimatorState locomotion = CreateLocomotion(controller, machine, clips);
            machine.defaultState = locomotion;

            AnimatorState crouch = CreateCrouch(controller, machine, clips);

            AnimatorState jumpStart = AddState(machine, ParkourAnimatorIds.StateJumpStart, clips["Jump"], 1.6f, new Vector3(440f, 90f));

            AnimatorStateMachine traversal = BuildTraversal(machine, locomotion, clips, out AnimatorState wallRun, out AnimatorState wallJump);
            AnimatorStateMachine airborne = BuildAirborne(machine, locomotion, crouch, clips, out AnimatorState falling);
            AnimatorStateMachine slide = BuildSlide(machine, locomotion, crouch, jumpStart, airborne, clips, out AnimatorState slideExit);

            AnimatorState vault = AddState(machine, ParkourAnimatorIds.StateVault, clips["ClimbUp_1m"], 1f, new Vector3(860f, 120f));
            vault.speedParameterActive = true;
            vault.speedParameter = ParkourAnimatorIds.ActionSpeed;

            AnimatorState dash = AddState(machine, ParkourAnimatorIds.StateDash, clips["Dodge_Forward"], 1f, new Vector3(860f, 240f));
            AnimatorState flip = AddState(machine, ParkourAnimatorIds.StateFlip, clips["HenitaFlip"], 2.2f, new Vector3(860f, 360f));
            AnimatorState stumble = AddState(machine, ParkourAnimatorIds.StateStumble, clips["Hit_Knockback"], 1.6f, new Vector3(860f, 480f));

            LinkTopLevel(locomotion, crouch, jumpStart, airborne, traversal, slide, vault, dash, flip, stumble);
            LinkCrossings(airborne, traversal, falling, wallRun, wallJump, crouch);

            BuildUpperBodyLayer(controller, upperBodyMask, clips, locomotion);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
            Debug.Log("[ParkourAnimator] Rebuilt " + ControllerPath + " — layers=" + controller.layers.Length + " baseStates=" + CountStates(baseMachine) + " baseTransitions=" + CountTransitions(baseMachine) + " upperStates=" + CountStates(controller.layers[1].stateMachine));
        }

        private static void LinkTopLevel(AnimatorState locomotion, AnimatorState crouch, AnimatorState jumpStart, AnimatorStateMachine airborne, AnimatorStateMachine traversal, AnimatorStateMachine slide, AnimatorState vault, AnimatorState dash, AnimatorState flip, AnimatorState stumble)
        {
            Responsive(locomotion, jumpStart, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Jump);
            Responsive(crouch, jumpStart, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Jump);

            AnimatorStateTransition walkOff = Responsive(locomotion, airborne, 0.06f);
            walkOff.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
            walkOff.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Falling);

            AnimatorStateTransition crouchFall = Responsive(crouch, airborne, 0.06f);
            crouchFall.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
            crouchFall.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Falling);

            ExitTransition(jumpStart, airborne, 0.40f, 0.10f);

            // Locomotion <-> Crouch
            Responsive(locomotion, crouch, 0.12f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Crouching);
            Responsive(crouch, locomotion, 0.14f).AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Crouching);

            // Crouch -> Slide
            Responsive(crouch, slide, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Slide);

            // Locomotion -> Traversal & Slide
            Responsive(locomotion, traversal, 0.10f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.WallRunning);
            Responsive(locomotion, slide, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Slide);

            // Action recovery
            ActionRecovery(vault, locomotion, airborne, 0.94f, 0.12f);
            ActionRecovery(dash, locomotion, airborne, 0.80f, 0.14f);
            ActionRecovery(flip, locomotion, airborne, 0.90f, 0.12f);
            ActionRecovery(stumble, locomotion, airborne, 0.85f, 0.18f);
        }

        private static void LinkCrossings(AnimatorStateMachine airborne, AnimatorStateMachine traversal, AnimatorState falling, AnimatorState wallRun, AnimatorState wallJump, AnimatorState crouch)
        {
            Responsive(falling, traversal, 0.10f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.WallRunning);
            Responsive(falling, wallJump, 0.05f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.WallJump);

            AnimatorStateTransition fallToAir = Responsive(wallRun, airborne, 0.15f);
            fallToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.WallRunning);
            fallToAir.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);

            // Wall Jump -> Airborne must have exit time (not Responsive on !Grounded) because the character is airborne during wall jump
            ExitTransition(wallJump, airborne, 0.70f, 0.15f);

            // Airborne -> Crouch landing if crouch is held
            AnimatorStateTransition fallToCrouch = Responsive(falling, crouch, 0.10f);
            fallToCrouch.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            fallToCrouch.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Crouching);
        }

        private static void ActionRecovery(AnimatorState action, AnimatorState grounded, AnimatorStateMachine airborne, float exitTime, float duration)
        {
            ExitTransition(action, grounded, exitTime, duration).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            ExitTransition(action, airborne, exitTime, duration).AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
        }

        private static AnimatorStateMachine BuildAirborne(AnimatorStateMachine owner, AnimatorState locomotion, AnimatorState crouch, Dictionary<string, AnimationClip> clips, out AnimatorState falling)
        {
            AnimatorStateMachine machine = AddSubMachine(owner, ParkourAnimatorIds.SubAirborne, new Vector3(200f, -200f));

            falling = AddState(machine, ParkourAnimatorIds.StateAirborne, clips["Jump"], 0.15f, new Vector3(320f, 0f));
            falling.cycleOffset = 0.45f;
            AnimatorState land = AddState(machine, ParkourAnimatorIds.StateLand, clips["Jump"], 1f, new Vector3(640f, 0f));
            land.cycleOffset = 0.72f;
            AnimatorState roll = AddState(machine, ParkourAnimatorIds.StateRoll, clips["HenitaRoll"], 2.2f, new Vector3(640f, 150f));

            machine.defaultState = falling;
            AddEntry(machine, falling, null);

            Responsive(falling, land, 0.06f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Land);
            Responsive(falling, roll, 0.06f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.HardLand);

            // Normal landing to Locomotion (if not crouching)
            AnimatorStateTransition fallToLoco = Responsive(falling, locomotion, 0.12f);
            fallToLoco.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            fallToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Crouching);

            // Land recovery
            AnimatorStateTransition landToLoco = ExitTransition(land, locomotion, 0.62f, 0.18f);
            landToLoco.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            landToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Crouching);

            AnimatorStateTransition landToCrouch = ExitTransition(land, crouch, 0.62f, 0.18f);
            landToCrouch.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            landToCrouch.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Crouching);

            // Roll recovery
            ExitTransition(roll, locomotion, 0.85f, 0.14f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            ExitTransition(roll, locomotion, 0.65f, 0.14f).AddCondition(AnimatorConditionMode.Greater, 0.4f, ParkourAnimatorIds.Speed);
            ExitTransition(roll, falling, 0.85f, 0.14f).AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);

            return machine;
        }

        private static AnimatorStateMachine BuildTraversal(AnimatorStateMachine owner, AnimatorState locomotion, Dictionary<string, AnimationClip> clips, out AnimatorState wallRun, out AnimatorState wallJump)
        {
            AnimatorStateMachine machine = AddSubMachine(owner, ParkourAnimatorIds.SubTraversal, new Vector3(520f, -200f));

            wallRun = AddState(machine, ParkourAnimatorIds.StateWallRun, clips["Henita_Run_Athletic"], 1.12f, new Vector3(0f, 0f));
            wallJump = AddState(machine, ParkourAnimatorIds.StateWallJump, clips["HenitaWallJump"], 2.2f, new Vector3(320f, 0f));

            machine.defaultState = wallRun;
            AddEntry(machine, wallRun, ParkourAnimatorIds.WallRunning);
            AddEntry(machine, wallJump, ParkourAnimatorIds.WallJump);

            AnimatorStateTransition wallRunExit = Responsive(wallRun, locomotion, 0.15f);
            wallRunExit.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.WallRunning);
            wallRunExit.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);
            Responsive(wallJump, locomotion, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Grounded);

            return machine;
        }

        private static AnimatorStateMachine BuildSlide(AnimatorStateMachine owner, AnimatorState locomotion, AnimatorState crouch, AnimatorState jumpStart, AnimatorStateMachine airborne, Dictionary<string, AnimationClip> clips, out AnimatorState slideExit)
        {
            AnimatorStateMachine machine = AddSubMachine(owner, ParkourAnimatorIds.SubSlide, new Vector3(200f, 260f));

            AnimatorState start = AddState(machine, ParkourAnimatorIds.StateSlideStart, clips["Slide_Start"], 1.6f, new Vector3(0f, 0f));
            AnimatorState loop = AddState(machine, ParkourAnimatorIds.StateSlideLoop, clips["Slide_Loop"], 1f, new Vector3(320f, 0f));
            slideExit = AddState(machine, ParkourAnimatorIds.StateSlideExit, clips["Slide_Exit"], 1.4f, new Vector3(640f, 0f));

            machine.defaultState = start;
            AddEntry(machine, start, null);

            ExitTransition(start, loop, 0.85f, 0.12f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Sliding);
            Responsive(start, slideExit, 0.10f).AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Sliding);
            Responsive(loop, slideExit, 0.10f).AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Sliding);

            // Slide exit to Locomotion or Crouch depending on Crouching parameter
            AnimatorStateTransition exitToLoco = ExitTransition(slideExit, locomotion, 0.75f, 0.15f);
            exitToLoco.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Crouching);

            AnimatorStateTransition exitToCrouch = ExitTransition(slideExit, crouch, 0.75f, 0.15f);
            exitToCrouch.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Crouching);

            // Interrupt to Jump Start on Jump trigger or to Airborne when falling off a ledge
            Responsive(start, jumpStart, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Jump);
            AnimatorStateTransition startFall = Responsive(start, airborne, 0.08f);
            startFall.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
            startFall.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Falling);

            Responsive(loop, jumpStart, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Jump);
            AnimatorStateTransition loopFall = Responsive(loop, airborne, 0.08f);
            loopFall.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
            loopFall.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Falling);

            Responsive(slideExit, jumpStart, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Jump);
            AnimatorStateTransition exitFall = Responsive(slideExit, airborne, 0.08f);
            exitFall.AddCondition(AnimatorConditionMode.IfNot, 0f, ParkourAnimatorIds.Grounded);
            exitFall.AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Falling);

            return machine;
        }

        private static AnimatorStateMachine BuildUpperBodyLayer(AnimatorController controller, AvatarMask mask, Dictionary<string, AnimationClip> clips, AnimatorState locomotion)
        {
            AnimatorStateMachine machine = new AnimatorStateMachine { name = ParkourAnimatorIds.LayerUpperBody };
            AssetDatabase.AddObjectToAsset(machine, controller);

            // Default state has NO motion (motion = null) and writeDefaultValues = false.
            // UpperBody layer defaultWeight is 0f: per AnimationGuide, Layer 1 weight is dynamically
            // crossfaded to 1f only when an upper-body action (e.g. Attack) is active, eliminating bind-pose freeze.
            AnimatorState idle = AddState(machine, ParkourAnimatorIds.StateUpperIdle, locomotion.motion, 1f, new Vector3(0f, 0f));
            idle.speedParameterActive = true;
            idle.speedParameter = ParkourAnimatorIds.LocomotionCadence;

            AnimatorState attack = AddState(machine, ParkourAnimatorIds.StateAttack, clips["Zombie Punching"], 1.3f, new Vector3(320f, 0f));
            AnimatorState attack2 = AddState(machine, ParkourAnimatorIds.StateAttack2, clips["Zombie Punching (1)"], 2.5f, new Vector3(320f, 150f));

            machine.defaultState = idle;
            Responsive(idle, attack, 0.06f).AddCondition(AnimatorConditionMode.If, 0f, ParkourAnimatorIds.Attack);
            Responsive(idle, attack2, 0.06f).If(ParkourAnimatorIds.Attack2);
            Responsive(attack, attack2, 0.06f).If(ParkourAnimatorIds.Attack2);
            ExitTransition(attack, idle, 0.82f, 0.15f);
            ExitTransition(attack2, idle, 0.82f, 0.15f);

            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = ParkourAnimatorIds.LayerUpperBody,
                stateMachine = machine,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
                iKPass = false
            };
            controller.AddLayer(layer);
            return machine;
        }

        private static AvatarMask EnsureUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ParkourAnimatorIds.UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "UpperBody" };
                AssetDatabase.CreateAsset(mask, ParkourAnimatorIds.UpperBodyMaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, true);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssets();
            return mask;
        }

        private static AnimatorState CreateLocomotion(AnimatorController controller, AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
        {
            BlendTree tree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = ParkourAnimatorIds.Speed,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(clips["Happy Idle"], 0f);
            tree.AddChild(clips["Henita_Walk_Athletic"], 0.37f);
            tree.AddChild(clips["Henita_Jog_Athletic"], 0.68f);
            tree.AddChild(clips["Henita_Run_Athletic"], 1f);

            AnimatorState state = machine.AddState(ParkourAnimatorIds.StateLocomotion, new Vector3(220f, 0f));
            state.motion = tree;
            state.writeDefaultValues = true;
            state.speedParameterActive = true;
            state.speedParameter = ParkourAnimatorIds.LocomotionCadence;
            return state;
        }

        private static AnimatorState CreateCrouch(AnimatorController controller, AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
        {
            BlendTree tree = new BlendTree
            {
                name = "CrouchBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = ParkourAnimatorIds.Speed,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(clips["Crouching"], 0f);
            tree.AddChild(clips["Sneaking"], 0.30f);

            AnimatorState state = machine.AddState(ParkourAnimatorIds.StateCrouch, new Vector3(220f, 130f));
            state.motion = tree;
            state.writeDefaultValues = true;
            state.speedParameterActive = true;
            state.speedParameter = ParkourAnimatorIds.LocomotionCadence;
            return state;
        }

        private static AnimatorState AddState(AnimatorStateMachine machine, string stateName, Motion motion, float speed, Vector3 position)
        {
            AnimatorState state = machine.AddState(stateName, position);
            state.motion = motion;
            state.speed = speed;
            state.writeDefaultValues = motion != null;
            return state;
        }

        private static AnimatorStateMachine AddSubMachine(AnimatorStateMachine owner, string name, Vector3 position)
        {
            return owner.AddStateMachine(name, position);
        }

        private static void AddEntry(AnimatorStateMachine machine, AnimatorState destination, string condition)
        {
            machine.AddEntryTransition(destination);
            AnimatorTransition[] entries = machine.entryTransitions;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].destinationState == destination && entries[i].conditions.Length == 0)
                {
                    if (condition != null)
                    {
                        entries[i].AddCondition(AnimatorConditionMode.If, 0f, condition);
                    }

                    return;
                }
            }
        }

        private static AnimatorStateTransition Responsive(AnimatorState from, AnimatorState to, float duration)
        {
            return Configure(from.AddTransition(to), false, 0f, duration, TransitionInterruptionSource.Destination);
        }

        private static AnimatorStateTransition Responsive(AnimatorState from, AnimatorStateMachine to, float duration)
        {
            return Configure(from.AddTransition(to), false, 0f, duration, TransitionInterruptionSource.Destination);
        }

        private static AnimatorStateTransition ExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            return Configure(from.AddTransition(to), true, exitTime, duration, TransitionInterruptionSource.None);
        }

        private static AnimatorStateTransition ExitTransition(AnimatorState from, AnimatorStateMachine to, float exitTime, float duration)
        {
            return Configure(from.AddTransition(to), true, exitTime, duration, TransitionInterruptionSource.None);
        }

        private static AnimatorStateTransition Configure(AnimatorStateTransition transition, bool exitTime, float exitTimeValue, float duration, TransitionInterruptionSource interruption)
        {
            transition.hasExitTime = exitTime;
            transition.exitTime = exitTimeValue;
            transition.duration = duration;
            transition.hasFixedDuration = true;
            transition.interruptionSource = interruption;
            transition.orderedInterruption = true;
            return transition;
        }

        private static void If(this AnimatorStateTransition transition, string parameter)
        {
            transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
        }

        private static AnimatorStateMachine ResetLayer(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorStateMachine machine = layers[0].stateMachine;

            ClearStateMachine(machine);

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (asset == null || asset == controller || asset == machine)
                {
                    continue;
                }

                Object.DestroyImmediate(asset, true);
            }

            if (layers.Length > 1)
            {
                layers = new[] { layers[0] };
            }

            layers[0].iKPass = true;
            layers[0].defaultWeight = 1f;
            controller.layers = layers;
            return controller.layers[0].stateMachine;
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            foreach (ChildAnimatorState child in sm.states)
            {
                sm.RemoveState(child.state);
            }

            foreach (AnimatorStateTransition transition in sm.anyStateTransitions)
            {
                sm.RemoveAnyStateTransition(transition);
            }

            foreach (AnimatorTransition transition in sm.entryTransitions)
            {
                sm.RemoveEntryTransition(transition);
            }

            foreach (ChildAnimatorStateMachine child in sm.stateMachines)
            {
                ClearStateMachine(child.stateMachine);
                sm.RemoveStateMachine(child.stateMachine);
            }
        }

        private static void ResetParameters(AnimatorController controller)
        {
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter(ParkourAnimatorIds.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(ParkourAnimatorIds.VerticalSpeed, AnimatorControllerParameterType.Float);
            AddFloat(controller, ParkourAnimatorIds.LocomotionCadence, 1f);
            AddFloat(controller, ParkourAnimatorIds.ActionSpeed, 1f);
            controller.AddParameter(ParkourAnimatorIds.WallSide, AnimatorControllerParameterType.Float);
            AddBool(controller, ParkourAnimatorIds.Grounded, true);
            controller.AddParameter(ParkourAnimatorIds.Falling, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParkourAnimatorIds.WallRunning, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParkourAnimatorIds.Sliding, AnimatorControllerParameterType.Bool);
            controller.AddParameter(ParkourAnimatorIds.Crouching, AnimatorControllerParameterType.Bool);

            foreach (string trigger in ParkourAnimatorIds.Triggers)
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            }
        }

        private static void AddFloat(AnimatorController controller, string parameterName, float defaultValue)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = defaultValue
            });
        }

        private static void AddBool(AnimatorController controller, string parameterName, bool defaultValue)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = parameterName,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = defaultValue
            });
        }

        private static void EnsureLooping()
        {
            foreach (string path in ClipSources)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                ModelImporterClipAnimation[] takes = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                bool changed = false;
                for (int i = 0; i < takes.Length; i++)
                {
                    bool loop = System.Array.IndexOf(LoopingClips, ShortName(takes[i].name)) >= 0;
                    if (loop && (!takes[i].loopTime || !takes[i].loopPose))
                    {
                        takes[i].loopTime = true;
                        takes[i].loopPose = true;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = takes;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>();
            foreach (string path in ClipSources)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        result[ShortName(clip.name)] = clip;
                    }
                }
            }

            return result;
        }

        private static bool HasAll(Dictionary<string, AnimationClip> clips)
        {
            string[] required =
            {
                "Happy Idle", "Henita_Walk_Athletic", "Henita_Jog_Athletic", "Henita_Run_Athletic", "Jump", "Zombie Punching", "Zombie Punching (1)",
                "HenitaRoll", "HenitaFlip", "HenitaWallJump", "Slide_Start", "Slide_Loop", "Slide_Exit",
                "ClimbUp_1m", "Dodge_Forward", "Hit_Knockback", "Crouching", "Sneaking"
            };

            bool ok = true;
            foreach (string clipName in required)
            {
                if (!clips.ContainsKey(clipName))
                {
                    Debug.LogError("[ParkourAnimator] Missing clip: " + clipName);
                    ok = false;
                }
            }

            return ok;
        }

        private static int CountStates(AnimatorStateMachine machine)
        {
            int count = machine.states.Length;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                count += CountStates(child.stateMachine);
            }

            return count;
        }

        private static int CountTransitions(AnimatorStateMachine machine)
        {
            int count = machine.anyStateTransitions.Length + machine.entryTransitions.Length;
            foreach (ChildAnimatorState child in machine.states)
            {
                count += child.state.transitions.Length;
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                count += CountTransitions(child.stateMachine);
            }

            return count;
        }

        private static string ShortName(string clipName)
        {
            int separator = clipName.LastIndexOf('|');
            return separator >= 0 ? clipName.Substring(separator + 1) : clipName;
        }
    }
}
