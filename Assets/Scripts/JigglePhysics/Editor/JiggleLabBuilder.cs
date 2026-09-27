using System.IO;
using JigglePhysics.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JigglePhysics.EditorTools
{

    public static class JiggleLabBuilder
    {
        private const string ScenePath = "Assets/Scenes/JiggleLab.unity";
        private const string ProfileFolder = "Assets/Settings/JigglePhysics/Lab";

        private sealed class Preset
        {
            public string Name;
            public string Label;
            public float Frequency;
            public float Damping;
            public float Gravity;
            public float MaxOffset;
            public float Squash;
            public Color Color;
        }

        private static readonly Preset[] Presets =
        {
            new Preset { Name = "1_Zhele",     Label = "ЖЕЛЕ  f=1.5  ζ=0.08",      Frequency = 1.5f, Damping = 0.08f, Gravity = 0.6f,  MaxOffset = 0.40f, Squash = 0.25f, Color = new Color(0.4f, 0.8f, 1f) },
            new Preset { Name = "2_Breast",    Label = "ГРУДИ (наш профіль)  f=2.7  ζ=0.2", Frequency = 2.7f, Damping = 0.2f,  Gravity = 0.25f, MaxOffset = 0.15f, Squash = 0.18f, Color = new Color(1f, 0.5f, 0.7f) },
            new Preset { Name = "3_Pruzhno",   Label = "ПРУЖНО  f=5  ζ=0.3",       Frequency = 5.0f, Damping = 0.3f,  Gravity = 0.15f, MaxOffset = 0.10f, Squash = 0.05f, Color = new Color(0.5f, 1f, 0.6f) },
            new Preset { Name = "4_Vidskok0",  Label = "БЕЗ ВІДСКОКУ  f=2.7  ζ=1.0", Frequency = 2.7f, Damping = 1.0f,  Gravity = 0.25f, MaxOffset = 0.15f, Squash = 0.0f,  Color = new Color(1f, 0.85f, 0.3f) },
        };

        [MenuItem("Tools/Jiggle Physics/Build Jiggle Lab Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(ProfileFolder);
            AssetDatabase.Refresh();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupCameraAndLight();
            SetupGround();

            for (int i = 0; i < Presets.Length; i++)
            {
                BuildRig(Presets[i], new Vector3(-3.3f + i * 2.5f, 2.4f, 1.6f));
            }

            GameObject labRoot = new GameObject("JiggleLab");
            labRoot.AddComponent<JiggleLabController>();
            labRoot.AddComponent<JiggleLabCharacterDriver>();
            labRoot.AddComponent<JiggleLabCameraRig>();
            labRoot.AddComponent<JiggleLabHud>();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[JiggleLab] Scene built: " + ScenePath);
        }

        private static void SetupCameraAndLight()
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.transform.position = new Vector3(0.95f, 2.2f, -7.4f);
            camGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void SetupGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            SetColor(ground, new Color(0.18f, 0.19f, 0.22f));
        }

        private static void BuildRig(Preset preset, Vector3 position)
        {
            GameObject root = new GameObject(preset.Name);
            root.transform.position = position;
            root.AddComponent<JiggleLabRigDriver>();

            Transform previous = root.transform;
            Transform bone0 = null;
            const int boneCount = 3;
            const float segment = 0.28f;

            for (int i = 0; i < boneCount; i++)
            {
                GameObject bone = new GameObject("Bone" + i);
                bone.transform.SetParent(previous, false);
                bone.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, -segment, 0f);
                if (i == 0) bone0 = bone.transform;

                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "Vis";
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.transform.SetParent(bone.transform, false);
                ball.transform.localScale = Vector3.one * (i == boneCount - 1 ? 0.16f : 0.1f);
                SetColor(ball, preset.Color);

                if (i > 0)
                {
                    GameObject link = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    link.name = "Link";
                    Object.DestroyImmediate(link.GetComponent<Collider>());
                    link.transform.SetParent(bone.transform, false);
                    link.transform.localPosition = new Vector3(0f, segment * 0.5f, 0f);
                    link.transform.localScale = new Vector3(0.05f, segment * 0.5f, 0.05f);
                    SetColor(link, preset.Color * 0.8f);
                }

                previous = bone.transform;
            }

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            TextMesh text = labelGo.AddComponent<TextMesh>();
            text.text = preset.Label;
            text.fontSize = 40;
            text.characterSize = 0.045f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = preset.Color;

            root.AddComponent<JiggleTargetVisualizer>();

            JiggleProfile profile = CreateProfile(preset);
            JiggleRig rig = root.AddComponent<JiggleRig>();
            SerializedObject rigObject = new SerializedObject(rig);
            rigObject.FindProperty("defaultProfile").objectReferenceValue = profile;
            rigObject.FindProperty("drawGizmos").boolValue = false;
            SerializedProperty chains = rigObject.FindProperty("chains");
            chains.ClearArray();
            chains.arraySize = 1;
            chains.GetArrayElementAtIndex(0).FindPropertyRelative("RootBone").objectReferenceValue = bone0;
            chains.GetArrayElementAtIndex(0).FindPropertyRelative("EndBone").objectReferenceValue = null;
            chains.GetArrayElementAtIndex(0).FindPropertyRelative("ProfileOverride").objectReferenceValue = null;
            rigObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static JiggleProfile CreateProfile(Preset preset)
        {
            string path = ProfileFolder + "/" + preset.Name + ".asset";
            JiggleProfile profile = AssetDatabase.LoadAssetAtPath<JiggleProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<JiggleProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.Frequency = preset.Frequency;
            profile.DampingRatio = preset.Damping;
            profile.GravityMultiplier = preset.Gravity;
            profile.MaxOffset = preset.MaxOffset;
            profile.MaxAngle = 70f;
            profile.SquashStretch = preset.Squash;
            profile.WorldInertia = 0.85f;
            profile.LocalInertia = 1f;
            EditorUtility.SetDirty(profile);
            return profile;
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
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }
    }
}
