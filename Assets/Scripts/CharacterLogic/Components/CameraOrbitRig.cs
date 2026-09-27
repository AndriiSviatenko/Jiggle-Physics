using ComponentLogic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterLogic
{
    public class CameraOrbitRig : IComponent, ILateTickableComponent, IPlayableComponent
    {
        private readonly CinemachineOrbitalFollow orbital;
        private readonly CameraOrbitConfig config;
        private readonly ICharacterInput input;
        private readonly CharacterMover mover;
        private readonly CinemachineCamera virtualCamera;

        private float targetYaw;
        private float targetPitch;
        private float targetRadius;
        private float currentYawVelocity;
        private float currentPitchVelocity;
        private float currentRadiusVelocity;
        private Vector3 baseTargetOffset;
        private float impactImpulse;
        private float impulseTime;
        private bool isInitialized;

        public float TargetYaw => targetYaw;
        public float TargetPitch => targetPitch;
        public float TargetRadius => targetRadius;

        public CameraOrbitRig(CinemachineOrbitalFollow orbital, CameraOrbitConfig config, ICharacterInput input, CharacterMover mover = null)
        {
            this.orbital = orbital;
            this.config = config;
            this.input = input;
            this.mover = mover;
            virtualCamera = orbital != null ? orbital.GetComponent<CinemachineCamera>() : null;
        }

        public void Play()
        {
            SetCursorLocked(true);

            if (orbital == null || config == null)
            {
                return;
            }

            orbital.VerticalAxis.Range = new Vector2(config.MinPitch, config.MaxPitch);
            orbital.VerticalAxis.Wrap = false;
            orbital.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbital.HorizontalAxis.Wrap = true;

            targetRadius = Mathf.Clamp(config.Radius, config.MinRadius, config.MaxRadius);
            orbital.Radius = targetRadius;

            targetPitch = Mathf.Clamp(config.DefaultPitch, config.MinPitch, config.MaxPitch);
            orbital.VerticalAxis.Value = targetPitch;

            targetYaw = orbital.HorizontalAxis.Value;

            Vector3 offset = config.ShoulderOffset;
            offset.y += config.TargetHeightOffset;
            baseTargetOffset = offset;
            orbital.TargetOffset = offset;

            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                lens.FieldOfView = config.NormalFov;
                virtualCamera.Lens = lens;
            }

            currentYawVelocity = 0f;
            currentPitchVelocity = 0f;
            currentRadiusVelocity = 0f;
            isInitialized = true;
        }

        public void Stop()
        {
            SetCursorLocked(false);
        }

        public void LateTick(float deltaTime)
        {
            HandleCursorLock();

            if (orbital == null || config == null || input == null || !input.IsReady)
            {
                return;
            }

            if (!isInitialized)
            {
                targetYaw = orbital.HorizontalAxis.Value;
                targetPitch = orbital.VerticalAxis.Value;
                targetRadius = orbital.Radius;
                isInitialized = true;
            }

            Vector2 look = input.ReadLook();
            float zoom = input.ReadZoom();

            bool isGamepad = Gamepad.current != null && Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.001f;
            float lookX;
            float lookY;

            if (isGamepad)
            {
                lookX = look.x * config.HorizontalSpeed * deltaTime;
                lookY = look.y * config.VerticalSpeed * deltaTime;
            }
            else
            {
                lookX = look.x * config.MouseSensitivity;
                lookY = look.y * config.MouseSensitivity;
            }

            if (config.InvertY)
            {
                lookY = -lookY;
            }

            targetYaw += lookX;

            if (orbital.HorizontalAxis.Value > 180f || orbital.HorizontalAxis.Value < -180f)
            {
                orbital.HorizontalAxis.Value = Mathf.Repeat(orbital.HorizontalAxis.Value + 180f, 360f) - 180f;
                targetYaw = Mathf.Repeat(targetYaw + 180f, 360f) - 180f;
            }

            targetPitch -= lookY;
            targetPitch = Mathf.Clamp(targetPitch, config.MinPitch, config.MaxPitch);

            if (Mathf.Abs(zoom) > 0.01f)
            {
                targetRadius -= Mathf.Sign(zoom) * config.ZoomStep;
                targetRadius = Mathf.Clamp(targetRadius, config.MinRadius, config.MaxRadius);
            }

            if (config.MouseSmoothTime > 0.0001f && deltaTime > 0f)
            {
                orbital.HorizontalAxis.Value = Mathf.SmoothDampAngle(
                    orbital.HorizontalAxis.Value,
                    targetYaw,
                    ref currentYawVelocity,
                    config.MouseSmoothTime,
                    float.PositiveInfinity,
                    deltaTime);

                orbital.VerticalAxis.Value = Mathf.SmoothDamp(
                    orbital.VerticalAxis.Value,
                    targetPitch,
                    ref currentPitchVelocity,
                    config.MouseSmoothTime,
                    float.PositiveInfinity,
                    deltaTime);

                orbital.Radius = Mathf.SmoothDamp(
                    orbital.Radius,
                    targetRadius,
                    ref currentRadiusVelocity,
                    config.MouseSmoothTime,
                    float.PositiveInfinity,
                    deltaTime);
            }
            else
            {
                orbital.HorizontalAxis.Value = targetYaw;
                orbital.VerticalAxis.Value = targetPitch;
                orbital.Radius = targetRadius;
            }

            UpdateDynamicFov(deltaTime);
            UpdateImpact(deltaTime);
        }

        public void AddImpulse(float strength)
        {
            impactImpulse = Mathf.Clamp(impactImpulse + Mathf.Max(0f, strength), 0f, 1.4f);
        }

        private void UpdateImpact(float deltaTime)
        {
            if (orbital == null)
            {
                return;
            }

            if (impactImpulse <= 0.001f)
            {
                impactImpulse = 0f;
                orbital.TargetOffset = baseTargetOffset;
                return;
            }

            impulseTime += deltaTime * 34f;
            Vector3 shake = new Vector3(
                Mathf.Sin(impulseTime * 1.37f),
                Mathf.Sin(impulseTime * 1.91f + 0.7f),
                0f) * (impactImpulse * 0.075f);
            orbital.TargetOffset = baseTargetOffset + shake;
            impactImpulse *= Mathf.Exp(-10f * Mathf.Max(0f, deltaTime));
        }

        private void UpdateDynamicFov(float deltaTime)
        {
            if (virtualCamera == null || mover == null)
            {
                return;
            }

            float targetFov = mover.IsWallRunning
                ? config.WallRunFov
                : Mathf.Lerp(config.NormalFov, config.SprintFov, mover.NormalizedSpeed);
            targetFov += impactImpulse * 5f;
            float blend = 1f - Mathf.Exp(-config.FovResponse * Mathf.Max(0f, deltaTime));
            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, blend);
            virtualCamera.Lens = lens;
        }

        private void HandleCursorLock()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    SetCursorLocked(true);
                }
            }
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
