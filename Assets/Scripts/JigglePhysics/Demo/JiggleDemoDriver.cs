using UnityEngine;

namespace JigglePhysics.Demo
{
    public class JiggleDemoDriver : MonoBehaviour
    {
        [SerializeField] private bool bounce = true;

        [Tooltip("Vertical bounce amplitude in meters.")]
        [SerializeField] private float bounceAmplitude = 0.06f;

        [Tooltip("Bounce frequency in Hz. 2.5-3 Hz mimics a running cadence.")]
        [SerializeField] private float bounceFrequency = 2.5f;

        [SerializeField] private bool sway = true;

        [Tooltip("Forward/back sway amplitude in meters.")]
        [SerializeField] private float swayAmplitude = 0.03f;

        [SerializeField] private float swayFrequency = 0.8f;

        [Tooltip("Rotate the character continuously, in degrees per second.")]
        [SerializeField] private float turnSpeed = 0f;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float elapsed;

        private void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            Vector3 offset = Vector3.zero;
            if (bounce)
            {
                offset.y = Mathf.Abs(Mathf.Sin(elapsed * bounceFrequency * Mathf.PI)) * bounceAmplitude;
            }

            if (sway)
            {
                offset.z = Mathf.Sin(elapsed * swayFrequency * 2f * Mathf.PI) * swayAmplitude;
            }

            transform.position = startPosition + offset;

            if (Mathf.Abs(turnSpeed) > JiggleMath.Epsilon)
            {
                transform.rotation = startRotation * Quaternion.Euler(0f, elapsed * turnSpeed, 0f);
            }
        }

        public void ResetMotion()
        {
            elapsed = 0f;
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
    }
}
