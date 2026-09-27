using UnityEngine;

namespace CharacterLogic
{

    public sealed class RunnerGem : MonoBehaviour
    {
        private Transform player;
        private Vector3 startPosition;
        private Vector3 baseScale;
        private float hoverOffset;
        private bool collected;
        private static Material gemMaterial;

        public static void SetSharedMaterial(Material mat)
        {
            gemMaterial = mat;
        }

        private void Start()
        {
            startPosition = transform.position;
            baseScale = transform.localScale;
            hoverOffset = Random.Range(0f, Mathf.PI * 2f);

            var bridge = Object.FindFirstObjectByType<CharacterBridge>();
            if (bridge != null)
            {
                player = bridge.transform;
            }

            if (gemMaterial != null)
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = gemMaterial;
                }
            }
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * 6f + hoverOffset) * 0.12f);
            float bob = Mathf.Sin(Time.time * 3f + hoverOffset) * 0.12f;
            transform.position = startPosition + Vector3.up * bob;

            if (player == null)
            {
                return;
            }

            Vector3 toPlayer = player.position + Vector3.up * 1f - transform.position;
            float sqrDist = toPlayer.sqrMagnitude;

            if (sqrDist < 20.25f && sqrDist > 1.2f)
            {
                transform.position = Vector3.MoveTowards(transform.position, player.position + Vector3.up * 1f, 12f * Time.deltaTime);
            }

            if (sqrDist < 1.96f)
            {
                Collect();
            }
        }

        private void Collect()
        {
            collected = true;

            if (RunnerScoreSystem.Instance != null)
            {
                RunnerScoreSystem.Instance.CollectGem(100);
            }

            SpawnPickupEffect(transform.position);

            Destroy(gameObject);
        }

        private void SpawnPickupEffect(Vector3 pos)
        {
            GameObject fx = new GameObject("Gem Sparkle");
            fx.transform.position = pos;
            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 0.35f;
            main.startSize = 0.12f;
            main.startSpeed = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = false;
            ps.Emit(18);

            var rend = fx.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = ParkourEffectMaterials.Get(new Color(2.6f, 2.1f, 0.3f, 1f));
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
