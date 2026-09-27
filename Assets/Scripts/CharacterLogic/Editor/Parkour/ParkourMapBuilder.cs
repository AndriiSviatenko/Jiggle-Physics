using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public sealed class ParkourMapBuilder
    {
        private const float DeckUvScale = 0.25f;
        private const float PropUvScale = 0.5f;
        private const float DetailUvScale = 1f;

        private readonly Transform root;
        private readonly ParkourMaterials materials;
        private readonly List<Deck> decks;
        private readonly List<Link> links;
        private readonly List<VaultProp> vaults;
        private readonly List<WallRunSpan> wallRuns;
        private readonly List<BeamSpan> beams;
        private readonly List<SlideGate> slides;

        private float cursorZ;
        private float cursorX;
        private float cursorY;
        private string cursorName;

        public ParkourMapBuilder(
            Transform root,
            ParkourMaterials materials,
            List<Deck> decks,
            List<Link> links,
            List<VaultProp> vaults,
            List<WallRunSpan> wallRuns,
            List<BeamSpan> beams,
            List<SlideGate> slides)
        {
            this.root = root;
            this.materials = materials;
            this.decks = decks;
            this.links = links;
            this.vaults = vaults;
            this.wallRuns = wallRuns;
            this.beams = beams;
            this.slides = slides;
        }

        public Deck Last { get; private set; }
        public float CursorEnd => cursorZ;
        public ParkourMaterials Materials => materials;

        public void ResetCursor(float z, float x, float y, string name)
        {
            cursorZ = z;
            cursorX = x;
            cursorY = y;
            cursorName = name;
        }

        public Deck AddDeck(string name, float width, float length, float xCentre, float topY, float gap, LinkKind kind)
        {
            float zStart = cursorZ + gap;
            float zEnd = zStart + length;

            ParkourMeshFactory.CreateBlock(
                name,
                root,
                new Vector3(xCentre, topY - RouteMetrics.DeckThickness * 0.5f, (zStart + zEnd) * 0.5f),
                new Vector3(width, RouteMetrics.DeckThickness, length),
                materials.Concrete,
                DeckUvScale,
                true);

            ParkourMeshFactory.CreateBlock(
                name + " Border L",
                root,
                new Vector3(xCentre - width * 0.5f + 0.08f, topY + 0.03f, (zStart + zEnd) * 0.5f),
                new Vector3(0.16f, 0.06f, length),
                materials.ConcreteDark,
                DetailUvScale,
                false);

            ParkourMeshFactory.CreateBlock(
                name + " Border R",
                root,
                new Vector3(xCentre + width * 0.5f - 0.08f, topY + 0.03f, (zStart + zEnd) * 0.5f),
                new Vector3(0.16f, 0.06f, length),
                materials.ConcreteDark,
                DetailUvScale,
                false);

            float buildingHeight = topY - RouteMetrics.StreetY - RouteMetrics.DeckThickness;
            if (buildingHeight > 0.5f)
            {
                float bodyY = RouteMetrics.StreetY + buildingHeight * 0.5f;
                float bodyWidth = width * 0.94f;
                float bodyLength = length * 0.94f;
                ParkourMeshFactory.CreateBlock(
                    name + " Tower Body",
                    root,
                    new Vector3(xCentre, bodyY, (zStart + zEnd) * 0.5f),
                    new Vector3(bodyWidth, buildingHeight, bodyLength),
                    materials.CityFacade,
                    DeckUvScale * 0.5f,
                    false);
            }

            if (cursorName != null)
            {
                float rise = topY - cursorY;
                links.Add(new Link
                {
                    From = cursorName,
                    To = name,
                    Kind = kind,
                    Gap = gap,
                    Rise = rise,
                    Allowed = RouteMetrics.AllowedGap(kind, rise),
                    AllowedMomentum = RouteMetrics.AllowedMomentumGap(kind, rise)
                });
            }

            Deck deck = new Deck { Name = name, X = xCentre, ZStart = zStart, ZEnd = zEnd, TopY = topY, Width = width };
            decks.Add(deck);

            cursorZ = zEnd;
            cursorX = xCentre;
            cursorY = topY;
            cursorName = name;
            Last = deck;
            return deck;
        }

        public void BeamSpan(string name, float width, float length, float xCentre, float topY)
        {
            float zStart = cursorZ;
            float zEnd = zStart + length;
            float centreZ = (zStart + zEnd) * 0.5f;

            ParkourMeshFactory.CreateBlock(name, root, new Vector3(xCentre, topY - 0.25f, centreZ), new Vector3(width, 0.5f, length), materials.ConcreteDark, PropUvScale, true);

            ParkourMeshFactory.CreateBlock(name + " Runner Line", root, new Vector3(xCentre, topY + 0.02f, centreZ), new Vector3(width * 0.35f, 0.04f, length * 0.96f), materials.RunnerRed, DetailUvScale, false);

            beams.Add(new BeamSpan { Name = name, Width = width, Length = length });
            cursorZ = zEnd;
            cursorX = xCentre;
            cursorY = topY;
        }

        public void VaultBox(string name, float x, float z, float floorY, Vector3 size)
        {

            ParkourMeshFactory.CreateBlock(name, root, new Vector3(x, floorY + size.y * 0.5f, z), size, materials.Metal, PropUvScale, true);

            ParkourMeshFactory.CreateBlock(name + " Vault Pad", root, new Vector3(x, floorY + size.y + 0.03f, z), new Vector3(size.x + 0.12f, 0.06f, size.z + 0.12f), materials.RunnerRed, DetailUvScale, false);

            ParkourMeshFactory.CreateBlock(name + " Grille", root, new Vector3(x, floorY + size.y * 0.5f, z - size.z * 0.5f - 0.02f), new Vector3(size.x * 0.65f, size.y * 0.6f, 0.04f), materials.ConcreteDark, DetailUvScale, false);

            vaults.Add(new VaultProp { Name = name, Position = new Vector3(x, floorY, z), Height = size.y });
        }

        public void Railing(string name, float x, float zStart, float zEnd, float floorY)
        {
            float length = zEnd - zStart;
            if (length <= 0.1f)
            {
                return;
            }

            ParkourMeshFactory.CreateBlock(name, root, new Vector3(x, floorY + 1.02f, (zStart + zEnd) * 0.5f), new Vector3(0.12f, 0.14f, length), materials.Metal, PropUvScale, true);

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float z = Mathf.Lerp(zStart, zEnd, i / (float)(posts - 1));
                ParkourMeshFactory.CreateBlock(name + " Post " + i, root, new Vector3(x, floorY + 0.5f, z), new Vector3(0.09f, 1f, 0.09f), materials.Metal, PropUvScale, false);
            }
        }

        public void Chevrons(string name, Vector3 position, float yaw, int count, float spacing)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(root, false);
            row.transform.localPosition = position;
            row.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            for (int i = 0; i < count; i++)
            {
                float z = i * spacing;
                GameObject left = ParkourMeshFactory.CreateBlock(name + " L" + i, row.transform, new Vector3(-0.35f, 0.02f, z), new Vector3(0.16f, 0.03f, 0.9f), materials.RunnerRed, DetailUvScale, false);
                left.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                GameObject right = ParkourMeshFactory.CreateBlock(name + " R" + i, row.transform, new Vector3(0.35f, 0.02f, z), new Vector3(0.16f, 0.03f, 0.9f), materials.RunnerRed, DetailUvScale, false);
                right.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            }
        }

        public void WallRunPanel(string name, float x, float zStart, float zEnd, float baseY, float height)
        {
            float length = zEnd - zStart;
            float centreZ = (zStart + zEnd) * 0.5f;

            ParkourMeshFactory.CreateBlock(name, root, new Vector3(x, baseY + height * 0.5f, centreZ), new Vector3(0.6f, height, length), materials.ConcreteDark, PropUvScale, true);

            float faceX = x - Mathf.Sign(x) * 0.32f;
            ParkourMeshFactory.CreateBlock(name + " Board", root, new Vector3(faceX, baseY + height * 0.5f, centreZ), new Vector3(0.05f, height * 0.92f, length * 0.98f), materials.RunnerRed, PropUvScale, false);

            ParkourMeshFactory.CreateBlock(name + " Stripe White", root, new Vector3(faceX - Mathf.Sign(x) * 0.03f, baseY + height * 0.58f, centreZ), new Vector3(0.04f, 0.32f, length * 0.95f), materials.RunnerWhite, DetailUvScale, false);
            ParkourMeshFactory.CreateBlock(name + " Stripe Accent", root, new Vector3(faceX - Mathf.Sign(x) * 0.03f, baseY + height * 0.36f, centreZ), new Vector3(0.04f, 0.20f, length * 0.95f), materials.RunnerWhite, DetailUvScale, false);

            ParkourMeshFactory.CreateBlock(name + " Cap", root, new Vector3(x, baseY + height + 0.15f, centreZ), new Vector3(0.85f, 0.30f, length), materials.Metal, DetailUvScale, false);

            wallRuns.Add(new WallRunSpan { Name = name, Length = length });
        }

        public void SlideGateObstacle(string name, float xCentre, float z, float floorY, float width, float clearance)
        {
            float slabHeight = 1.3f;
            float legHeight = 3.4f;

            ParkourMeshFactory.CreateBlock(name, root, new Vector3(xCentre, floorY + clearance + slabHeight * 0.5f, z), new Vector3(width, slabHeight, 0.9f), materials.ConcreteDark, PropUvScale, true);

            ParkourMeshFactory.CreateBlock(name + " Trim Red", root, new Vector3(xCentre, floorY + clearance - 0.04f, z), new Vector3(width, 0.10f, 1.0f), materials.RunnerRed, DetailUvScale, false);
            ParkourMeshFactory.CreateBlock(name + " Stripe White", root, new Vector3(xCentre, floorY + clearance + 0.12f, z + 0.47f), new Vector3(width * 0.9f, 0.12f, 0.04f), materials.RunnerWhite, DetailUvScale, false);

            ParkourMeshFactory.CreateBlock(name + " Leg L", root, new Vector3(xCentre - width * 0.5f, floorY + legHeight * 0.5f, z), new Vector3(0.5f, legHeight, 0.9f), materials.Metal, PropUvScale, true);
            ParkourMeshFactory.CreateBlock(name + " Leg R", root, new Vector3(xCentre + width * 0.5f, floorY + legHeight * 0.5f, z), new Vector3(0.5f, legHeight, 0.9f), materials.Metal, PropUvScale, true);

            slides.Add(new SlideGate { Name = name, Clearance = clearance });
        }

        public void FacadeBands(string name, Deck deck)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = deck.X + side * (deck.Width * 0.5f + 0.06f);
                for (int band = 0; band < 2; band++)
                {
                    float y = deck.TopY - 0.85f - band * 1.1f;
                    float thickness = band == 0 ? 0.34f : 0.22f;
                    ParkourMeshFactory.CreateBlock(name + " Band " + side + band, root, new Vector3(x, y, (deck.ZStart + deck.ZEnd) * 0.5f), new Vector3(0.09f, thickness, deck.Length * 0.82f), materials.Window, PropUvScale, false);
                }
            }
        }

        public void Antenna(string name, Vector3 basePosition, float height)
        {
            ParkourMeshFactory.CreateCylinder(name + " Mast", root, basePosition + Vector3.up * (height * 0.5f), Quaternion.identity, 0.07f, height, materials.Metal, false);

            for (int i = 1; i <= 3; i++)
            {
                float y = basePosition.y + height * (i / 4f);
                ParkourMeshFactory.CreateBlock(name + " Bar " + i, root, new Vector3(basePosition.x, y, basePosition.z), new Vector3(1.2f - i * 0.24f, 0.06f, 0.06f), materials.Metal, DetailUvScale, false);
            }

            ParkourMeshFactory.CreateDisc(name + " Beacon", root, basePosition + Vector3.up * (height + 0.12f), 0.22f, 0.2f, materials.RunnerRed);
        }

        public void WaterTower(string name, Vector3 basePosition, float roofY)
        {
            const float legHeight = 2.2f;

            for (int i = 0; i < 4; i++)
            {
                float x = basePosition.x + (i % 2 == 0 ? -1.4f : 1.4f);
                float z = basePosition.z + (i < 2 ? -1.4f : 1.4f);
                ParkourMeshFactory.CreateBlock(name + " Leg " + i, root, new Vector3(x, roofY + legHeight * 0.5f, z), new Vector3(0.14f, legHeight, 0.14f), materials.Metal, PropUvScale, false);
            }

            ParkourMeshFactory.CreateCylinder(name + " Tank", root, new Vector3(basePosition.x, roofY + legHeight + 1.6f, basePosition.z), Quaternion.identity, 2f, 3.2f, materials.Metal, false);
            ParkourMeshFactory.CreateCylinder(name + " Cap", root, new Vector3(basePosition.x, roofY + legHeight + 3.35f, basePosition.z), Quaternion.identity, 2.1f, 0.35f, materials.RunnerRed, false);
        }

        public void Crane(string name, Vector3 basePosition, float height, float jibLength)
        {

            ParkourMeshFactory.CreateBlock(name + " Tower", root, basePosition + Vector3.up * (height * 0.5f), new Vector3(1.7f, height, 1.7f), materials.RunnerOrange, PropUvScale, true);
            ParkourMeshFactory.CreateBlock(name + " Jib", root, basePosition + new Vector3(jibLength * 0.5f - 2f, height, 0f), new Vector3(jibLength, 0.9f, 0.9f), materials.RunnerOrange, PropUvScale, false);
            ParkourMeshFactory.CreateBlock(name + " Counter", root, basePosition + new Vector3(-5f, height, 0f), new Vector3(6f, 1.1f, 1.1f), materials.Metal, PropUvScale, false);
            ParkourMeshFactory.CreateBlock(name + " Cabin", root, basePosition + new Vector3(0f, height - 2.2f, 1.4f), new Vector3(2f, 1.8f, 1.6f), materials.Glass, PropUvScale, false);
            ParkourMeshFactory.CreateCable(name + " Cable", root, basePosition + new Vector3(jibLength - 3f, height, 0f), basePosition + new Vector3(jibLength - 3f, height - 12f, 0f), 0.06f, materials.ConcreteDark);
            ParkourMeshFactory.CreateBlock(name + " Hook", root, basePosition + new Vector3(jibLength - 3f, height - 12.5f, 0f), new Vector3(0.7f, 0.9f, 0.7f), materials.RunnerRed, PropUvScale, false);
            ParkourMeshFactory.CreateDisc(name + " Beacon", root, basePosition + Vector3.up * (height + 0.6f), 0.3f, 0.3f, materials.RunnerRed);
        }

        public void Cable(string name, Vector3 from, Vector3 to, float radius)
        {
            ParkourMeshFactory.CreateCable(name, root, from, to, radius, materials.ConcreteDark);
        }

        public void PointLight(string name, Vector3 position, Color color, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        public void Checkpoint(string name, Vector3 position, float yaw)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(root, false);
            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject disc = ParkourMeshFactory.CreateDisc("Ring", marker.transform, new Vector3(0f, 0.02f, 0f), 1.8f, 0.04f, materials.RunnerRed);

            ParkourMeshFactory.CreateBlock("Pylon L", marker.transform, new Vector3(-2.6f, 1.1f, 0f), new Vector3(0.18f, 2.2f, 0.18f), materials.Metal, 1f, false);
            ParkourMeshFactory.CreateDisc("Beacon L", marker.transform, new Vector3(-2.6f, 2.25f, 0f), 0.16f, 0.12f, materials.RunnerRed);

            ParkourMeshFactory.CreateBlock("Pylon R", marker.transform, new Vector3(2.6f, 1.1f, 0f), new Vector3(0.18f, 2.2f, 0.18f), materials.Metal, 1f, false);
            ParkourMeshFactory.CreateDisc("Beacon R", marker.transform, new Vector3(2.6f, 2.25f, 0f), 0.16f, 0.12f, materials.RunnerRed);

            RunnerCheckpoint checkpoint = marker.AddComponent<RunnerCheckpoint>();
            checkpoint.Configure(disc.GetComponent<Renderer>());
        }

        public void Portal(string name, Vector3 position)
        {
            GameObject portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portal.name = name;
            portal.transform.SetParent(root, false);
            portal.transform.position = position;
            portal.transform.localScale = new Vector3(2.2f, 0.18f, 2.2f);
            portal.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            portal.GetComponent<Renderer>().sharedMaterial = materials.Finish;
            Collider portalCollider = portal.GetComponent<Collider>();
            if (portalCollider != null)
            {
                portalCollider.enabled = false;
            }

            for (int i = 0; i < 3; i++)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Portal Ring";
                ring.transform.SetParent(portal.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0f, i * 0.35f - 0.35f);
                ring.transform.localScale = new Vector3(1f - i * 0.18f, 0.45f, 1f - i * 0.18f);
                ring.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? materials.Finish : materials.Cyan;
                Collider ringCollider = ring.GetComponent<Collider>();
                if (ringCollider != null)
                {
                    Object.DestroyImmediate(ringCollider);
                }
            }
        }

        public GameObject PlaceBuilding(string name, string prefabPath, Vector3 basePosition, float targetHeight, float yaw, bool solid)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = name;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(go);
                return null;
            }

            Bounds localBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                localBounds.Encapsulate(renderers[i].bounds);
            }

            foreach (Renderer renderer in renderers)
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i] = materials.CityFacade;
                }
                renderer.sharedMaterials = slots;
            }

            float scaleY = targetHeight / Mathf.Max(0.01f, localBounds.size.y);
            go.transform.localScale = Vector3.one * scaleY;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = basePosition;

            Bounds placed = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                placed.Encapsulate(renderers[i].bounds);
            }
            go.transform.position += new Vector3(0f, basePosition.y - placed.min.y, 0f);

            if (solid)
            {
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.center = localBounds.center;
                collider.size = localBounds.size;
            }

            return go;
        }

        public void Street()
        {
            ParkourMeshFactory.CreateBlock("Street", root, new Vector3(0f, RouteMetrics.StreetY - 0.6f, 150f), new Vector3(460f, 1.2f, 560f), materials.Road, 0.04f, false);
        }

        public GameObject Solid(string name, Vector3 centre, Vector3 size, Material material, float uvPerMeter)
        {
            return ParkourMeshFactory.CreateBlock(name, root, centre, size, material, uvPerMeter, true);
        }

        public GameObject Detail(string name, Vector3 centre, Vector3 size, Material material, float uvPerMeter)
        {
            return ParkourMeshFactory.CreateBlock(name, root, centre, size, material, uvPerMeter, false);
        }

        public GameObject Cylinder(string name, Vector3 centre, Quaternion rotation, float radius, float height, Material material, bool solid)
        {
            return ParkourMeshFactory.CreateCylinder(name, root, centre, rotation, radius, height, material, solid);
        }
    }
}
