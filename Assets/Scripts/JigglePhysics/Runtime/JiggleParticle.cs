using UnityEngine;

namespace JigglePhysics
{
    public struct JiggleParticle
    {
        public Transform Bone;
        public Quaternion RestLocalRotation;
        public Vector3 RestTipLocalOffset;
        public float Length;
        public Vector3 Position;
        public Vector3 PreviousPosition;
        public Vector3 Velocity;

        public void Snap(Vector3 position)
        {
            Position = position;
            PreviousPosition = position;
            Velocity = Vector3.zero;
        }

        public void VerletIntegrate(Vector3 acceleration, float dampingRetention, float deltaTime, float stepRatio = 1f)
        {
            Vector3 velocity = (Position - PreviousPosition) * (dampingRetention * stepRatio);
            Vector3 next = Position + velocity + acceleration * (deltaTime * deltaTime);
            PreviousPosition = Position;
            Position = next;
        }

        public void SpringIntegrate(Vector3 acceleration, float velocityRetention, float deltaTime)
        {
            Velocity *= velocityRetention;
            Velocity += acceleration * deltaTime;
            Position += Velocity * deltaTime;
        }

        public void TranslateHistory(Vector3 delta)
        {
            Position += delta;
            PreviousPosition += delta;
        }

        public void RotateHistory(Vector3 pivot, Quaternion rotation)
        {
            Position = pivot + rotation * (Position - pivot);
            PreviousPosition = pivot + rotation * (PreviousPosition - pivot);
        }
    }
}
