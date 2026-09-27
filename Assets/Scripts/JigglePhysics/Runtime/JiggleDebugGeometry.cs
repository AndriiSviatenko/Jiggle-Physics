using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics
{
    public static class JiggleDebugGeometry
    {
        public const int DefaultCircleSegments = 32;

        public static void AppendLine(List<Vector3> vertices, List<Color> colors, Vector3 from, Vector3 to, Color color)
        {
            if (vertices == null || colors == null || !IsDrawable(from) || !IsDrawable(to))
            {
                return;
            }

            vertices.Add(from);
            colors.Add(color);
            vertices.Add(to);
            colors.Add(color);
        }

        public static void AppendCircle(List<Vector3> vertices, List<Color> colors, Vector3 center, Vector3 normal, float radius, Color color, int segments = DefaultCircleSegments)
        {
            if (vertices == null || colors == null || radius <= JiggleMath.Epsilon)
            {
                return;
            }

            int safeSegments = Mathf.Clamp(segments, 3, 256);
            ResolveBasis(normal, out Vector3 tangent, out Vector3 bitangent);

            Vector3 first = center + tangent * radius;
            Vector3 previous = first;
            for (int i = 1; i <= safeSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / safeSegments;
                Vector3 point = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
                AppendLine(vertices, colors, previous, point, color);
                previous = point;
            }

            AppendLine(vertices, colors, previous, first, color);
        }

        public static void AppendCone(List<Vector3> vertices, List<Color> colors, Vector3 apex, Vector3 direction, float length, float angleDegrees, Color ringColor, Color rayColor, int segments = DefaultCircleSegments, int rayCount = 4)
        {
            if (vertices == null || colors == null || length <= JiggleMath.Epsilon)
            {
                return;
            }

            Vector3 axis = direction.sqrMagnitude > JiggleMath.Epsilon ? direction.normalized : Vector3.up;
            float angleRadians = Mathf.Clamp(angleDegrees, 0f, 179.5f) * Mathf.Deg2Rad;
            Vector3 ringCenter = apex + axis * (length * Mathf.Cos(angleRadians));
            float ringRadius = length * Mathf.Sin(angleRadians);

            AppendCircle(vertices, colors, ringCenter, axis, ringRadius, ringColor, segments);

            if (rayCount <= 0 || ringRadius <= JiggleMath.Epsilon)
            {
                return;
            }

            ResolveBasis(axis, out Vector3 tangent, out Vector3 bitangent);
            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Vector3 point = ringCenter + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * ringRadius;
                AppendLine(vertices, colors, apex, point, rayColor);
            }
        }

        public static void AppendRibbon(List<Vector3> vertices, List<Color> colors, List<int> indices, Vector3 from, Vector3 to, Vector3 viewDirection, float width, Color color, Color endColor)
        {
            if (vertices == null || colors == null || indices == null || !IsDrawable(from) || !IsDrawable(to) || width <= JiggleMath.Epsilon)
            {
                return;
            }

            Vector3 segment = to - from;
            if (segment.sqrMagnitude < JiggleMath.Epsilon)
            {
                return;
            }

            Vector3 side = Vector3.Cross(segment.normalized, viewDirection);
            if (side.sqrMagnitude < JiggleMath.Epsilon)
            {
                side = Vector3.Cross(segment.normalized, Vector3.up);
                if (side.sqrMagnitude < JiggleMath.Epsilon)
                {
                    side = Vector3.Cross(segment.normalized, Vector3.right);
                }
            }

            side = side.normalized * (width * 0.5f);
            int startIndex = vertices.Count;
            vertices.Add(from - side);
            colors.Add(color);
            vertices.Add(from + side);
            colors.Add(color);
            vertices.Add(to - side);
            colors.Add(endColor);
            vertices.Add(to + side);
            colors.Add(endColor);
            indices.Add(startIndex);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 1);
            indices.Add(startIndex + 2);
            indices.Add(startIndex + 3);
        }

        public static void AppendArrow(List<Vector3> vertices, List<Color> colors, Vector3 origin, Vector3 vector, Color color, float headLengthRatio = 0.3f, float headWidthRatio = 0.35f)
        {
            if (vertices == null || colors == null || vector.sqrMagnitude < JiggleMath.Epsilon)
            {
                return;
            }

            float length = vector.magnitude;
            Vector3 direction = vector / length;
            Vector3 tip = origin + vector;
            AppendLine(vertices, colors, origin, tip, color);

            float headLength = length * Mathf.Clamp(headLengthRatio, 0.05f, 0.6f);
            float headWidth = headLength * Mathf.Clamp(headWidthRatio, 0.05f, 1.5f);
            Vector3 baseCenter = tip - direction * headLength;

            ResolveBasis(direction, out Vector3 tangent, out Vector3 bitangent);
            AppendLine(vertices, colors, baseCenter + tangent * headWidth, tip, color);
            AppendLine(vertices, colors, baseCenter - tangent * headWidth, tip, color);
            AppendLine(vertices, colors, baseCenter + bitangent * headWidth, tip, color);
            AppendLine(vertices, colors, baseCenter - bitangent * headWidth, tip, color);
        }

        private static void ResolveBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            Vector3 axis = normal.sqrMagnitude > JiggleMath.Epsilon ? normal.normalized : Vector3.up;
            Vector3 reference = Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right;
            tangent = Vector3.Cross(axis, reference);
            if (tangent.sqrMagnitude < JiggleMath.Epsilon)
            {
                tangent = Vector3.right;
            }

            tangent.Normalize();
            bitangent = Vector3.Cross(axis, tangent).normalized;
        }

        private static bool IsDrawable(Vector3 point)
        {
            return JiggleMath.IsFinite(point);
        }
    }
}
