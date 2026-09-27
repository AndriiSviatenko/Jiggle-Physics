using UnityEngine;

namespace JigglePhysics.Demo
{

    public sealed class JiggleLabRigDriver : MonoBehaviour
    {
        [SerializeField] private float bounceFrequency = 2.4f;
        [SerializeField] private float bounceHeight = 0.16f;
        [SerializeField] private float swayAmplitude = 0.22f;
        [SerializeField] private float jumpInterval = 4f;
        [SerializeField] private float jumpHeight = 0.55f;

        private Vector3 origin;
        private float time;

        private void Start()
        {
            origin = transform.position;
        }

        private void Update()
        {
            time += Time.deltaTime;
            float bounce = Mathf.Abs(Mathf.Sin(time * bounceFrequency * Mathf.PI)) * bounceHeight;
            float sway = Mathf.Sin(time * 0.7f) * swayAmplitude;

            float phase = (time % jumpInterval) / jumpInterval;
            float jump = phase < 0.25f ? Mathf.Sin(phase / 0.25f * Mathf.PI) * jumpHeight : 0f;

            transform.position = origin + new Vector3(sway, bounce + jump, 0f);
        }
    }
}
