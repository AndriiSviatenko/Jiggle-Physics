using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JigglePhysics.Demo
{
    [DefaultExecutionOrder(10100)]
    public sealed class JiggleTargetVisualizer : MonoBehaviour
    {
        private const int CircleSegments = 24;
        private const int LimitCircleSegments = 36;
        private const float TrailTime = 2f;
        private const float VelocityArrowSeconds = 0.15f;
        private const float AccelerationArrowScale = 0.02f;
        private const float MaximumArrowLength = 0.14f;
        private const float GravityArrowLength = 0.07f;
        private const float PlanRibbonWidth = 0.013f;
        private const float ActualRibbonWidth = 0.022f;
        private const float DeviationRibbonWidth = 0.016f;

        private sealed class ChainVisual
        {
            public JiggleChain Chain;
            public JiggleChain PartnerChain;
            public int Index;
            public Mesh Mesh;
            public Mesh RibbonMesh;
            public Transform AnchorRoot;
            public Transform TargetRoot;
            public Transform SimulatedRoot;
            public Transform[] AnchorDots;
            public Transform[] TargetDots;
            public Transform[] SimulatedDots;
            public TrailRenderer SimulatedTrail;
            public TrailRenderer AnimatedTrail;
            public TextMesh Label;
            public Transform LabelTransform;
        }

        [SerializeField] private JiggleRig rig;

        private readonly List<ChainVisual> visuals = new List<ChainVisual>();
        private readonly List<Vector3> meshVertices = new List<Vector3>(512);
        private readonly List<Color> meshColors = new List<Color>(512);
        private readonly List<int> meshIndices = new List<int>(512);
        private readonly List<Vector3> ribbonVertices = new List<Vector3>(256);
        private readonly List<Color> ribbonColors = new List<Color>(256);
        private readonly List<int> ribbonIndices = new List<int>(512);
        private readonly StringBuilder labelBuilder = new StringBuilder(128);

        private Transform container;
        private Material lineMaterial;
        private Material anchorDotMaterial;
        private Material targetDotMaterial;
        private Material simulatedDotMaterial;
        private Material simulatedTrailMaterial;
        private Material animatedTrailMaterial;
        private JiggleRig duelPartner;
        private static Mesh dotMesh;

        public JiggleRig Rig
        {
            get { return rig; }
        }

        public void SetDuelPartner(JiggleRig partner)
        {
            duelPartner = partner;
        }

        private void Awake()
        {
            if (rig == null)
            {
                rig = GetComponent<JiggleRig>();
            }

            if (rig == null)
            {
                rig = GetComponentInParent<JiggleRig>();
            }

            if (rig == null)
            {
                rig = GetComponentInChildren<JiggleRig>();
            }

            CreateMaterials();
        }

        private void LateUpdate()
        {
            if (rig == null || !rig.Initialized)
            {
                return;
            }

            if (!EnsureVisuals())
            {
                return;
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                UpdateChainVisual(visuals[i]);
            }
        }

        private void OnDestroy()
        {
            DestroyMaterial(lineMaterial);
            DestroyMaterial(anchorDotMaterial);
            DestroyMaterial(targetDotMaterial);
            DestroyMaterial(simulatedDotMaterial);
            DestroyMaterial(simulatedTrailMaterial);
            DestroyMaterial(animatedTrailMaterial);
        }

        private bool EnsureVisuals()
        {
            bool matches = visuals.Count == rig.ChainCount;
            if (matches)
            {
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (visuals[i].Chain != rig.GetChain(i))
                    {
                        matches = false;
                        break;
                    }
                }
            }

            if (matches)
            {
                return true;
            }

            RebuildVisuals();
            return visuals.Count > 0;
        }

        private void RebuildVisuals()
        {
            if (container != null)
            {
                Destroy(container.gameObject);
                container = null;
            }

            visuals.Clear();

            GameObject containerObject = new GameObject("JiggleViz");
            containerObject.transform.SetParent(transform, false);
            container = containerObject.transform;

            for (int i = 0; i < rig.ChainCount; i++)
            {
                JiggleChain chain = rig.GetChain(i);
                if (chain == null || !chain.IsValid || chain.ParticleCount == 0)
                {
                    continue;
                }

                visuals.Add(CreateChainVisual(chain, i));
            }
        }

        private ChainVisual CreateChainVisual(JiggleChain chain, int index)
        {
            ChainVisual visual = new ChainVisual();
            visual.Chain = chain;
            visual.Index = index;
            visual.PartnerChain = ResolvePartnerChain(index);

            GameObject chainObject = new GameObject("Chain" + index);
            chainObject.transform.SetParent(container, false);

            GameObject linesObject = new GameObject("Lines");
            linesObject.transform.SetParent(chainObject.transform, false);
            MeshFilter filter = linesObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = linesObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            visual.Mesh = new Mesh { name = "JiggleVizMesh" };
            visual.Mesh.MarkDynamic();
            filter.sharedMesh = visual.Mesh;

            GameObject ribbonsObject = new GameObject("Ribbons");
            ribbonsObject.transform.SetParent(chainObject.transform, false);
            MeshFilter ribbonFilter = ribbonsObject.AddComponent<MeshFilter>();
            MeshRenderer ribbonRenderer = ribbonsObject.AddComponent<MeshRenderer>();
            ribbonRenderer.sharedMaterial = lineMaterial;
            ribbonRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ribbonRenderer.receiveShadows = false;
            visual.RibbonMesh = new Mesh { name = "JiggleVizRibbons" };
            visual.RibbonMesh.MarkDynamic();
            ribbonFilter.sharedMesh = visual.RibbonMesh;

            visual.AnchorDots = CreateDots(chainObject.transform, "Anchors", chain.ParticleCount, anchorDotMaterial);
            visual.TargetDots = CreateDots(chainObject.transform, "Targets", chain.ParticleCount, targetDotMaterial);
            visual.SimulatedDots = CreateDots(chainObject.transform, "Simulated", chain.ParticleCount, simulatedDotMaterial);
            visual.AnchorRoot = visual.AnchorDots.Length > 0 ? visual.AnchorDots[0].parent : null;
            visual.TargetRoot = visual.TargetDots.Length > 0 ? visual.TargetDots[0].parent : null;
            visual.SimulatedRoot = visual.SimulatedDots.Length > 0 ? visual.SimulatedDots[0].parent : null;

            visual.SimulatedTrail = CreateTrail(chainObject.transform, "TrailSimulated", JiggleLabVisualSettings.SimulatedColor, simulatedTrailMaterial, 0.03f);
            visual.AnimatedTrail = CreateTrail(chainObject.transform, "TrailTarget", JiggleLabVisualSettings.TargetColor, animatedTrailMaterial, 0.018f);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(chainObject.transform, false);
            visual.Label = labelObject.AddComponent<TextMesh>();
            visual.Label.fontSize = 36;
            visual.Label.characterSize = 0.019f;
            visual.Label.anchor = TextAnchor.LowerCenter;
            visual.Label.alignment = TextAlignment.Center;
            visual.Label.color = JiggleLabVisualSettings.TargetColor;
            visual.LabelTransform = labelObject.transform;
            labelObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return visual;
        }

        private JiggleChain ResolvePartnerChain(int index)
        {
            if (duelPartner == null || index < 0 || index >= duelPartner.ChainCount)
            {
                return null;
            }

            return duelPartner.GetChain(index);
        }

        private Transform[] CreateDots(Transform parent, string name, int count, Material material)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform[] dots = new Transform[count];
            float diameter = name == "Anchors" ? 0.011f : name == "Targets" ? 0.028f : 0.034f;

            for (int i = 0; i < count; i++)
            {
                GameObject dot = new GameObject("Dot" + i);
                dot.transform.SetParent(root.transform, false);
                MeshFilter filter = dot.AddComponent<MeshFilter>();
                filter.sharedMesh = ResolveDotMesh();
                MeshRenderer renderer = dot.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                dot.transform.localScale = Vector3.one * diameter;
                dots[i] = dot.transform;
            }

            root.SetActive(count > 0);
            return dots;
        }

        private TrailRenderer CreateTrail(Transform parent, string name, Color color, Material material, float width)
        {
            GameObject trailObject = new GameObject(name);
            trailObject.transform.SetParent(parent, false);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = TrailTime;
            trail.minVertexDistance = 0.003f;
            trail.startWidth = width;
            trail.endWidth = width * 0.1f;
            trail.numCapVertices = 2;
            trail.autodestruct = false;
            trail.sharedMaterial = material;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        private void UpdateChainVisual(ChainVisual visual)
        {
            JiggleChain chain = visual.Chain;
            int count = chain.ParticleCount;
            JiggleProfile profile = chain.Profile;
            float maxOffset = profile != null ? profile.MaxOffset : 0.1f;

            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = chain.GetAnchor(i);
                Vector3 target = chain.GetAnimatedTip(i);
                Vector3 simulated = chain.GetSimulatedTip(i);

                if (i < visual.AnchorDots.Length)
                {
                    visual.AnchorDots[i].position = anchor;
                    visual.TargetDots[i].position = target;
                    visual.SimulatedDots[i].position = simulated;
                }
            }

            SetActive(visual.AnchorRoot, JiggleLabVisualSettings.ShowPoints);
            SetActive(visual.TargetRoot, JiggleLabVisualSettings.ShowPoints);
            SetActive(visual.SimulatedRoot, JiggleLabVisualSettings.ShowPoints);

            int last = count - 1;
            Vector3 lastTarget = chain.GetAnimatedTip(last);
            Vector3 lastSimulated = chain.GetSimulatedTip(last);

            UpdateTrail(visual.SimulatedTrail, lastSimulated);
            UpdateTrail(visual.AnimatedTrail, lastTarget);

            float deviation = Vector3.Distance(lastSimulated, lastTarget);
            float normalized = maxOffset > JiggleMath.Epsilon ? Mathf.Clamp01(deviation / maxOffset) : 0f;
            UpdateLabel(visual, chain, deviation, maxOffset, normalized);

            BuildMesh(visual);
        }

        private void UpdateTrail(TrailRenderer trail, Vector3 position)
        {
            if (trail == null)
            {
                return;
            }

            trail.gameObject.SetActive(JiggleLabVisualSettings.ShowTrails);
            if (JiggleLabVisualSettings.ShowTrails)
            {
                trail.transform.position = position;
            }
        }

        private void UpdateLabel(ChainVisual visual, JiggleChain chain, float deviation, float maxOffset, float normalized)
        {
            if (visual.Label == null)
            {
                return;
            }

            bool show = JiggleLabVisualSettings.ShowLabels;
            visual.Label.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            labelBuilder.Length = 0;
            labelBuilder.Append(ResolveLabelPrefix());
            labelBuilder.Append(rig.ChainCount > 1 ? chain.DebugName : rig.name);
            labelBuilder.Append(" · ").Append(chain.ActiveSolverKind);
            labelBuilder.Append('\n');
            labelBuilder.Append((deviation * 1000f).ToString("F1"));
            labelBuilder.Append(" / ");
            labelBuilder.Append((maxOffset * 1000f).ToString("F0"));
            labelBuilder.Append(" мм (").Append((normalized * 100f).ToString("F0")).Append(" %)");
            visual.Label.text = labelBuilder.ToString();

            int last = chain.ParticleCount - 1;
            Camera labelCamera = Camera.main;
            Vector3 labelPosition;
            if (rig.ChainCount > 1)
            {
                Vector3 sideOffset = Vector3.zero;
                Vector3 verticalOffset = Vector3.up * 0.1f;
                if (labelCamera != null)
                {
                    float spacing = (visual.Index - (rig.ChainCount - 1) * 0.5f) * 0.1f;
                    sideOffset = labelCamera.transform.right * spacing;
                }

                verticalOffset += Vector3.up * (visual.Index * 0.17f);
                labelPosition = chain.GetAnimatedTip(last) + verticalOffset + sideOffset;
            }
            else
            {
                labelPosition = chain.GetAnchor(0) + Vector3.up * 0.2f;
            }

            visual.LabelTransform.position = labelPosition;
            if (labelCamera != null)
            {
                visual.LabelTransform.rotation = labelCamera.transform.rotation;
            }

            visual.Label.color = JiggleLabVisualSettings.DeviationColor(normalized);
        }

        private string ResolveLabelPrefix()
        {
            if (rig.name.EndsWith("_A"))
            {
                return "A · ";
            }

            return rig.name.EndsWith("_B") ? "B · " : string.Empty;
        }

        private void BuildMesh(ChainVisual visual)
        {
            meshVertices.Clear();
            meshColors.Clear();
            ribbonVertices.Clear();
            ribbonColors.Clear();
            ribbonIndices.Clear();
            JiggleChain chain = visual.Chain;
            JiggleProfile profile = chain.Profile;
            Camera viewCamera = Camera.main;
            Vector3 viewDirection = viewCamera != null ? viewCamera.transform.forward : Vector3.forward;

            for (int i = 0; i < chain.ParticleCount; i++)
            {
                Vector3 anchor = chain.GetAnchor(i);
                Vector3 target = chain.GetAnimatedTip(i);
                Vector3 simulated = chain.GetSimulatedTip(i);

                if (JiggleLabVisualSettings.ShowBones)
                {
                    JiggleDebugGeometry.AppendRibbon(ribbonVertices, ribbonColors, ribbonIndices, anchor, target, viewDirection, PlanRibbonWidth, JiggleLabVisualSettings.AnimatedBoneColor, JiggleLabVisualSettings.AnimatedBoneColor);
                    JiggleDebugGeometry.AppendRibbon(ribbonVertices, ribbonColors, ribbonIndices, anchor, simulated, viewDirection, ActualRibbonWidth, JiggleLabVisualSettings.SimulatedBoneColor, JiggleLabVisualSettings.SimulatedBoneColor);
                }

                if (JiggleLabVisualSettings.ShowDeviation)
                {
                    float maxOffset = profile != null ? Mathf.Max(profile.MaxOffset, JiggleMath.Epsilon) : 0.1f;
                    float normalized = Mathf.Clamp01(Vector3.Distance(simulated, target) / maxOffset);
                    Color deviationColor = JiggleLabVisualSettings.DeviationColor(normalized);
                    JiggleDebugGeometry.AppendRibbon(ribbonVertices, ribbonColors, ribbonIndices, target, simulated, viewDirection, DeviationRibbonWidth, deviationColor, deviationColor);
                }

                if (JiggleLabVisualSettings.ShowLimit && profile != null)
                {
                    JiggleDebugGeometry.AppendCircle(meshVertices, meshColors, target, target - anchor, profile.MaxOffset, JiggleLabVisualSettings.LimitColor, LimitCircleSegments);
                }

                if (JiggleLabVisualSettings.ShowCone && profile != null)
                {
                    Vector3 direction = target - anchor;
                    JiggleDebugGeometry.AppendCone(meshVertices, meshColors, anchor, direction, direction.magnitude, profile.MaxAngle, JiggleLabVisualSettings.ConeColor, JiggleLabVisualSettings.ConeColor, CircleSegments, 4);
                }

                if (JiggleLabVisualSettings.ShowVelocity)
                {
                    Vector3 velocity = chain.GetVelocity(i) * VelocityArrowSeconds;
                    JiggleDebugGeometry.AppendArrow(meshVertices, meshColors, simulated, ClampArrow(velocity), JiggleLabVisualSettings.VelocityColor);
                }

                if (JiggleLabVisualSettings.ShowForces)
                {
                    Vector3 spring = ClampArrow(chain.GetSpringAcceleration(i) * AccelerationArrowScale);
                    JiggleDebugGeometry.AppendArrow(meshVertices, meshColors, simulated, spring, JiggleLabVisualSettings.SpringColor);
                    JiggleDebugGeometry.AppendArrow(meshVertices, meshColors, simulated, Vector3.down * GravityArrowLength, JiggleLabVisualSettings.GravityColor);
                }
            }

            if (JiggleLabVisualSettings.ShowDuelLink && visual.PartnerChain != null && visual.PartnerChain.IsValid)
            {
                int last = chain.ParticleCount - 1;
                int partnerLast = visual.PartnerChain.ParticleCount - 1;
                JiggleDebugGeometry.AppendLine(meshVertices, meshColors,
                    chain.GetSimulatedTip(last),
                    visual.PartnerChain.GetSimulatedTip(partnerLast),
                    JiggleLabVisualSettings.DuelLinkColor);
            }

            Mesh mesh = visual.Mesh;
            mesh.Clear();
            if (meshVertices.Count > 0)
            {
                mesh.SetVertices(meshVertices);
                mesh.SetColors(meshColors);
                meshIndices.Clear();
                for (int i = 0; i < meshVertices.Count; i++)
                {
                    meshIndices.Add(i);
                }

                mesh.SetIndices(meshIndices, MeshTopology.Lines, 0);
            }

            Mesh ribbonMesh = visual.RibbonMesh;
            ribbonMesh.Clear();
            if (ribbonVertices.Count > 0)
            {
                ribbonMesh.SetVertices(ribbonVertices);
                ribbonMesh.SetColors(ribbonColors);
                ribbonMesh.SetIndices(ribbonIndices, MeshTopology.Triangles, 0);
            }
        }

        private static Vector3 ClampArrow(Vector3 vector)
        {
            if (!JiggleMath.IsFinite(vector))
            {
                return Vector3.zero;
            }

            float magnitude = vector.magnitude;
            if (magnitude <= MaximumArrowLength)
            {
                return vector;
            }

            return vector * (MaximumArrowLength / magnitude);
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private void CreateMaterials()
        {
            lineMaterial = CreateUnlitMaterial(Color.white);
            anchorDotMaterial = CreateUnlitMaterial(JiggleLabVisualSettings.AnchorColor);
            targetDotMaterial = CreateUnlitMaterial(JiggleLabVisualSettings.TargetColor);
            simulatedDotMaterial = CreateUnlitMaterial(JiggleLabVisualSettings.SimulatedColor);
            simulatedTrailMaterial = CreateUnlitMaterial(JiggleLabVisualSettings.SimulatedColor);
            animatedTrailMaterial = CreateUnlitMaterial(JiggleLabVisualSettings.TargetColor);
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private static Mesh ResolveDotMesh()
        {
            if (dotMesh == null)
            {
                dotMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            }

            return dotMesh;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
