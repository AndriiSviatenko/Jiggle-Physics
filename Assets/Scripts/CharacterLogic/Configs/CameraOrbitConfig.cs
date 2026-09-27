using UnityEngine;

namespace CharacterLogic
{
    [CreateAssetMenu(fileName = "CameraOrbitConfig", menuName = "Character/Camera Orbit Config", order = 1)]
    public class CameraOrbitConfig : ScriptableObject
    {
        [Tooltip("Horizontal orbit speed for gamepad stick input in degrees per second.")]
        [SerializeField] private float horizontalSpeed = 150f;

        [Tooltip("Vertical orbit speed for gamepad stick input in degrees per second.")]
        [SerializeField] private float verticalSpeed = 120f;

        [Tooltip("Mouse sensitivity in degrees per pixel.")]
        [SerializeField] private float mouseSensitivity = 0.15f;

        [Tooltip("Smoothing time for camera orbit rotation in seconds (0 = instant).")]
        [SerializeField] private float mouseSmoothTime = 0.04f;

        [Tooltip("Lowest allowed vertical orbit angle (looking up), in degrees.")]
        [SerializeField] private float minPitch = -25f;

        [Tooltip("Highest allowed vertical orbit angle (looking down), in degrees.")]
        [SerializeField] private float maxPitch = 60f;

        [Tooltip("Default vertical orbit angle on start, in degrees.")]
        [SerializeField] private float defaultPitch = 10f;

        [Tooltip("Distance from the follow target to the camera, in meters.")]
        [SerializeField] private float radius = 2.6f;

        [Tooltip("Minimum camera distance when zoomed in, in meters.")]
        [SerializeField] private float minRadius = 1.4f;

        [Tooltip("Maximum camera distance when zoomed out, in meters.")]
        [SerializeField] private float maxRadius = 4.5f;

        [Tooltip("Distance change per scroll wheel tick, in meters.")]
        [SerializeField] private float zoomStep = 0.4f;

        [Tooltip("Invert vertical mouse/stick look.")]
        [SerializeField] private bool invertY = false;

        [Tooltip("Lateral and vertical shoulder offset from target (X = right shoulder, Y = vertical).")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.2f, 0f, 0f);

        [Tooltip("Vertical offset of the orbit focus from the target center, in meters.")]
        [SerializeField] private float targetHeightOffset = 0f;

        [Header("Dynamic camera")]
        [SerializeField] private float normalFov = 58f;
        [SerializeField] private float sprintFov = 66f;
        [SerializeField] private float wallRunFov = 72f;
        [SerializeField] private float fovResponse = 7f;

        public float HorizontalSpeed => horizontalSpeed;
        public float VerticalSpeed => verticalSpeed;
        public float MouseSensitivity => mouseSensitivity;
        public float MouseSmoothTime => mouseSmoothTime;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float DefaultPitch => defaultPitch;
        public float Radius => radius;
        public float MinRadius => minRadius;
        public float MaxRadius => maxRadius;
        public float ZoomStep => zoomStep;
        public bool InvertY => invertY;
        public Vector3 ShoulderOffset => shoulderOffset;
        public float TargetHeightOffset => targetHeightOffset;
        public float NormalFov => normalFov;
        public float SprintFov => sprintFov;
        public float WallRunFov => wallRunFov;
        public float FovResponse => fovResponse;

        private void OnValidate()
        {
            horizontalSpeed = Mathf.Max(0f, horizontalSpeed);
            verticalSpeed = Mathf.Max(0f, verticalSpeed);
            mouseSensitivity = Mathf.Max(0.001f, mouseSensitivity);
            mouseSmoothTime = Mathf.Max(0f, mouseSmoothTime);
            if (minPitch > maxPitch)
            {
                float swap = minPitch;
                minPitch = maxPitch;
                maxPitch = swap;
            }

            minRadius = Mathf.Max(0.5f, minRadius);
            maxRadius = Mathf.Max(minRadius, maxRadius);
            radius = Mathf.Clamp(radius, minRadius, maxRadius);
            zoomStep = Mathf.Max(0.01f, zoomStep);
            normalFov = Mathf.Clamp(normalFov, 35f, 100f);
            sprintFov = Mathf.Clamp(sprintFov, normalFov, 110f);
            wallRunFov = Mathf.Clamp(wallRunFov, sprintFov, 120f);
            fovResponse = Mathf.Max(0.1f, fovResponse);
        }
    }
}
