using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerBoostPad : MonoBehaviour
    {
        [SerializeField] private float boostSpeed = 16f;
        [SerializeField] private float boostDuration = 2.2f;

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
            if (Mathf.Abs(delta.x) < transform.localScale.x * 0.7f &&
                Mathf.Abs(delta.z) < transform.localScale.z * 0.7f &&
                delta.y >= -0.2f && delta.y <= 1.2f)
            {
                ActivateBoost();
            }
        }

        private void ActivateBoost()
        {
            cooldownTimer = 1.0f;
            Vector3 boostDirection = transform.forward;
            if (boostDirection.sqrMagnitude < 0.1f)
            {
                boostDirection = Vector3.forward;
            }

            mover.ApplySpeedBoost(boostDirection, boostSpeed, boostDuration);

            if (RunnerScoreSystem.Instance != null)
            {
                RunnerScoreSystem.Instance.RegisterStunt("НІТРО БУСТ!", 300);
            }
        }
    }
}
