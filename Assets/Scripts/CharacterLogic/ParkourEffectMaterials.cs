using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CharacterLogic
{

    public static class ParkourEffectMaterials
    {
        private static readonly Dictionary<long, Material> Cache = new Dictionary<long, Material>();
        private static Texture2D glowTexture;

        public static Material Get(Color emission, bool softGlow = true)
        {
            long key = ((long)ColorKey(emission) << 1) | (softGlow ? 1L : 0L);
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = "Effect " + emission };
            Texture2D texture = softGlow ? GetGlowTexture() : Texture2D.whiteTexture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", emission);
            if (material.HasProperty("_Color")) material.SetColor("_Color", emission);

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 2f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            Cache[key] = material;
            return material;
        }

        private static int ColorKey(Color color)
        {
            Color32 packed = new Color32(
                (byte)Mathf.Clamp(color.r * 50f, 0f, 255f),
                (byte)Mathf.Clamp(color.g * 50f, 0f, 255f),
                (byte)Mathf.Clamp(color.b * 50f, 0f, 255f),
                (byte)Mathf.Clamp(color.a * 255f, 0f, 255f));
            return packed.r | (packed.g << 8) | (packed.b << 16) | (packed.a << 24);
        }

        private static Texture2D GetGlowTexture()
        {
            if (glowTexture != null)
            {
                return glowTexture;
            }

            const int size = 64;
            glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Effect Glow", wrapMode = TextureWrapMode.Clamp };
            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float alpha = Mathf.Clamp01(1f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            glowTexture.SetPixels(pixels);
            glowTexture.Apply(false, true);
            return glowTexture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            glowTexture = null;
        }
    }
}
