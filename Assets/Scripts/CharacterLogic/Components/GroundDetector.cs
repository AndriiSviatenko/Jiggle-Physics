using ComponentLogic;
using UnityEngine;

namespace CharacterLogic
{
    public class GroundDetector : IGroundSource, ITickableComponent
    {
        private const int MaxHits = 8;
        private const float UngroundedGraceTime = 0.09f;
        private const float ProbeHeight = 0.6f;
        private const float ProbeRadius = 0.08f;

        private readonly CharacterController controller;
        private readonly CharacterControllerConfig config;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxHits];

        private float ungroundedTimer;

        public GroundDetector(CharacterController controller, CharacterControllerConfig config)
        {
            this.controller = controller;
            this.config = config;
        }

        public bool IsGrounded { get; private set; }

        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        public void Tick(float deltaTime)
        {
            bool probeGrounded = false;
            Vector3 probeNormal = Vector3.up;

            Vector3 feet = controller.transform.position + controller.center + Vector3.down * (controller.height * 0.5f);
            Vector3 origin = feet + Vector3.up * ProbeHeight;
            float maxDistance = ProbeHeight + config.GroundCheckRadius * 2f;

            int hitCount = Physics.SphereCastNonAlloc(origin, ProbeRadius, Vector3.down, hitBuffer, maxDistance, config.GroundLayers, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (hit.collider == controller || hit.collider.transform.IsChildOf(controller.transform))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    probeNormal = hit.normal;
                    probeGrounded = true;
                }
            }

            if (!probeGrounded && controller.isGrounded)
            {
                probeGrounded = true;
                probeNormal = Vector3.up;
            }

            if (probeGrounded)
            {
                ungroundedTimer = 0f;
                IsGrounded = true;
                GroundNormal = probeNormal;
                return;
            }

            ungroundedTimer += deltaTime;
            if (ungroundedTimer >= UngroundedGraceTime)
            {
                IsGrounded = false;
                GroundNormal = Vector3.up;
            }
        }
    }
}
