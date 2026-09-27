using UnityEditor;
using UnityEngine;

namespace CharacterLogic.Editor
{
    public static class AnimationReimporter
    {
        public static string ReimportAll()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Models/Henita.fbx");
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                return "ERROR: Henita humanoid avatar is invalid or not found!";
            }

            string[] paths = new string[]
            {
                "Assets/Animation/Henita@Happy Idle.fbx",
                "Assets/Animation/Henita@Slow Run.fbx",
                "Assets/Animation/Henita@Fast Run.fbx",
                "Assets/Animation/Henita@Jump.fbx",
                "Assets/Animation/Henita@Zombie Punching.fbx",
                "Assets/Animation/Henita@Zombie Punching (1).fbx"
            };

            var sb = new System.Text.StringBuilder();

            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    sb.AppendLine($"Missing: {path}");
                    continue;
                }

                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.bakeAxisConversion = true;

                var takes = importer.defaultClipAnimations;
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

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                sb.AppendLine($"{path}: isHumanMotion={clip?.isHumanMotion}, loop={clip?.isLooping}");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return sb.ToString();
        }
    }
}
