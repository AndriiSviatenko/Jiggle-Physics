using UnityEngine;

namespace CharacterLogic
{

    public sealed class FootPlantIK : MonoBehaviour
    {
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float footOffset = 0.06f;
        [SerializeField] private float maxSpeedForPlanting = 0.4f;
        [SerializeField] private float weightResponse = 8f;
        [SerializeField] private float maxCorrection = 0.35f;

        private Animator animator;
        private CharacterMover mover;
        private float weight;

        public void Configure(Animator characterAnimator, CharacterMover characterMover)
        {
            animator = characterAnimator;
            mover = characterMover;
        }

        private void Update()
        {
            if (mover == null)
            {
                return;
            }

            bool plant = mover.IsGrounded
                && !mover.IsWallRunning
                && !mover.IsSliding
                && !mover.IsFlipping
                && !mover.IsStumbling
                && mover.NormalizedSpeed < maxSpeedForPlanting;
            weight = Mathf.MoveTowards(weight, plant ? 1f : 0f, weightResponse * Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || mover == null)
            {
                return;
            }

            ApplyFoot(AvatarIKGoal.LeftFoot);
            ApplyFoot(AvatarIKGoal.RightFoot);
        }

        private void ApplyFoot(AvatarIKGoal goal)
        {
            if (weight <= 0.001f)
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            Vector3 animatedPosition = animator.GetIKPosition(goal);
            Quaternion animatedRotation = animator.GetIKRotation(goal);
            Vector3 origin = animatedPosition + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.5f + maxCorrection, groundMask, QueryTriggerInteraction.Ignore)
                || hit.collider.transform.IsChildOf(transform.root))
            {
                animator.SetIKPositionWeight(goal, 0f);
                animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            float correction = Mathf.Clamp(hit.point.y - transform.root.position.y, -maxCorrection, maxCorrection);
            Vector3 target = animatedPosition;
            target.y = Mathf.Max(animatedPosition.y + correction, hit.point.y + footOffset);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, hit.normal);

            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight * 0.7f);
            animator.SetIKPosition(goal, target);
            animator.SetIKRotation(goal, slope * animatedRotation);
        }
    }
}
