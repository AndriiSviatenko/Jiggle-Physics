using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerSlideObstacle : MonoBehaviour
    {
        private Transform player;
        private CharacterMover mover;
        private bool hasTriggered;

        private void Start()
        {
            var bridge = Object.FindFirstObjectByType<CharacterBridge>();
            if (bridge != null)
            {
                player = bridge.transform;
                mover = bridge.Mover;
            }
        }

        private void Update()
        {
            if (hasTriggered || player == null || mover == null)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;

            if (Mathf.Abs(delta.x) < transform.localScale.x * 0.6f &&
                Mathf.Abs(delta.z) < 1.0f &&
                delta.y >= -0.5f && delta.y <= 1.8f)
            {
                if (mover.IsSliding || mover.IsCrouching)
                {
                    hasTriggered = true;
                    if (RunnerScoreSystem.Instance != null)
                    {
                        RunnerScoreSystem.Instance.RegisterStunt(mover.IsSliding ? "ПІД СМУГОЮ!" : "ПРОПОВЗАННЯ!", 400);
                    }
                }
                else
                {
                    hasTriggered = true;
                    mover.TriggerStumble(transform.forward);
                    if (RunnerScoreSystem.Instance != null)
                    {
                        RunnerScoreSystem.Instance.ResetComboOnStumble();
                    }
                }
            }
        }
    }
}
