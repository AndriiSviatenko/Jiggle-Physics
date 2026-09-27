using ComponentLogic;
using UnityEngine;

namespace CharacterLogic
{
    public interface IGroundSource : IComponent
    {
        bool IsGrounded { get; }
        Vector3 GroundNormal { get; }
    }
}
