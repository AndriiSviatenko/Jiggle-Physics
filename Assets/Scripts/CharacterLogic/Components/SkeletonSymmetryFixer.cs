using UnityEngine;

namespace CharacterLogic
{
    public sealed class SkeletonSymmetryFixer : MonoBehaviour
    {
        private Animator animator;

        public void Configure(Animator characterAnimator)
        {
            animator = characterAnimator;
        }

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Apply()
        {
            if (animator == null)
            {
                return;
            }

            EqualizeBoneLength(HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg);
            EqualizeBoneLength(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg);
            EqualizeBoneLength(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm);
            EqualizeBoneLength(HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm);
        }

        private void EqualizeBoneLength(HumanBodyBones left, HumanBodyBones right)
        {
            Transform leftBone = animator.GetBoneTransform(left);
            Transform rightBone = animator.GetBoneTransform(right);
            if (leftBone == null || rightBone == null || leftBone.childCount == 0 || rightBone.childCount == 0)
            {
                return;
            }

            Transform leftChild = leftBone.GetChild(0);
            Transform rightChild = rightBone.GetChild(0);
            float leftLength = leftChild.localPosition.magnitude;
            float rightLength = rightChild.localPosition.magnitude;
            if (leftLength < 1e-5f || rightLength < 1e-5f)
            {
                return;
            }

            if (Mathf.Abs(leftLength - rightLength) > 1e-5f)
            {
                rightChild.localPosition = rightChild.localPosition * (leftLength / rightLength);
            }
        }
    }
}
