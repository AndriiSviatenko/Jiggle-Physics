using System.Collections.Generic;
using UnityEngine;

namespace CharacterLogic.EditorTools
{
    public static class ParkourMeshFactory
    {
        private static readonly int[][] FaceCorners =
        {
            new[] { 4, 5, 6, 7 },
            new[] { 1, 0, 3, 2 },
            new[] { 3, 7, 6, 2 },
            new[] { 0, 1, 5, 4 },
            new[] { 0, 4, 7, 3 },
            new[] { 5, 1, 2, 6 }
        };

        private static readonly Vector3[] FaceNormals =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right
        };

        private static readonly Vector3[] FaceUAxes =
        {
            Vector3.right,
            Vector3.right,
            Vector3.right,
            Vector3.right,
            Vector3.forward,
            Vector3.forward
        };

        private static readonly Vector3[] FaceVAxes =
        {
            Vector3.up,
            Vector3.up,
            Vector3.forward,
            Vector3.forward,
            Vector3.up,
            Vector3.up
        };

        public static Mesh CreateBox(Vector3 size, float uvPerMeter)
        {
            Vector3 half = size * 0.5f;

            Vector3[] corners =
            {
                new Vector3(-half.x, -half.y, -half.z),
                new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, half.y, -half.z),
                new Vector3(-half.x, half.y, -half.z),
                new Vector3(-half.x, -half.y, half.z),
                new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, half.y, half.z),
                new Vector3(-half.x, half.y, half.z)
            };

            List<Vector3> vertices = new List<Vector3>(24);
            List<Vector3> normals = new List<Vector3>(24);
            List<Vector2> uvs = new List<Vector2>(24);
            List<int> triangles = new List<int>(36);

            for (int face = 0; face < 6; face++)
            {
                int start = vertices.Count;
                Vector3 normal = FaceNormals[face];
                Vector3 uAxis = FaceUAxes[face];
                Vector3 vAxis = FaceVAxes[face];

                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = corners[FaceCorners[face][corner]];
                    vertices.Add(point);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(point, uAxis), Vector3.Dot(point, vAxis)) * uvPerMeter);
                }

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            Mesh mesh = new Mesh { name = "ParkourBox" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject CreateBlock(string name, Transform parent, Vector3 centre, Vector3 size, Material material, float uvPerMeter, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.identity;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateBox(size, uvPerMeter);

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (solid)
            {
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.size = size;
                collider.center = Vector3.zero;
            }

            return go;
        }

        public static GameObject CreateCylinder(string name, Transform parent, Vector3 centre, Quaternion rotation, float radius, float height, Material material, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!solid)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        public static GameObject CreateDisc(string name, Transform parent, Vector3 centre, float radius, float thickness, Material material)
        {
            return CreateCylinder(name, parent, centre, Quaternion.identity, radius, thickness, material, false);
        }

        public static GameObject CreateCable(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            Vector3 centre = (from + to) * 0.5f;
            Quaternion rotation = length > 0.001f ? Quaternion.FromToRotation(Vector3.up, direction / length) : Quaternion.identity;
            return CreateCylinder(name, parent, centre, rotation, radius, length, material, false);
        }

        public static GameObject CreatePipe(string name, Transform parent, Vector3 centre, bool alongZ, float radius, float length, Material material, bool solid)
        {
            Quaternion rotation = alongZ ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f);
            return CreateCylinder(name, parent, centre, rotation, radius, length, material, solid);
        }
    }
}
