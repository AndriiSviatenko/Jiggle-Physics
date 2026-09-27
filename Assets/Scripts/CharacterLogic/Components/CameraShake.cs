using UnityEngine;

namespace CharacterLogic
{
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private float positionAmplitude = 0.09f;
        [SerializeField] private float rotationAmplitude = 1.4f;
        [SerializeField] private float decay = 1.7f;

        private float seed;
        private float trauma;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private bool bound;

        private void OnEnable()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            seed = Random.value * 100f;
            Bind();
        }

        private void Start()
        {
            Bind();
        }

        private void Bind()
        {
            if (bound)
            {
                return;
            }

            CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
            CharacterMover mover = bridge != null ? bridge.Mover : null;
            if (mover == null)
            {
                return;
            }

            mover.Landed += OnLanded;
            mover.Jumped += OnAction;
            mover.WallJumped += OnAction;
            mover.Slid += OnAction;
            mover.Stumbled += OnAction;
            bound = true;
        }

        private void OnLanded(float impact)
        {
            AddShake(Mathf.Clamp(impact / 9f, 0.15f, 1f));
        }

        private void OnAction()
        {
            AddShake(0.25f);
        }

        public void AddShake(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void LateUpdate()
        {
            if (trauma <= 0f)
            {
                return;
            }

            trauma = Mathf.Max(0f, trauma - Time.deltaTime * decay);
            float power = trauma * trauma;
            float ox = (Mathf.PerlinNoise(seed, Time.time * 22f) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(seed + 7.3f, Time.time * 22f) - 0.5f) * 2f;
            transform.localPosition = basePosition + new Vector3(ox, oy, 0f) * positionAmplitude * power;
            transform.localRotation = baseRotation * Quaternion.Euler(oy * rotationAmplitude * power, ox * rotationAmplitude * power, ox * rotationAmplitude * 0.6f * power);
        }

        private void OnDestroy()
        {
            if (!bound)
            {
                return;
            }

            CharacterBridge bridge = Object.FindFirstObjectByType<CharacterBridge>();
            CharacterMover mover = bridge != null ? bridge.Mover : null;
            if (mover == null)
            {
                return;
            }

            mover.Landed -= OnLanded;
            mover.Jumped -= OnAction;
            mover.WallJumped -= OnAction;
            mover.Slid -= OnAction;
            mover.Stumbled -= OnAction;
        }
    }
}
