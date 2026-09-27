using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class RetargetedAnimationBaker
    {
        private const string ControllerPath = "Assets/Settings/Character/HenitaAnimator.controller";
        private const string OutputFolder = "Assets/Settings/Character/Retargeted";

        [MenuItem("Tools/Character/Bake Retargeted Animation Library")]
        public static void Bake()
        {
            EnsureFolder();
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[RetargetBake] Controller not found: " + ControllerPath);
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                AnimatorState state = child.state;
                if (state.motion is BlendTree tree)
                {
                    ChildMotion[] children = tree.children;
                    for (int i = 0; i < children.Length; i++)
                    {
                        children[i].motion = Resolve(children[i].motion);
                    }
                    tree.children = children;
                }
                else
                {
                    state.motion = Resolve(state.motion);
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[RetargetBake] All external clips compensated for the Z-up rig.");
        }

        private static Motion Resolve(Motion motion)
        {
            AnimationClip clip = motion as AnimationClip;
            if (clip == null)
            {
                return motion;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            bool external = path.Contains("ThirdParty") || path.Contains("Idle_UnityMocap");
            if (!external)
            {
                return clip;
            }

            return Bake(clip);
        }

        private static AnimationClip Bake(AnimationClip source)
        {
            string outputPath = OutputFolder + "/" + Sanitize(source.name) + "_RT.anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existing != null)
            {
                return existing;
            }

            AnimationClip clip = new AnimationClip { name = source.name + "_RT", frameRate = 60f };
            float length = Mathf.Max(0.01f, source.length);

            EditorCurveBinding rx = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.x");
            EditorCurveBinding ry = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.y");
            EditorCurveBinding rz = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.z");
            EditorCurveBinding rw = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ.w");
            AnimationCurve srcX = AnimationUtility.GetEditorCurve(source, rx);
            AnimationCurve srcY = AnimationUtility.GetEditorCurve(source, ry);
            AnimationCurve srcZ = AnimationUtility.GetEditorCurve(source, rz);
            AnimationCurve srcW = AnimationUtility.GetEditorCurve(source, rw);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.propertyName.StartsWith("RootQ"))
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (curve != null)
                {
                    clip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
                }
            }

            int steps = 90;
            AnimationCurve ox = new AnimationCurve();
            AnimationCurve oy = new AnimationCurve();
            AnimationCurve oz = new AnimationCurve();
            AnimationCurve ow = new AnimationCurve();
            Quaternion previous = Quaternion.identity;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps * length;
                Quaternion q = srcX != null
                    ? new Quaternion(srcX.Evaluate(t), srcY.Evaluate(t), srcZ.Evaluate(t), srcW.Evaluate(t))
                    : Quaternion.identity;
                float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                q = magnitude < 1e-6f ? Quaternion.identity : new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);

                Quaternion r = q;
                if (i > 0 && Quaternion.Dot(previous, r) < 0f)
                {
                    r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                }

                ox.AddKey(t, r.x);
                oy.AddKey(t, r.y);
                oz.AddKey(t, r.z);
                ow.AddKey(t, r.w);
                previous = r;
            }

            clip.SetCurve("", typeof(Animator), "RootQ.x", ox);
            clip.SetCurve("", typeof(Animator), "RootQ.y", oy);
            clip.SetCurve("", typeof(Animator), "RootQ.z", oz);
            clip.SetCurve("", typeof(Animator), "RootQ.w", ow);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            bool loop = source.isLooping || source.name.Contains("Run") || source.name.Contains("Walk") || source.name.Contains("Idle") || source.name.Contains("Strafe");
            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.loopBlendOrientation = loop;
            settings.loopBlendPositionY = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, outputPath);
            return clip;
        }

        private static string Sanitize(string name)
        {
            return name.Replace('|', '_').Replace(' ', '_').Replace('/', '_');
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Settings/Character", "Retargeted");
            }
        }
    }
}
