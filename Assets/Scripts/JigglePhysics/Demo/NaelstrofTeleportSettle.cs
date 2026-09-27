using System.Collections.Generic;
using UnityEngine;
using NaelstrofRig = GatorDragonGames.JigglePhysics.JiggleRig;
using NaelstrofSolver = GatorDragonGames.JigglePhysics.JigglePhysics;

namespace JigglePhysics.Demo
{
    [DefaultExecutionOrder(-100)]
    public sealed class NaelstrofTeleportSettle : MonoBehaviour
    {
        [SerializeField] private float teleportDistance = 2.5f;
        [SerializeField, Min(0f)] private float angularSpeedThreshold = 540f;
        [SerializeField, Min(1)] private int holdFrames = 3;

        private readonly List<NaelstrofRig> rigs = new List<NaelstrofRig>();
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool hasLastPosition;
        private int holdFramesRemaining;

        private void Awake()
        {
            NaelstrofRig[] found = GetComponentsInChildren<NaelstrofRig>(true);
            for (int i = 0; i < found.Length; i++)
            {
                rigs.Add(found[i]);
            }

            lastPosition = transform.position;
            lastRotation = transform.rotation;
            hasLastPosition = true;
        }

        private void LateUpdate()
        {
            Vector3 current = transform.position;
            Quaternion currentRotation = transform.rotation;
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            float angularSpeed = Quaternion.Angle(lastRotation, currentRotation) / deltaTime;
            bool teleported = (current - lastPosition).sqrMagnitude > teleportDistance * teleportDistance;
            bool turnedTooFast = angularSpeedThreshold > 0f && angularSpeed > angularSpeedThreshold;
            if (hasLastPosition && (teleported || turnedTooFast))
            {
                BeginSettle();
            }

            if (holdFramesRemaining > 0)
            {
                holdFramesRemaining--;
                if (holdFramesRemaining == 0)
                {
                    SetRigsEnabled(true);
                }
            }

            lastPosition = current;
            lastRotation = currentRotation;
        }

        private void BeginSettle()
        {
            if (holdFramesRemaining <= 0)
            {
                SetRigsEnabled(false);
                NaelstrofSolver.ScheduleSimulate(Time.timeAsDouble, Time.fixedDeltaTime * 0.05f);
                NaelstrofSolver.SetGlobalDirty();
            }

            holdFramesRemaining = Mathf.Max(1, holdFrames);
        }

        private void SetRigsEnabled(bool value)
        {
            for (int i = 0; i < rigs.Count; i++)
            {
                if (rigs[i] != null)
                {
                    rigs[i].enabled = value;
                }
            }
        }
    }
}
