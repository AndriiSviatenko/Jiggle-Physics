using UnityEngine;
using UnityEngine.InputSystem;

namespace JigglePhysics.Demo
{
    public sealed class JiggleLabCameraRig : MonoBehaviour
    {
        public static readonly string[] ViewNames = { "Огляд", "Моделі", "Пресети" };

        private struct ViewPreset
        {
            public Vector3 Focus;
            public float Yaw;
            public float Pitch;
            public float Distance;
        }

        private static readonly ViewPreset[] Views =
        {
            new ViewPreset { Focus = new Vector3(0.95f, 1.5f, 0.6f), Yaw = 0f, Pitch = 5f, Distance = 8f },
            new ViewPreset { Focus = new Vector3(0f, 1.1f, 0f), Yaw = 0f, Pitch = 5f, Distance = 4.6f },
            new ViewPreset { Focus = new Vector3(0.5f, 2.35f, 1.6f), Yaw = 0f, Pitch = 2f, Distance = 5.8f },
        };

        [SerializeField] private Camera targetCamera;
        [SerializeField] private int initialView = 1;
        [SerializeField] private float orbitSensitivity = 0.2f;
        [SerializeField] private float zoomSensitivity = 0.015f;
        [SerializeField] private float minimumDistance = 1.2f;
        [SerializeField] private float maximumDistance = 20f;

        private int activeView;
        private float yaw;
        private float pitch;
        private float distance;
        private Vector3 focus;
        private bool userControlled;

        public int ActiveView
        {
            get { return activeView; }
        }

        public bool IsUserControlled
        {
            get { return userControlled; }
        }

        public void ApplyView(int index)
        {
            int clamped = Mathf.Clamp(index, 0, Views.Length - 1);
            activeView = clamped;
            focus = Views[clamped].Focus;
            yaw = Views[clamped].Yaw;
            pitch = Views[clamped].Pitch;
            distance = Views[clamped].Distance;
            userControlled = false;
            ApplyTransform();
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            ApplyView(initialView);
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > JiggleMath.Epsilon)
            {
                distance = Mathf.Clamp(distance - scroll * zoomSensitivity, minimumDistance, maximumDistance);
                userControlled = true;
            }

            if (!mouse.middleButton.isPressed)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * orbitSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * orbitSensitivity, -60f, 75f);
            userControlled = true;
        }

        private void LateUpdate()
        {
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (targetCamera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            targetCamera.transform.position = focus - rotation * Vector3.forward * distance;
            targetCamera.transform.rotation = rotation;
        }
    }
}
