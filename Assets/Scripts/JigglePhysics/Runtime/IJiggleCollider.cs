using UnityEngine;

namespace JigglePhysics
{
    public interface IJiggleCollider
    {
        bool IsActive { get; }
        Vector3 Resolve(Vector3 point, float pointRadius);
    }
}
