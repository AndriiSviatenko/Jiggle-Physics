using UnityEngine;

namespace JigglePhysics
{
    public class JiggleSphereCollider : MonoBehaviour, IJiggleCollider
    {
        [SerializeField] private float radius = 0.08f;

        public float Radius
        {
            get { return radius; }
            set { radius = Mathf.Max(0f, value); }
        }

        public bool IsActive
        {
            get { return isActiveAndEnabled; }
        }

        public Vector3 Resolve(Vector3 point, float pointRadius)
        {
            return JiggleMath.ResolveSphereCollision(point, pointRadius, transform.position, radius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
