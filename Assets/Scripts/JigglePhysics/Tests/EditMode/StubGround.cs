using UnityEngine;

namespace JigglePhysics.Tests
{
    public class StubGround : CharacterLogic.IGroundSource
    {
        public bool IsGrounded { get; set; } = true;
        public Vector3 GroundNormal { get; set; } = Vector3.up;
    }
}
