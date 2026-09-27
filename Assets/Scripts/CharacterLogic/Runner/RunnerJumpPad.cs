using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerJumpPad : MonoBehaviour
    {
        [SerializeField] private float launchVelocity = 14f;
        [SerializeField] private float forwardBoost = 6f;

        private CharacterMover mover;
        private Transform player;
        private float cooldownTimer;

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
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            if (player == null || mover == null)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;
            if (Mathf.Abs(delta.x) < transform.localScale.x * 0.65f &&
                Mathf.Abs(delta.z) < transform.localScale.z * 0.65f &&
                delta.y >= -0.2f && delta.y <= 1.2f)
            {
                cooldownTimer = 1.2f;
                mover.Launch(launchVelocity, forwardBoost);
                if (RunnerScoreSystem.Instance != null)
                {
                    RunnerScoreSystem.Instance.RegisterStunt("СУПЕР СТРИБОК!", 250);
                }
            }
        }
    }
}
