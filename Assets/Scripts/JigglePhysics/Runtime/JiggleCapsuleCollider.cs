using UnityEngine;

namespace JigglePhysics
{
    public class JiggleCapsuleCollider : MonoBehaviour, IJiggleCollider
    {
        [SerializeField] private float radius = 0.06f;

        [Tooltip("Capsule axis in local space, normalized.")]
        [SerializeField] private Vector3 direction = Vector3.up;

        [Tooltip("Total capsule length including both hemisphere caps.")]
        [SerializeField] private float height = 0.3f;

        public float Radius
        {
            get { return radius; }
            set { radius = Mathf.Max(0f, value); }
        }

        public float Height
        {
            get { return height; }
            set { height = Mathf.Max(0f, value); }
        }

        public Vector3 Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        public bool IsActive
        {
            get { return isActiveAndEnabled; }
        }

        public Vector3 Resolve(Vector3 point, float pointRadius)
        {
            Vector3 axis = transform.rotation * GetNormalizedDirection();
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 center = transform.position;
            Vector3 start = center - axis * halfSegment;
            Vector3 end = center + axis * halfSegment;
            return JiggleMath.ResolveCapsuleCollision(point, pointRadius, start, end, radius);
        }

        private Vector3 GetNormalizedDirection()
        {
            if (direction.sqrMagnitude < JiggleMath.Epsilon)
            {
                return Vector3.up;
            }
            return direction.normalized;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0f, radius);
            height = Mathf.Max(0f, height);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Vector3 axis = transform.rotation * GetNormalizedDirection();
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 center = transform.position;
            Gizmos.DrawWireSphere(center - axis * halfSegment, radius);
            Gizmos.DrawWireSphere(center + axis * halfSegment, radius);
            Gizmos.DrawLine(center - axis * halfSegment, center + axis * halfSegment);
        }
    }
}
