using NUnit.Framework;
using UnityEngine;

namespace JigglePhysics.Tests
{
    public class JiggleMathTests
    {
        [Test]
        public void DampingRetention_ZeroDeltaTime_ReturnsOne()
        {
            float retention = JiggleMath.ComputeDampingRetention(0.5f, 3f, 0f);
            Assert.AreEqual(1f, retention, 1e-5f);
        }

        [Test]
        public void DampingRetention_ZeroDamping_ReturnsOne()
        {
            float retention = JiggleMath.ComputeDampingRetention(0f, 3f, 0.016f);
            Assert.AreEqual(1f, retention, 1e-5f);
        }

        [Test]
        public void DampingRetention_IsFrameRateIndependent()
        {
            float oneStep = JiggleMath.ComputeDampingRetention(0.4f, 3f, 1f / 30f);
            float twoSteps = JiggleMath.ComputeDampingRetention(0.4f, 3f, 1f / 60f);
            twoSteps *= twoSteps;
            Assert.AreEqual(oneStep, twoSteps, 1e-4f);
        }

        [Test]
        public void DampingRetention_NeverNegative()
        {
            float retention = JiggleMath.ComputeDampingRetention(10f, 9f, 0.5f);
            Assert.GreaterOrEqual(retention, 0f);
            Assert.Less(retention, 1f);
        }

        [Test]
        public void ConeConstraint_InsideCone_Unchanged()
        {
            Vector3 animated = Vector3.up;
            Vector3 simulated = Quaternion.Euler(10f, 0f, 0f) * Vector3.up;
            Vector3 result = JiggleMath.ApplyConeConstraint(animated, simulated, 45f);
            Assert.Less(Vector3.Distance(result, simulated.normalized), 1e-4f);
        }

        [Test]
        public void ConeConstraint_OutsideCone_ClampedToMaxAngle()
        {
            Vector3 animated = Vector3.up;
            Vector3 simulated = Quaternion.Euler(90f, 0f, 0f) * Vector3.up;
            Vector3 result = JiggleMath.ApplyConeConstraint(animated, simulated, 30f);
            Assert.AreEqual(30f, Vector3.Angle(animated, result), 0.5f);
        }

        [Test]
        public void SphereResolve_Outside_Unchanged()
        {
            Vector3 point = new Vector3(5f, 0f, 0f);
            Vector3 result = JiggleMath.ResolveSphereCollision(point, 0.1f, Vector3.zero, 1f);
            Assert.AreEqual(point, result);
        }

        [Test]
        public void SphereResolve_Inside_PushedToSurface()
        {
            Vector3 point = new Vector3(0.5f, 0f, 0f);
            Vector3 result = JiggleMath.ResolveSphereCollision(point, 0.1f, Vector3.zero, 1f);
            Assert.AreEqual(1.1f, result.magnitude, 1e-4f);
        }

        [Test]
        public void SphereResolve_AtCenter_PushedOutAlongUp()
        {
            Vector3 result = JiggleMath.ResolveSphereCollision(Vector3.zero, 0f, Vector3.zero, 1f);
            Assert.AreEqual(1f, result.magnitude, 1e-4f);
        }

        [Test]
        public void CapsuleResolve_OffAxisPoint_PushedToSurfaceRadius()
        {
            Vector3 point = new Vector3(0.1f, 0.2f, 0f);
            Vector3 result = JiggleMath.ResolveCapsuleCollision(point, 0f, Vector3.zero, Vector3.up, 0.5f);
            Assert.AreEqual(0.5f, new Vector3(result.x, 0f, result.z).magnitude, 1e-4f);
            Assert.AreEqual(0.2f, result.y, 1e-4f);
        }

        [Test]
        public void CapsuleResolve_PointOnAxis_PushedOutAlongUp()
        {
            Vector3 point = new Vector3(0f, 0.2f, 0f);
            Vector3 result = JiggleMath.ResolveCapsuleCollision(point, 0f, Vector3.zero, Vector3.up, 0.5f);
            Assert.AreEqual(new Vector3(0f, 0.7f, 0f), result);
        }

        [Test]
        public void CapsuleResolve_PointBeyondSegmentEnd_ClampedToEndpoint()
        {
            Vector3 point = new Vector3(0.1f, 1.05f, 0f);
            Vector3 result = JiggleMath.ResolveCapsuleCollision(point, 0f, Vector3.zero, Vector3.up, 0.5f);
            Vector3 offset = result - Vector3.up;
            Assert.AreEqual(0.5f, offset.magnitude, 1e-4f);
        }

