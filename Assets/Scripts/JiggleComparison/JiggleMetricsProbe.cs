using UnityEngine;

namespace JiggleComparison
{

    [DefaultExecutionOrder(10100)]
    public sealed class JiggleMetricsProbe : MonoBehaviour
    {
        public struct Report
        {
            public int Frames;
            public float AverageCostMilliseconds;
            public float PeakCostMilliseconds;
            public float PeakDeviationMillimeters;
            public float RmsDeviationMillimeters;
            public float RmsAcceleration;
        }

        private const float SmoothingTime = 1.5f;
        private const float PeakHoldTime = 2f;

        private JiggleBoneSet bones;
        private IJiggleBackend source;
        private Vector3[] restTipLocal = new Vector3[0];
        private Vector3[] previousTip = new Vector3[0];
        private Vector3[] previousVelocity = new Vector3[0];
        private int warmupFrames;

        private float peakHoldTimer;
        private float meanSquareDeviation;
        private float meanSquareAcceleration;
        private float smoothedCost;

        private bool recording;
        private Report accumulated;
        private double costSum;
        private double deviationSquareSum;
        private double accelerationSquareSum;

        public float CurrentDeviationMillimeters { get; private set; }
        public float PeakDeviationMillimeters { get; private set; }

        public float RmsDeviationMillimeters
        {
            get { return Mathf.Sqrt(meanSquareDeviation); }
        }

        public float RmsAcceleration
        {
            get { return Mathf.Sqrt(meanSquareAcceleration); }
        }

        public float SmoothedCostMilliseconds
        {
            get { return smoothedCost; }
        }

        public void Configure(JiggleBoneSet boneSet)
        {
            bones = boneSet;
            restTipLocal = new Vector3[bones.Count];
            previousTip = new Vector3[bones.Count];
            previousVelocity = new Vector3[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                Transform parent = bones.GetRoot(i).parent;
                restTipLocal[i] = parent.InverseTransformPoint(bones.GetTip(i).position);
            }

            ResetStatistics();
        }

        public void SetSource(IJiggleBackend backend)
        {
            source = backend;
            ResetStatistics();
        }

        public void ResetStatistics()
        {
            warmupFrames = 2;
            peakHoldTimer = 0f;
            PeakDeviationMillimeters = 0f;
            CurrentDeviationMillimeters = 0f;
            meanSquareDeviation = 0f;
            meanSquareAcceleration = 0f;
            smoothedCost = 0f;
        }

        public void BeginRecording()
        {
            recording = true;
            accumulated = new Report();
            costSum = 0d;
            deviationSquareSum = 0d;
            accelerationSquareSum = 0d;
            ResetStatistics();
        }

        public Report EndRecording()
        {
            recording = false;
            Report report = accumulated;
            if (report.Frames > 0)
            {
                report.AverageCostMilliseconds = (float)(costSum / report.Frames);
                report.RmsDeviationMillimeters = (float)System.Math.Sqrt(deviationSquareSum / report.Frames);
                report.RmsAcceleration = (float)System.Math.Sqrt(accelerationSquareSum / report.Frames);
            }

            return report;
        }

        private void LateUpdate()
        {
            if (bones == null || bones.Count == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            float deviation = 0f;
            float acceleration = 0f;
            for (int i = 0; i < bones.Count; i++)
            {
                Transform parent = bones.GetRoot(i).parent;
                Vector3 tipLocal = parent.InverseTransformPoint(bones.GetTip(i).position);
                deviation = Mathf.Max(deviation, Vector3.Distance(tipLocal, restTipLocal[i]));

                Vector3 velocity = (tipLocal - previousTip[i]) / deltaTime;
                if (warmupFrames <= 0)
                {
                    acceleration = Mathf.Max(acceleration, ((velocity - previousVelocity[i]) / deltaTime).magnitude);
                }

                previousVelocity[i] = velocity;
                previousTip[i] = tipLocal;
            }

            if (warmupFrames > 0)
            {
                warmupFrames--;
                return;
            }

            float deviationMillimeters = deviation * 1000f;
            float cost = source != null ? source.LastCostMilliseconds : 0f;
            CurrentDeviationMillimeters = deviationMillimeters;

            peakHoldTimer -= deltaTime;
            if (deviationMillimeters >= PeakDeviationMillimeters || peakHoldTimer <= 0f)
            {
                PeakDeviationMillimeters = deviationMillimeters;
                peakHoldTimer = PeakHoldTime;
            }

            float blend = 1f - Mathf.Exp(-deltaTime / SmoothingTime);
            meanSquareDeviation = Mathf.Lerp(meanSquareDeviation, deviationMillimeters * deviationMillimeters, blend);
            meanSquareAcceleration = Mathf.Lerp(meanSquareAcceleration, acceleration * acceleration, blend);
            smoothedCost = Mathf.Lerp(smoothedCost, cost, blend);

            if (recording)
            {
                accumulated.Frames++;
                accumulated.PeakDeviationMillimeters = Mathf.Max(accumulated.PeakDeviationMillimeters, deviationMillimeters);
                accumulated.PeakCostMilliseconds = Mathf.Max(accumulated.PeakCostMilliseconds, cost);
                costSum += cost;
                deviationSquareSum += deviationMillimeters * deviationMillimeters;
                accelerationSquareSum += acceleration * acceleration;
            }
        }
    }
}
