using UnityEditor;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public sealed class ParkourMaterials
    {
        private const string MaterialFolder = "Assets/Settings/Parkour";
        private const string TextureFolder = "Assets/Textures/City";

        public Material Concrete { get; private set; }
        public Material ConcreteDark { get; private set; }
        public Material Asphalt { get; private set; }
        public Material Metal { get; private set; }
        public Material Glass { get; private set; }
        public Material Window { get; private set; }
        public Material Sign { get; private set; }
        public Material CityFacade { get; private set; }
        public Material Road { get; private set; }
        public Material RunnerRed { get; private set; }
        public Material RunnerOrange { get; private set; }
        public Material RunnerWhite { get; private set; }
        public Material Gold { get; private set; }
        public Material Cyan { get; private set; }
        public Material Pink { get; private set; }
        public Material Blue { get; private set; }
        public Material Finish { get; private set; }

        public static ParkourMaterials Build()
        {
            ParkourMaterials set = new ParkourMaterials();

            set.Concrete = Textured("ME_Concrete_White", "Concrete034_1K-JPG_Color.jpg", "Concrete034_1K-JPG_NormalGL.jpg", new Color(0.96f, 0.97f, 0.99f, 1f), 0.32f, 0.04f, 0.85f);
            set.ConcreteDark = Textured("ME_Concrete_Dark", "Concrete034_1K-JPG_Color.jpg", "Concrete034_1K-JPG_NormalGL.jpg", new Color(0.24f, 0.26f, 0.29f, 1f), 0.25f, 0.05f, 0.85f);
            set.Asphalt = Textured("ME_Asphalt", "Asphalt012_1K-JPG_Color.jpg", "Asphalt012_1K-JPG_NormalGL.jpg", new Color(0.35f, 0.37f, 0.40f, 1f), 0.20f, 0.02f, 1.0f);
            set.Metal = Textured("ME_Metal_White", "Metal032_1K-JPG_Color.jpg", "Metal032_1K-JPG_NormalGL.jpg", new Color(0.92f, 0.94f, 0.96f, 1f), 0.70f, 0.80f, 0.80f);
            set.Glass = Solid("ME_Glass_Reflective", new Color(0.06f, 0.10f, 0.16f, 1f), 0.96f, 0.35f);
            set.Window = Solid("ME_RibbonWindow", new Color(0.10f, 0.14f, 0.20f, 1f), 0.92f, 0.25f);
            set.CityFacade = BuildCityFacade();
            set.Road = Solid("ME_LowerStreet", new Color(0.18f, 0.20f, 0.24f, 1f), 0.15f, 0.0f);

            set.RunnerRed = EmissiveSolid("ME_Runner_Red", new Color(0.96f, 0.09f, 0.09f, 1f), 0.45f, 0.60f, 0.05f);
            set.RunnerOrange = EmissiveSolid("ME_Runner_Orange", new Color(1.0f, 0.45f, 0.05f, 1f), 0.50f, 0.55f, 0.05f);
            set.RunnerWhite = Solid("ME_Runner_White", new Color(0.98f, 0.98f, 0.99f, 1f), 0.50f, 0.05f);

            set.Gold = set.RunnerRed;
            set.Pink = set.RunnerRed;
            set.Sign = set.RunnerOrange;
            set.Cyan = EmissiveSolid("ME_Runner_Cyan", new Color(0.12f, 0.85f, 1.0f, 1f), 0.8f, 0.65f, 0.1f);
            set.Blue = Solid("ME_Accent_Blue", new Color(0.15f, 0.45f, 0.95f, 1f), 0.6f, 0.1f);
            set.Finish = EmissiveSolid("ME_Finish_Gate", new Color(0.98f, 0.12f, 0.12f, 1f), 1.2f, 0.70f, 0.1f);

            AssetDatabase.SaveAssets();
            return set;
        }

        private static Material BuildCityFacade()
        {
            Material material = LoadOrCreate("ME_CityFacade");
            Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/KenneyCityCommercial/Textures/colormap.png");

            material.SetTexture("_BaseMap", colormap);

            material.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.98f, 1f));
            material.SetFloat("_Smoothness", 0.45f);
            material.SetFloat("_Metallic", 0.10f);

            if (colormap != null)
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Textured(string name, string albedoFile, string normalFile, Color tint, float smoothness, float metallic, float normalScale)
        {
            Material material = LoadOrCreate(name);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + albedoFile);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + normalFile);

            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetTextureScale("_BaseMap", Vector2.one);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", normalScale);
                material.SetTextureScale("_BumpMap", Vector2.one);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Solid(string name, Color color, float smoothness, float metallic)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.DisableKeyword("_EMISSION");
            material.DisableKeyword("_NORMALMAP");
            material.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EmissiveSolid(string name, Color color, float emissionMultiplier, float smoothness, float metallic)
        {
            Material material = LoadOrCreate(name);
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * emissionMultiplier);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.DisableKeyword("_NORMALMAP");
            material.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                {
                    litShader = Shader.Find("Standard");
                }

                material = new Material(litShader);
                material.name = name;
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }
    }
}
