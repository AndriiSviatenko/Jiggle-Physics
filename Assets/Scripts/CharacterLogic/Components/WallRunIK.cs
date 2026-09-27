using UnityEngine;

namespace CharacterLogic
{
    public sealed class WallRunIK : MonoBehaviour
    {
        [SerializeField] private float handReach = 0.26f;
        [SerializeField] private float handDrop = 0.12f;
        [SerializeField] private float weightResponse = 7f;

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
            float target = mover != null && mover.IsWallRunning ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, Mathf.Max(0.01f, weightResponse) * Time.deltaTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || mover == null)
            {
                return;
            }

            bool left = mover.State == ParkourState.WallRunLeft;
            AvatarIKGoal goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            animator.SetIKPositionWeight(goal, weight);

            if (weight <= 0.001f)
            {
                return;
            }

            Transform shoulder = animator.GetBoneTransform(left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
            Vector3 shoulderPosition = shoulder != null ? shoulder.position : animator.transform.position + Vector3.up * 1.2f;
            Vector3 normal = mover.WallNormal;
            if (normal.sqrMagnitude < 0.001f)
            {
                normal = left ? -animator.transform.right : animator.transform.right;
            }

            Vector3 contact = mover.WallContactPoint;
            float planeDistance = Vector3.Dot(contact - shoulderPosition, normal);
            Vector3 target = shoulderPosition + normal * planeDistance;

            Vector3 along = mover.HorizontalVelocity;
            along.y = 0f;
            if (along.sqrMagnitude > 0.01f)
            {
                target += along.normalized * handReach;
            }

            target.y = shoulderPosition.y - handDrop;
            animator.SetIKPosition(goal, target);
        }
    }
}
