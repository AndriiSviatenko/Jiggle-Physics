using System.Collections.Generic;
using JigglePhysics;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterLogic
{
    [DefaultExecutionOrder(-100)]
    public sealed class BreastShowcaseCamera : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera showcaseCamera;
        [SerializeField] private CinemachineTargetGroup targetGroup;
        [SerializeField] private Transform[] breastBones = new Transform[0];
        [SerializeField] private bool startActive;
        [SerializeField] private int activePriority = 1000;
        [SerializeField] private int idlePriority = -1000;
        [SerializeField] private float radius = 0.95f;
        [SerializeField] private float fieldOfView = 42f;
        [SerializeField] private Key toggleKey = Key.F8;
        [SerializeField] private bool showHud = true;
        [SerializeField] private bool showPictureInPicture = true;
        [SerializeField] private float pictureHeight = 0.26f;

        private Camera pictureCamera;
        private Transform anchor;
        private bool active;

        public bool IsActive
        {
            get { return active; }
        }

        public void Configure(JiggleRig rig)
        {
            if (rig == null)
            {
                return;
            }

            List<Transform> roots = new List<Transform>();
            for (int i = 0; i < rig.ChainCount; i++)
            {
                JiggleChain chain = rig.GetChain(i);
                Transform bone = chain != null ? chain.GetBone(0) : null;
                if (bone != null && !roots.Contains(bone))
                {
                    roots.Add(bone);
                }
            }

            if (roots.Count == 0)
            {
                Debug.LogWarning("[BreastShowcaseCamera] No breast chains found on the rig; showcase camera disabled.", this);
                enabled = false;
                return;
            }

            breastBones = roots.ToArray();
            Build();
            SetActive(startActive);
        }

        public void SetActive(bool value)
        {
            active = value;
            if (showcaseCamera != null)
            {
                showcaseCamera.Priority = value ? activePriority : idlePriority;
            }

            if (pictureCamera != null)
            {
                pictureCamera.enabled = !value && showPictureInPicture;
            }
        }

        public void Toggle()
        {
            SetActive(!active);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || showcaseCamera == null)
            {
                return;
            }

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void LateUpdate()
        {
            UpdateAnchor();
            UpdatePictureCamera();
        }

        private void OnGUI()
        {
            if (showcaseCamera == null || !showHud)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, Screen.height - 72f, 250f, 56f), GUI.skin.box);
            string label = active ? "ХОВАТИ КАМЕРУ ГРУДЕЙ (F8)" : "ПОКАЗАТИ КАМЕРУ ГРУДЕЙ (F8)";
            if (GUILayout.Button(label))
            {
                Toggle();
            }
            GUILayout.EndArea();
        }

        private void Build()
        {
            EnsureTargetGroup();
            EnsureAnchor();
            EnsureCamera();
            EnsurePictureCamera();
        }

        private void EnsureTargetGroup()
        {
            if (targetGroup == null)
            {
                GameObject groupObject = new GameObject("Breast Camera Target");
                groupObject.transform.SetParent(transform, false);
                targetGroup = groupObject.AddComponent<CinemachineTargetGroup>();
            }

            targetGroup.Targets.Clear();
            for (int i = 0; i < breastBones.Length; i++)
            {
                if (breastBones[i] != null)
                {
                    targetGroup.AddMember(breastBones[i], 1f, 0.12f);
                }
            }
        }

        private void EnsureAnchor()
        {
            if (anchor != null)
            {
                return;
            }

            anchor = new GameObject("Breast Camera Anchor").transform;
            anchor.SetParent(transform, false);
            UpdateAnchor();
        }

        private void UpdateAnchor()
        {
            if (anchor != null)
            {
                anchor.SetPositionAndRotation(GetBreastCenter(), transform.rotation);
            }
        }

        private void EnsureCamera()
        {
            if (showcaseCamera == null)
            {
                GameObject cameraObject = new GameObject("Breast Showcase Camera");
                cameraObject.transform.SetParent(transform, false);
                showcaseCamera = cameraObject.AddComponent<CinemachineCamera>();
            }

            showcaseCamera.Follow = anchor;
            showcaseCamera.LookAt = anchor;
            showcaseCamera.Priority = idlePriority;

            LensSettings lens = showcaseCamera.Lens;
            lens.FieldOfView = fieldOfView;
            showcaseCamera.Lens = lens;

            CinemachineOrbitalFollow orbital = showcaseCamera.GetComponent<CinemachineOrbitalFollow>();
            if (orbital == null)
            {
                orbital = showcaseCamera.gameObject.AddComponent<CinemachineOrbitalFollow>();
            }

            orbital.Radius = radius;
            orbital.TargetOffset = Vector3.zero;
            orbital.HorizontalAxis.Value = 180f;
            orbital.VerticalAxis.Value = 12f;
            Unity.Cinemachine.TargetTracking.TrackerSettings tracker = orbital.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            tracker.PositionDamping = Vector3.zero;
            tracker.RotationDamping = Vector3.zero;
            orbital.TrackerSettings = tracker;

            CinemachineRotationComposer composer = showcaseCamera.GetComponent<CinemachineRotationComposer>();
            if (composer == null)
            {
                composer = showcaseCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            }

            composer.Damping = Vector2.zero;
        }

        private void EnsurePictureCamera()
        {
            if (pictureCamera != null)
            {
                return;
            }

            GameObject pipObject = new GameObject("Breast PiP Camera");
            pipObject.transform.SetParent(transform, false);
            pictureCamera = pipObject.AddComponent<Camera>();
            pictureCamera.fieldOfView = fieldOfView;
            pictureCamera.nearClipPlane = 0.03f;
            pictureCamera.farClipPlane = 50f;
            pictureCamera.clearFlags = CameraClearFlags.SolidColor;
            pictureCamera.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            pictureCamera.depth = 10f;
            pictureCamera.enabled = showPictureInPicture && !active;

            GameObject keyLightObject = new GameObject("Breast Showcase Key Light");
            keyLightObject.transform.SetParent(pipObject.transform, false);
            keyLightObject.transform.localPosition = new Vector3(0.28f, 0.3f, 0.1f);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.color = new Color(1f, 0.96f, 0.92f);
            keyLight.range = 2.2f;
            keyLight.intensity = 2.6f;
            keyLight.shadows = LightShadows.None;

            GameObject fillLightObject = new GameObject("Breast Showcase Fill Light");
            fillLightObject.transform.SetParent(pipObject.transform, false);
            fillLightObject.transform.localPosition = new Vector3(-0.32f, -0.12f, 0.15f);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.72f, 0.82f, 1f);
            fillLight.range = 2.2f;
            fillLight.intensity = 1.2f;
            fillLight.shadows = LightShadows.None;
        }

        private void UpdatePictureCamera()
        {
            if (pictureCamera == null)
            {
                return;
            }

            if (!pictureCamera.enabled)
            {
                return;
            }

            Vector3 center = GetBreastCenter();
            Vector3 forward = transform.forward;
            Vector3 up = transform.up;
            pictureCamera.transform.position = center + forward * 1.35f + up * 0.12f;
            pictureCamera.transform.LookAt(center - up * 0.02f, up);

            float height = Mathf.Clamp(pictureHeight, 0.12f, 0.5f);
            float width = height * (float)Screen.height / Mathf.Max(1, Screen.width);
            pictureCamera.rect = new Rect(1f - width - 0.02f, 0.02f, width, height);
        }

        private Vector3 GetBreastCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < breastBones.Length; i++)
            {
                if (breastBones[i] != null)
                {
                    sum += breastBones[i].position;
                    count++;
                }
            }

            if (count == 0)
            {
                return transform.position + transform.up * 1.2f;
            }

            return sum / count;
        }
    }
}
