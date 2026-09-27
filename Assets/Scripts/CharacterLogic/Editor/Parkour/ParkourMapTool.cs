using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CharacterLogic.EditorTools
{
    public static class ParkourMapTool
    {
        private const string RootName = "Parkour World";
        private const string KenneyFolder = "Assets/ThirdParty/KenneyCityCommercial/Models";
        private const string SkyboxPath = "Assets/Settings/Character/MirrorsEdgeSkybox.mat";

        private static readonly List<Deck> Decks = new List<Deck>();
        private static readonly List<Link> Links = new List<Link>();
        private static readonly List<VaultProp> Vaults = new List<VaultProp>();
        private static readonly List<WallRunSpan> WallRuns = new List<WallRunSpan>();
        private static readonly List<BeamSpan> Beams = new List<BeamSpan>();
        private static readonly List<SlideGate> Slides = new List<SlideGate>();

        [MenuItem("Tools/Parkour/1. Generate Neon District")]
        public static void Generate()
        {
            ClearWorld();
            ResetRoute();

            GameObject root = new GameObject(RootName);
            ParkourMaterials materials = ParkourMaterials.Build();
            ParkourMapBuilder builder = new ParkourMapBuilder(root.transform, materials, Decks, Links, Vaults, WallRuns, Beams, Slides);

            BuildMainRoute(builder);
            BuildLowerRoute(builder);
            BuildMarkers(root.transform, builder, materials);
            BuildSkyline(root.transform, builder, materials);
            builder.Street();
            ConfigureEnvironment(root.transform);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Henita");
            if (player != null && Decks.Count > 0)
            {
                Deck first = Decks[0];
                player.transform.position = new Vector3(first.X, first.TopY + 0.05f, first.ZStart + 2f);
                player.transform.rotation = Quaternion.identity;
                EditorUtility.SetDirty(player);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            RouteReport report = Validate();
            Debug.Log("[ParkourMapTool] Mirror's Edge style district generated: " + Decks.Count + " decks, " + Links.Count + " links, " + Beams.Count + " beams, " + WallRuns.Count + " wall-run panels, " + Vaults.Count + " vaults. Validation: " + (report.Passed ? "PASSED" : "FAILED"));
        }

        [MenuItem("Tools/Parkour/2. Validate Route")]
        public static RouteReport Validate()
        {
            RouteReport report = new RouteReport();
            float tightestMargin = float.MaxValue;
            string tightestLink = "none";

            foreach (Link link in Links)
            {
                report.LinkChecks++;
                if (!link.Ok)
                {
                    report.FailedLinks++;
                    Debug.LogError("[Route] Unreachable: " + link.From + " -> " + link.To + " (" + link.Kind + "): gap " + link.Gap.ToString("0.00") + " m vs base limit " + link.Allowed.ToString("0.00") + " m and momentum limit " + link.AllowedMomentum.ToString("0.00") + " m, rise " + link.Rise.ToString("0.00") + " m");
                    continue;
                }

                float margin;
                if (link.NeedsMomentum)
                {
                    report.MomentumLinks++;
                    margin = link.AllowedMomentum - link.Gap;
                    Debug.Log("[Route] Momentum jump: " + link.From + " -> " + link.To + ", gap " + link.Gap.ToString("0.00") + " m — needs a run-up (base " + link.Allowed.ToString("0.00") + " m, with momentum " + link.AllowedMomentum.ToString("0.00") + " m)");
                }
                else
                {
                    margin = link.Allowed - link.Gap;
                }

                if (margin < tightestMargin)
                {
                    tightestMargin = margin;
                    tightestLink = link.From + " -> " + link.To;
                }
            }

            foreach (Deck deck in Decks)
            {
                report.GeometryChecks++;
                Vector3 origin = new Vector3(deck.X, deck.TopY + 3f, (deck.ZStart + deck.ZEnd) * 0.5f);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (Mathf.Abs(hit.point.y - deck.TopY) > RouteMetrics.MaxLandingTolerance)
                    {
                        report.FailedGeometry++;
                        Debug.LogError("[Route] Deck height for \"" + deck.Name + "\" = " + hit.point.y.ToString("0.00") + ", expected " + deck.TopY.ToString("0.00"));
                    }
                }
                else
                {
                    report.FailedGeometry++;
                    Debug.LogError("[Route] No supporting geometry under deck \"" + deck.Name + "\"");
                }
            }

            foreach (VaultProp vault in Vaults)
            {
                report.PropChecks++;
                if (vault.Height > RouteMetrics.MaxVaultHeight + 0.001f)
                {
                    report.FailedProps++;
                    Debug.LogError("[Route] Vault \"" + vault.Name + "\" is too tall: " + vault.Height.ToString("0.00") + " m vs limit " + RouteMetrics.MaxVaultHeight.ToString("0.00") + " m");
                }
            }

            foreach (BeamSpan beam in Beams)
            {
                report.PropChecks++;
                if (beam.Width < RouteMetrics.MinBeamWidth)
                {
                    report.FailedProps++;
                    Debug.LogError("[Route] Beam \"" + beam.Name + "\" is too narrow: " + beam.Width.ToString("0.00") + " m vs minimum " + RouteMetrics.MinBeamWidth.ToString("0.00") + " m");
                }
            }

            foreach (WallRunSpan span in WallRuns)
            {
                report.PropChecks++;
                if (span.Length < RouteMetrics.MinWallRunLength)
                {
                    report.FailedProps++;
                    Debug.LogError("[Route] Wall-run panel \"" + span.Name + "\" is too short: " + span.Length.ToString("0.00") + " m vs minimum " + RouteMetrics.MinWallRunLength.ToString("0.00") + " m");
                }
            }

            foreach (SlideGate gate in Slides)
            {
                report.PropChecks++;
                if (gate.Clearance < RouteMetrics.MinSlideClearance)
                {
                    report.FailedProps++;
                    Debug.LogError("[Route] Slide gate \"" + gate.Name + "\" has too little clearance: " + gate.Clearance.ToString("0.00") + " m vs minimum " + RouteMetrics.MinSlideClearance.ToString("0.00") + " m");
                }
            }

            string summary = "[Route] Links checked: " + report.LinkChecks + " (momentum jumps: " + report.MomentumLinks + "), decks: " + report.GeometryChecks + ", props: " + report.PropChecks + ". Tightest margin: " + (tightestMargin == float.MaxValue ? "none" : tightestMargin.ToString("0.00") + " m (" + tightestLink + ")");
            if (report.Passed)
            {
                Debug.Log(summary + " — ALL ELEMENTS ARE TRAVERSABLE.");
            }
            else
            {
                Debug.LogError(summary + " — PROBLEMS FOUND: links " + report.FailedLinks + ", geometry " + report.FailedGeometry + ", props " + report.FailedProps);
            }

            return report;
        }

        [MenuItem("Tools/Parkour/3. Clear")]
        public static void Clear()
        {
            ClearWorld();
            ResetRoute();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void BuildMainRoute(ParkourMapBuilder builder)
        {
            ParkourMaterials materials = builder.Materials;

            Deck start = builder.AddDeck("HQ Start", 14f, 22f, 0f, 16.0f, 0f, LinkKind.Jump);
            builder.Railing("Start Railing L", -6.55f, start.ZStart + 0.5f, start.ZEnd - 0.5f, start.TopY);
            builder.Railing("Start Railing R", 6.55f, start.ZStart + 0.5f, start.ZEnd - 0.5f, start.TopY);
            builder.Chevrons("Start Chevrons", new Vector3(0f, 16.02f, start.ZStart + 12f), 0f, 3, 2.4f);

            Deck dash = builder.AddDeck("Edge Sprint", 12f, 20f, 0f, 16.0f, 3.0f, LinkKind.Jump);
            builder.Chevrons("Sprint Chevrons", new Vector3(0f, 16.02f, dash.ZStart + 8f), 0f, 3, 2.2f);

            Deck terrace = builder.AddDeck("Penthouse Terrace", 14f, 24f, 0f, 12.0f, 4.0f, LinkKind.Drop);
            builder.VaultBox("HVAC Vault 1", -2.2f, terrace.ZStart + 10f, 12.0f, new Vector3(1.3f, 1.05f, 1.8f));
            builder.VaultBox("HVAC Vault 2", 2.2f, terrace.ZStart + 15f, 12.0f, new Vector3(1.3f, 1.05f, 1.8f));
            builder.Chevrons("Terrace Chevrons", new Vector3(0f, 12.02f, terrace.ZStart + 18f), 0f, 3, 2.0f);

            builder.Solid("Teleport Pylon L", new Vector3(-5.5f, 14.5f, terrace.ZEnd - 1f), new Vector3(0.5f, 5.0f, 0.5f), materials.Cyan, 1f);
            builder.Solid("Teleport Pylon R", new Vector3(5.5f, 14.5f, terrace.ZEnd - 1f), new Vector3(0.5f, 5.0f, 0.5f), materials.Cyan, 1f);
            builder.Detail("Teleport Banner", new Vector3(0f, 16.8f, terrace.ZEnd - 1f), new Vector3(11.5f, 0.6f, 0.2f), materials.RunnerRed, 1f);

            Deck postBlink = builder.AddDeck("Post-Teleport Terrace", 14f, 22f, 0f, 12.0f, 12.0f, LinkKind.Blink);
            builder.Chevrons("District Turn Chevrons", new Vector3(2.5f, 12.02f, postBlink.ZStart + 12f), 24f, 3, 2.0f);

            Deck blockA = builder.AddDeck("Rooftop Block A", 12f, 18f, 5f, 13.8f, 2.8f, LinkKind.DoubleJump);
            builder.Chevrons("Block A Chevrons", new Vector3(6.5f, 13.82f, blockA.ZStart + 9f), 20f, 3, 2.0f);

            Deck blockB = builder.AddDeck("Rooftop Block B", 12f, 18f, 10f, 15.6f, 2.8f, LinkKind.DoubleJump);
            builder.Chevrons("Block B Chevrons", new Vector3(9f, 15.62f, blockB.ZStart + 9f), -20f, 3, 2.0f);

            Deck upperTier = builder.AddDeck("District Upper Tier", 14f, 20f, 3f, 17.4f, 2.8f, LinkKind.DoubleJump);
            builder.Chevrons("Upper Tier Chevrons", new Vector3(2f, 17.42f, upperTier.ZStart + 10f), -15f, 3, 2.0f);

            Deck skybridge = builder.AddDeck("High Skybridge", 8f, 24f, 0f, 19.0f, 2.8f, LinkKind.DoubleJump);
            builder.Railing("Bridge Railing L", -3.55f, skybridge.ZStart + 0.3f, skybridge.ZEnd - 0.3f, skybridge.TopY);
            builder.Railing("Bridge Railing R", 3.55f, skybridge.ZStart + 0.3f, skybridge.ZEnd - 0.3f, skybridge.TopY);
            builder.Chevrons("Bridge Chevrons", new Vector3(0f, 19.02f, skybridge.ZStart + 11f), 0f, 3, 2.2f);

            Deck wallCanyon = builder.AddDeck("Wall Run Canyon", 14f, 24f, 0f, 19.0f, 2.5f, LinkKind.Jump);
            builder.WallRunPanel("Wall Run L", -6.8f, wallCanyon.ZStart + 3f, wallCanyon.ZEnd + 6f, 18.0f, 7.5f);
            builder.WallRunPanel("Wall Run R", 6.8f, wallCanyon.ZStart + 3f, wallCanyon.ZEnd + 6f, 18.0f, 7.5f);
            builder.Chevrons("Canyon Chevrons", new Vector3(0f, 19.02f, wallCanyon.ZStart + 13f), 0f, 3, 2.0f);

            Deck summitHelipad = builder.AddDeck("Summit Helipad", 16f, 22f, 0f, 20.8f, 2.8f, LinkKind.DoubleJump);
            builder.Detail("Helipad Circle", new Vector3(0f, 20.82f, summitHelipad.ZStart + 11f), new Vector3(10f, 0.04f, 10f), materials.RunnerWhite, 0.2f);
            builder.Detail("Helipad H1", new Vector3(-2.0f, 20.83f, summitHelipad.ZStart + 11f), new Vector3(0.6f, 0.05f, 6.0f), materials.RunnerRed, 1f);
            builder.Detail("Helipad H2", new Vector3(2.0f, 20.83f, summitHelipad.ZStart + 11f), new Vector3(0.6f, 0.05f, 6.0f), materials.RunnerRed, 1f);
            builder.Detail("Helipad HBar", new Vector3(0f, 20.83f, summitHelipad.ZStart + 11f), new Vector3(4.6f, 0.05f, 0.8f), materials.RunnerRed, 1f);

            builder.BeamSpan("Steel Beam Chasm", 0.5f, 12.0f, 0f, 20.8f);
            Deck postBeam = builder.AddDeck("Observation Deck", 10f, 14f, 0f, 20.8f, 0f, LinkKind.Jump);
            builder.Chevrons("Descent Chevrons", new Vector3(0f, 20.82f, postBeam.ZStart + 5f), 0f, 3, 2.0f);

            Deck drop1 = builder.AddDeck("Drop Ramp Alpha", 12f, 20f, -2f, 15.0f, 4.5f, LinkKind.Drop);
            builder.Chevrons("Ramp Chevrons", new Vector3(-2f, 15.02f, drop1.ZStart + 9f), 10f, 3, 2.0f);

            Deck slideDeck = builder.AddDeck("Speed Slide Gallery", 12f, 22f, 0f, 9.0f, 4.5f, LinkKind.Drop);
            builder.SlideGateObstacle("Slide Gate", -2.0f, slideDeck.ZStart + 12f, 9.0f, 5.5f, 1.35f);
            builder.VaultBox("Descent Vault", 3.2f, slideDeck.ZStart + 12f, 9.0f, new Vector3(1.3f, 0.85f, 1.6f));
            builder.Chevrons("Slide Chevrons", new Vector3(-2.0f, 9.02f, slideDeck.ZStart + 6f), 0f, 3, 1.8f);

            Deck finishRun = builder.AddDeck("Pre-Finish Sprint", 14f, 20f, 0f, 4.0f, 4.5f, LinkKind.Drop);
            builder.Chevrons("Finish Run Chevrons", new Vector3(0f, 4.02f, finishRun.ZStart + 9f), 0f, 3, 2.0f);

            Deck finishSquare = builder.AddDeck("Finish Plaza", 22f, 36f, 0f, 0.0f, 3.5f, LinkKind.Jump);
        }

        private static void BuildLowerRoute(ParkourMapBuilder builder)
        {
        }

        private static void BuildMarkers(Transform root, ParkourMapBuilder builder, ParkourMaterials materials)
        {

            builder.Detail("Start Line Red", new Vector3(0f, 16.04f, 2f), new Vector3(12f, 0.06f, 0.5f), materials.RunnerRed, 1f);
            builder.Detail("Start Line White", new Vector3(0f, 16.045f, 2.4f), new Vector3(12f, 0.05f, 0.2f), materials.RunnerWhite, 1f);

            builder.Solid("Start Arch Pylon L", new Vector3(-5.5f, 18.5f, 3f), new Vector3(0.4f, 5f, 0.4f), materials.Metal, 0.5f);
            builder.Solid("Start Arch Pylon R", new Vector3(5.5f, 18.5f, 3f), new Vector3(0.4f, 5f, 0.4f), materials.Metal, 0.5f);
            builder.Detail("Start Arch Top", new Vector3(0f, 21.2f, 3f), new Vector3(11.4f, 0.4f, 0.4f), materials.RunnerRed, 1f);

            Deck finishSquare = Decks.Count > 0 ? Decks[Decks.Count - 1] : null;
            float finishZ = finishSquare != null ? finishSquare.ZStart + 18f : 380f;
            float finishWallZ = finishSquare != null ? finishSquare.ZEnd - 2f : 398f;

            builder.Solid("Finish Gate Pylon L", new Vector3(-8.5f, 3.2f, finishZ), new Vector3(0.5f, 6.4f, 0.5f), materials.Metal, 0.5f);
            builder.Solid("Finish Gate Pylon R", new Vector3(8.5f, 3.2f, finishZ), new Vector3(0.5f, 6.4f, 0.5f), materials.Metal, 0.5f);
            builder.Detail("Finish Gate Top", new Vector3(0f, 6.5f, finishZ), new Vector3(17.5f, 0.6f, 0.6f), materials.Finish, 1f);
            builder.Detail("Finish Tape", new Vector3(0f, 0.04f, finishZ), new Vector3(22f, 0.05f, 0.8f), materials.Finish, 1f);
            builder.Detail("Finish Wall", new Vector3(0f, 4.0f, finishWallZ), new Vector3(24f, 8.0f, 0.5f), materials.ConcreteDark, 0.5f);
            builder.Detail("Finish Banner", new Vector3(0f, 4.0f, finishWallZ - 0.25f), new Vector3(20f, 5.0f, 0.1f), materials.Finish, 1f);

            Deck startDeck = Decks.Find(d => d.Name == "HQ Start");
            if (startDeck != null)
            {
                builder.Checkpoint("Checkpoint Start", new Vector3(startDeck.X, startDeck.TopY + 0.02f, startDeck.ZStart + 4f), 0f);
            }

            Deck terraceDeck = Decks.Find(d => d.Name == "Penthouse Terrace");
            if (terraceDeck != null)
            {
                builder.Checkpoint("Checkpoint Terrace", new Vector3(terraceDeck.X, terraceDeck.TopY + 0.02f, terraceDeck.ZStart + 4f), 0f);
            }

            Deck blinkDeck = Decks.Find(d => d.Name == "Post-Teleport Terrace");
            if (blinkDeck != null)
            {
                builder.Checkpoint("Checkpoint Teleport", new Vector3(blinkDeck.X, blinkDeck.TopY + 0.02f, blinkDeck.ZStart + 4f), 0f);
            }

            Deck tierDeck = Decks.Find(d => d.Name == "District Upper Tier");
            if (tierDeck != null)
            {
                builder.Checkpoint("Checkpoint District", new Vector3(tierDeck.X, tierDeck.TopY + 0.02f, tierDeck.ZStart + 3.5f), -15f);
            }

            Deck bridgeDeck = Decks.Find(d => d.Name == "High Skybridge");
            if (bridgeDeck != null)
            {
                builder.Checkpoint("Checkpoint Bridge", new Vector3(bridgeDeck.X, bridgeDeck.TopY + 0.02f, bridgeDeck.ZStart + 3.5f), 0f);
            }

            Deck canyonDeck = Decks.Find(d => d.Name == "Wall Run Canyon");
            if (canyonDeck != null)
            {
                builder.Checkpoint("Checkpoint WallRun", new Vector3(canyonDeck.X, canyonDeck.TopY + 0.02f, canyonDeck.ZStart + 3.5f), 0f);
            }

            Deck summitDeck = Decks.Find(d => d.Name == "Summit Helipad");
            if (summitDeck != null)
            {
                builder.Checkpoint("Checkpoint Summit", new Vector3(summitDeck.X, summitDeck.TopY + 0.02f, summitDeck.ZStart + 3.5f), 0f);
            }

            Deck slideDeck = Decks.Find(d => d.Name == "Speed Slide Gallery");
            if (slideDeck != null)
            {
                builder.Checkpoint("Checkpoint Slide", new Vector3(slideDeck.X, slideDeck.TopY + 0.02f, slideDeck.ZStart + 2.5f), 0f);
            }

            if (finishSquare != null)
            {
                builder.Checkpoint("Checkpoint Finish", new Vector3(finishSquare.X, finishSquare.TopY + 0.02f, finishZ), 0f);
                builder.Portal("Level Portal", new Vector3(finishSquare.X, finishSquare.TopY + 2.1f, finishZ + 4f));
            }
        }

        private static void BuildSkyline(Transform root, ParkourMapBuilder builder, ParkourMaterials materials)
        {
            List<string> models = CollectBuildingModels();
            if (models.Count == 0)
            {
                return;
            }

            System.Random random = new System.Random(20260607);

            for (int side = -1; side <= 1; side += 2)
            {
                for (float z = -26f; z <= 420f; z += 26f)
                {
                    float distance = 26f + (float)random.NextDouble() * 6f;
                    float height = 30f + (float)random.NextDouble() * 16f;
                    string model = models[random.Next(models.Count)];
                    Vector3 basePosition = new Vector3(side * distance, RouteMetrics.StreetY, z + (float)random.NextDouble() * 6f);
                    builder.PlaceBuilding("City Near " + side + " " + z, model, basePosition, height, random.Next(4) * 90f, false);
                }

                for (float z = -40f; z <= 430f; z += 30f)
                {
                    float distance = 42f + (float)random.NextDouble() * 10f;
                    float height = 24f + (float)random.NextDouble() * 18f;
                    builder.PlaceBuilding("City Mid " + side + " " + z, models[random.Next(models.Count)], new Vector3(side * distance, RouteMetrics.StreetY, z), height, random.Next(4) * 90f, false);
                }

                for (float z = -60f; z <= 440f; z += 38f)
                {
                    float distance = 65f + (float)random.NextDouble() * 16f;
                    float height = 18f + (float)random.NextDouble() * 22f;
                    builder.PlaceBuilding("City Far " + side + " " + z, models[random.Next(models.Count)], new Vector3(side * distance, RouteMetrics.StreetY, z), height, random.Next(4) * 90f, false);
                }
            }
        }

        private static void ConfigureEnvironment(Transform root)
        {

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.80f, 0.88f, 0.98f, 1f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.88f, 0.94f, 1.0f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.80f, 0.88f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.62f, 0.66f, 0.72f, 1f);
            RenderSettings.reflectionIntensity = 1.0f;

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (skybox == null)
            {
                skybox = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(skybox, SkyboxPath);
            }

            skybox.shader = Shader.Find("Skybox/Procedural");
            skybox.SetFloat("_SunSize", 0.04f);
            skybox.SetFloat("_SunSizeConvergence", 6f);
            skybox.SetFloat("_AtmosphereThickness", 0.85f);
            skybox.SetColor("_SkyTint", new Color(0.42f, 0.65f, 0.98f, 1f));
            skybox.SetColor("_GroundColor", new Color(0.85f, 0.88f, 0.92f, 1f));
            skybox.SetFloat("_Exposure", 1.35f);
            EditorUtility.SetDirty(skybox);
            RenderSettings.skybox = skybox;

            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                GameObject sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root, false);
                sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.name = "Sun";
            sun.color = new Color(1.0f, 0.98f, 0.94f);
            sun.intensity = 1.95f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -36f, 0f);

            ConfigurePostProcessing();
        }

        private static void ConfigurePostProcessing()
        {
            Volume volume = Object.FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                GameObject gv = GameObject.Find("Global Volume");
                if (gv != null)
                {
                    volume = gv.GetComponent<Volume>();
                }
            }

            VolumeProfile profile = volume != null ? volume.profile : null;
            if (profile == null)
            {
                return;
            }

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.mode.Override(TonemappingMode.Neutral);

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.7f);

            if (!profile.TryGet(out ColorAdjustments colorAdjust))
            {
                colorAdjust = profile.Add<ColorAdjustments>(true);
            }
            colorAdjust.contrast.Override(16f);
            colorAdjust.saturation.Override(8f);
            colorAdjust.postExposure.Override(0.2f);

            EditorUtility.SetDirty(profile);
        }

        private static List<string> CollectBuildingModels()
        {
            List<string> paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { KenneyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("building"))
                {
                    paths.Add(path);
                }
            }

            paths.Sort();
            return paths;
        }

        private static void ClearWorld()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == RootName)
                {
                    Object.DestroyImmediate(rootObject);
                }
            }
        }

        private static void ResetRoute()
        {
            Decks.Clear();
            Links.Clear();
            Vaults.Clear();
            WallRuns.Clear();
            Beams.Clear();
            Slides.Clear();
        }
    }
}
