using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class HumanoidRetargetingTool
    {
        private const string ModelPath = "Assets/Models/Henita.fbx";

        private static readonly string[] AnimationPaths =
        {
            "Assets/Animation/Henita@Happy Idle.fbx",
            "Assets/Animation/Henita@Slow Run.fbx",
            "Assets/Animation/Henita@Fast Run.fbx",
            "Assets/Animation/Henita@Jump.fbx",
            "Assets/Animation/Henita@Zombie Punching.fbx",
            "Assets/Animation/Henita@Zombie Punching (1).fbx"
        };

        private static readonly HumanBodyBones[] KeyBones =
        {
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightIndexProximal
        };

        [MenuItem("Tools/Character/Diagnose Humanoid Rig")]
        public static void Diagnose()
        {
            StringBuilder report = new StringBuilder();
            Avatar modelAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            report.AppendLine($"[Humanoid] Model avatar: {(modelAvatar == null ? "MISSING" : modelAvatar.name)}");
            if (modelAvatar != null)
            {
                report.AppendLine($"[Humanoid]   isValid={modelAvatar.isValid} isHuman={modelAvatar.isHuman}");
            }

            GameObject character = FindCharacter();
            if (character == null)
            {
                report.AppendLine("[Humanoid] No 'Henita' character in the active scene.");
                Debug.Log(report.ToString());
                return;
            }

            Animator animator = FindActiveAnimator(character);
            if (animator == null)
            {
                report.AppendLine("[Humanoid] No enabled animator with an avatar found.");
                Debug.Log(report.ToString());
                return;
            }

            Avatar sceneAvatar = animator.avatar;
            report.AppendLine($"[Humanoid] Animator on '{animator.name}' avatar={(sceneAvatar == null ? "null" : sceneAvatar.name)} valid={(sceneAvatar != null && sceneAvatar.isValid)} human={(sceneAvatar != null && sceneAvatar.isHuman)}");

            if (sceneAvatar != null && sceneAvatar.isHuman)
            {
                for (int i = 0; i < KeyBones.Length; i++)
                {
                    Transform bone = animator.GetBoneTransform(KeyBones[i]);
                    if (bone == null)
                    {
                        report.AppendLine($"[Humanoid]   MISSING bone mapping: {KeyBones[i]}");
                    }
                }
            }

            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller != null)
            {
                AnimationClip[] clips = controller.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    bool human = clips[i].isHumanMotion;
                    report.AppendLine($"[Humanoid] Clip '{clips[i].name}' isHumanMotion={human} loop={clips[i].isLooping}");
                    if (!human && !clips[i].legacy)
                    {
                        report.AppendLine($"[Humanoid]   WARNING: '{clips[i].name}' is not humanoid; limbs will snap to bind pose. Run 'Tools/Character/Repair Humanoid Animation Import'.");
                    }
                }
            }

            ReportDuplicateBones(character, report, "Hips");
            ReportDuplicateBones(character, report, "Left wrist");
            ReportDuplicateBones(character, report, "Right wrist");
            ReportDuplicateBones(character, report, "Left arm");
            ReportDuplicateBones(character, report, "Right arm");

            SkinnedMeshRenderer[] meshes = character.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                SkinnedMeshRenderer mesh = meshes[i];
                Transform[] bones = mesh.bones;
                int nulls = 0;
                int foreign = 0;
                for (int b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == null)
                    {
                        nulls++;
                    }
                    else if (!bones[b].IsChildOf(animator.transform) && bones[b] != animator.transform)
                    {
                        foreign++;
                    }
                }

                report.AppendLine($"[Humanoid] Mesh '{mesh.name}' bones={bones.Length} null={nulls} outsideAnimator={foreign}");
            }

            Debug.Log(report.ToString());
        }

        [MenuItem("Tools/Character/Repair Humanoid Animation Import")]
        public static void RepairAnimationImport()
        {
            Avatar modelAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(ModelPath);
            if (modelAvatar == null || !modelAvatar.isValid || !modelAvatar.isHuman)
            {
                Debug.LogError("[Humanoid] Model avatar is missing or invalid. Fix the Rig on '" + ModelPath + "' first (Rig tab > Apply).");
                return;
            }

            for (int i = 0; i < AnimationPaths.Length; i++)
            {
                ModelImporter importer = AssetImporter.GetAtPath(AnimationPaths[i]) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            StringBuilder report = new StringBuilder();
            report.AppendLine("[Humanoid] Animation clips re-imported as Humanoid.");
            for (int i = 0; i < AnimationPaths.Length; i++)
            {
                AnimationClip clip = LoadFirstClip(AnimationPaths[i]);
                report.AppendLine($"[Humanoid]   {AnimationPaths[i]} isHumanMotion={(clip != null && clip.isHumanMotion)}");
            }

            Debug.Log(report.ToString());
        }

        private static AnimationClip LoadFirstClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void ReportDuplicateBones(GameObject character, StringBuilder report, string boneName)
        {
            Transform[] transforms = character.GetComponentsInChildren<Transform>(true);
            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == boneName)
                {
                    count++;
                }
            }

            if (count > 1)
            {
                report.AppendLine($"[Humanoid] DUPLICATE bone '{boneName}' x{count} (mesh may bind to the wrong skeleton).");
            }
        }

        private static GameObject FindCharacter()
        {
            GameObject character = GameObject.Find("Henita");
            if (character != null)
            {
                return character;
            }

            CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
            return bridge != null ? bridge.gameObject : null;
        }

        private static Animator FindActiveAnimator(GameObject character)
        {
            Animator[] animators = character.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].enabled && animators[i].avatar != null)
                {
                    return animators[i];
                }
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i].avatar != null)
                {
                    return animators[i];
                }
            }

            return null;
        }
    }
}
