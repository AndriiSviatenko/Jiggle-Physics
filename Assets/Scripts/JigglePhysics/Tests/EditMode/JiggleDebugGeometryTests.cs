using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleDebugGeometryTests
    {
        private List<Vector3> vertices;
        private List<Color> colors;
        private List<int> indices;

        [SetUp]
        public void SetUp()
        {
            vertices = new List<Vector3>();
            colors = new List<Color>();
            indices = new List<int>();
        }

        [Test]
        public void AppendLine_AddsTwoColoredVertices()
        {
            JiggleDebugGeometry.AppendLine(vertices, colors, Vector3.zero, Vector3.up, Color.red);

            Assert.AreEqual(2, vertices.Count);
            Assert.AreEqual(2, colors.Count);
            Assert.AreEqual(Vector3.zero, vertices[0]);
            Assert.AreEqual(Vector3.up, vertices[1]);
            Assert.AreEqual(Color.red, colors[0]);
        }

        [Test]
        public void AppendLine_NonFinitePoint_IsSkipped()
        {
            JiggleDebugGeometry.AppendLine(vertices, colors, new Vector3(float.NaN, 0f, 0f), Vector3.up, Color.red);

            Assert.AreEqual(0, vertices.Count);
            Assert.AreEqual(0, colors.Count);
        }

        [Test]
        public void AppendCircle_ClosesLoopAtRadiusInPlane()
        {
            Vector3 center = new Vector3(0.3f, 1.2f, -0.4f);
            float radius = 0.25f;
            int segments = 8;

            JiggleDebugGeometry.AppendCircle(vertices, colors, center, Vector3.up, radius, Color.yellow, segments);

            Assert.AreEqual((segments + 1) * 2, vertices.Count);
            Assert.AreEqual(vertices.Count, colors.Count);
            Assert.AreEqual(vertices[0], vertices[vertices.Count - 1]);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 offset = vertices[i] - center;
                Assert.AreEqual(radius, offset.magnitude, 1e-5f);
                Assert.AreEqual(0f, offset.y, 1e-6f);
            }
        }

        [Test]
        public void AppendCircle_ZeroRadius_AddsNothing()
        {
            JiggleDebugGeometry.AppendCircle(vertices, colors, Vector3.zero, Vector3.up, 0f, Color.yellow);

            Assert.AreEqual(0, vertices.Count);
            Assert.AreEqual(0, colors.Count);
        }

        [Test]
        public void AppendCircle_ZeroNormal_UsesFallbackBasis()
        {
            JiggleDebugGeometry.AppendCircle(vertices, colors, Vector3.zero, Vector3.zero, 0.4f, Color.yellow, 12);

            Assert.AreEqual(26, vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                Assert.IsTrue(JiggleMath.IsFinite(vertices[i]));
                Assert.AreEqual(0.4f, vertices[i].magnitude, 1e-5f);
            }
        }

        [Test]
        public void AppendCone_RingSitsAtAngleDistance()
        {
            float length = 1f;
            float angleDegrees = 30f;

            JiggleDebugGeometry.AppendCone(vertices, colors, Vector3.zero, Vector3.up, length, angleDegrees, Color.cyan, Color.blue, 8, 4);

            float ringCenterHeight = Mathf.Cos(angleDegrees * Mathf.Deg2Rad);
            int ringVertexCount = 18;

            for (int i = 0; i < ringVertexCount; i++)
            {
                Assert.AreEqual(ringCenterHeight, vertices[i].y, 1e-5f);
                Assert.AreEqual(length, vertices[i].magnitude, 1e-5f);
            }
        }

        [Test]
        public void AppendCone_RaysRunFromApexToRing()
        {
            JiggleDebugGeometry.AppendCone(vertices, colors, Vector3.zero, Vector3.up, 1f, 30f, Color.cyan, Color.blue, 8, 4);

            int firstRayIndex = 18;
            for (int i = firstRayIndex; i < firstRayIndex + 8; i += 2)
            {
                Assert.AreEqual(Vector3.zero, vertices[i]);
                Assert.AreEqual(1f, vertices[i + 1].magnitude, 1e-5f);
            }

            Assert.AreEqual(26, vertices.Count);
            Assert.AreEqual(vertices.Count, colors.Count);
        }

        [Test]
        public void AppendCone_ZeroLength_AddsNothing()
        {
            JiggleDebugGeometry.AppendCone(vertices, colors, Vector3.zero, Vector3.up, 0f, 30f, Color.cyan, Color.blue);

            Assert.AreEqual(0, vertices.Count);
        }

        [Test]
        public void AppendArrow_ShaftAndHeadMeetAtTip()
        {
            Vector3 tip = new Vector3(0f, 1f, 0f);

            JiggleDebugGeometry.AppendArrow(vertices, colors, Vector3.zero, tip, Color.magenta);

            Assert.AreEqual(10, vertices.Count);
            Assert.AreEqual(vertices.Count, colors.Count);
            Assert.AreEqual(Vector3.zero, vertices[0]);
            Assert.AreEqual(tip, vertices[1]);

            for (int i = 3; i < vertices.Count; i += 2)
            {
                Assert.AreEqual(tip, vertices[i]);
            }
        }

        [Test]
        public void AppendRibbon_AddsQuadWithBothWidths()
        {
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(0f, 1f, 0f);
            float width = 0.1f;

            JiggleDebugGeometry.AppendRibbon(vertices, colors, indices, from, to, Vector3.forward, width, Color.white, Color.red);

            Assert.AreEqual(4, vertices.Count);
            Assert.AreEqual(4, colors.Count);
            Assert.AreEqual(6, indices.Count);
            Assert.AreEqual(Color.white, colors[0]);
            Assert.AreEqual(Color.white, colors[1]);
            Assert.AreEqual(Color.red, colors[2]);
            Assert.AreEqual(Color.red, colors[3]);

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = vertices[i] - (i < 2 ? from : to);
                Assert.AreEqual(width * 0.5f, offset.magnitude, 1e-5f);
            }
        }

        [Test]
        public void AppendRibbon_IndicesReferenceOwnQuad()
        {
            JiggleDebugGeometry.AppendRibbon(vertices, colors, indices, Vector3.zero, Vector3.right, Vector3.forward, 0.05f, Color.white, Color.white);
            JiggleDebugGeometry.AppendRibbon(vertices, colors, indices, Vector3.up, Vector3.up + Vector3.right, Vector3.forward, 0.05f, Color.white, Color.white);

            Assert.AreEqual(8, vertices.Count);
            Assert.AreEqual(12, indices.Count);
            for (int i = 0; i < 6; i++)
            {
                Assert.Less(indices[i], 4);
            }

            for (int i = 6; i < 12; i++)
            {
                Assert.GreaterOrEqual(indices[i], 4);
                Assert.Less(indices[i], 8);
            }
        }

        [Test]
        public void AppendRibbon_DegenerateInputs_AddNothing()
        {
            JiggleDebugGeometry.AppendRibbon(vertices, colors, indices, Vector3.zero, Vector3.zero, Vector3.forward, 0.1f, Color.white, Color.white);
            JiggleDebugGeometry.AppendRibbon(vertices, colors, indices, Vector3.zero, Vector3.up, Vector3.forward, 0f, Color.white, Color.white);

            Assert.AreEqual(0, vertices.Count);
            Assert.AreEqual(0, indices.Count);
        }

        [Test]
        public void AppendArrow_ZeroVector_AddsNothing()
        {
            JiggleDebugGeometry.AppendArrow(vertices, colors, Vector3.zero, Vector3.zero, Color.magenta);

            Assert.AreEqual(0, vertices.Count);
        }

        [Test]
        public void AppendArrow_HeadWingsSitOnBasePlaneAtHeadWidth()
        {
            Vector3 vector = new Vector3(0f, 2f, 0f);
            Vector3 direction = vector.normalized;
            float headLength = vector.magnitude * 0.3f;
            float headWidth = headLength * 0.35f;

            JiggleDebugGeometry.AppendArrow(vertices, colors, Vector3.zero, vector, Color.magenta);

            for (int i = 2; i < 10; i += 2)
            {
                Vector3 wing = vertices[i];
                Assert.AreEqual(-headLength, Vector3.Dot(wing - vector, direction), 1e-4f);

                Vector3 radial = wing - direction * Vector3.Dot(wing, direction);
                Assert.AreEqual(headWidth, radial.magnitude, 1e-4f);
            }
        }
    }
}
