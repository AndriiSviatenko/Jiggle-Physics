using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerCheckpoint : MonoBehaviour
    {
        [SerializeField] private float radius = 3f;
        [SerializeField] private Renderer glow;
        [SerializeField] private Color idleColor = new Color(0.1f, 0.9f, 2.4f);
        [SerializeField] private Color reachedColor = new Color(0.3f, 2.6f, 0.9f);

        private CharacterMover mover;
        private Transform player;
        private MaterialPropertyBlock block;
        private bool reached;

        public void Configure(Renderer glowRenderer)
        {
            glow = glowRenderer;
        }

        private void Start()
        {
            CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
            if (bridge != null)
            {
                player = bridge.transform;
                mover = bridge.Mover;
            }

            block = new MaterialPropertyBlock();
            ApplyColor(idleColor);
        }

        private void Update()
        {
            if (glow != null)
            {
                glow.transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            }

            if (reached || player == null || mover == null)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;
            if (delta.y < -0.5f || delta.y > 2.5f || new Vector2(delta.x, delta.z).sqrMagnitude > radius * radius)
            {
                return;
            }

            reached = true;
            mover.SetCheckpoint(transform.position + Vector3.up * 0.1f, Quaternion.LookRotation(transform.forward, Vector3.up));
            ApplyColor(reachedColor);
            RunnerScoreSystem.Instance?.RegisterStunt("ЧЕКПОІНТ", 150);
        }

        private void ApplyColor(Color color)
        {
            if (glow == null || block == null)
            {
                return;
            }

            glow.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", color);
            block.SetColor("_BaseColor", color * 0.25f);
            glow.SetPropertyBlock(block);
        }
    }
}