        [Test]
        public void CapsuleResolve_OutsideCapsule_Unchanged()
        {
            Vector3 point = new Vector3(3f, 0.5f, 0f);
            Vector3 result = JiggleMath.ResolveCapsuleCollision(point, 0f, Vector3.zero, Vector3.up, 0.5f);
            Assert.AreEqual(point, result);
        }

        [Test]
        public void Sanitize_NaN_ReturnsFallback()
        {
            Vector3 bad = new Vector3(float.NaN, 0f, 0f);
            Vector3 fallback = Vector3.one;
            Assert.AreEqual(fallback, JiggleMath.Sanitize(bad, fallback));
        }

        [Test]
        public void Sanitize_Finite_ReturnsInput()
        {
            Vector3 good = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(good, JiggleMath.Sanitize(good, Vector3.zero));
        }

        [Test]
        public void ClampMagnitudeSafe_OverLimit_Clamped()
        {
            Vector3 value = new Vector3(10f, 0f, 0f);
            Vector3 result = JiggleMath.ClampMagnitudeSafe(value, 0.2f);
            Assert.AreEqual(0.2f, result.magnitude, 1e-5f);
        }

        [Test]
        public void SubstepCount_ShortFrame_UsesSingleSubstep()
        {
            Assert.AreEqual(1, JiggleMath.ComputeSubstepCount(0.5f, 1f / 60f, 4));
        }

        [Test]
        public void SubstepCount_SplitsFrameToStayInsideStabilityLimit()
        {
            int substeps = JiggleMath.ComputeSubstepCount(3f, 1f / 60f, 4);
            float deltaTime = 1f / 60f / substeps;
            Assert.LessOrEqual(deltaTime * JiggleMath.ComputeOmega(3f), JiggleSolverConfig.Active.StabilityFactor + 1e-4f);
        }

        [Test]
        public void SubstepCount_ClampsToMaxSubsteps()
        {
            Assert.AreEqual(4, JiggleMath.ComputeSubstepCount(3f, 1f, 4));
        }

        [Test]
        public void SubstepCount_HighFrequencyNeedsMoreSubsteps()
        {
            int low = JiggleMath.ComputeSubstepCount(1f, 1f / 60f, 8);
            int high = JiggleMath.ComputeSubstepCount(9f, 1f / 60f, 8);
            Assert.Greater(high, low);
        }

        [Test]
        public void SubstepCount_NeverReturnsZero()
        {
            Assert.GreaterOrEqual(JiggleMath.ComputeSubstepCount(3f, 0f, 4), 1);
            Assert.GreaterOrEqual(JiggleMath.ComputeSubstepCount(3f, 1f / 60f, 0), 1);
        }

        [Test]
        public void TeleportThreshold_ScalesWithFrameDuration()
        {
            float at60 = JiggleMath.ComputeTeleportThreshold(0.5f, 1f / 60f);
            float at30 = JiggleMath.ComputeTeleportThreshold(0.5f, 1f / 30f);
            Assert.AreEqual(0.5f, at60, 1e-4f);
            Assert.AreEqual(1f, at30, 1e-4f);
        }

        [Test]
        public void TeleportThreshold_NeverBelowBaseValue()
        {
            Assert.AreEqual(0.5f, JiggleMath.ComputeTeleportThreshold(0.5f, 1f / 240f), 1e-4f);
        }

        [Test]
        public void IsAlive_DetectsDestroyedUnityObject()
        {
            GameObject temp = new GameObject("Temp");
            JiggleSphereCollider collider = temp.AddComponent<JiggleSphereCollider>();

            Assert.IsTrue(JiggleMath.IsAlive(collider));
            Object.DestroyImmediate(temp);
            Assert.IsFalse(JiggleMath.IsAlive(collider));
        }

        [Test]
        public void IsAlive_NullIsNotAlive()
        {
            Assert.IsFalse(JiggleMath.IsAlive(null));
        }

        [Test]
        public void ConeConstraint_AntiParallel_ReturnsDeterministicResult()
        {
            Vector3 animated = Vector3.up;
            Vector3 first = JiggleMath.ApplyConeConstraint(animated, Vector3.down, 30f);
            Vector3 second = JiggleMath.ApplyConeConstraint(animated, Vector3.down, 30f);

            Assert.AreEqual(first, second);
            Assert.AreEqual(30f, Vector3.Angle(animated, first), 0.5f);
        }
    }
}
