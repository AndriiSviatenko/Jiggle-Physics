using UnityEngine;

namespace CharacterLogic
{
    public sealed class ParkourPresentation : MonoBehaviour
    {
        private CharacterMover mover;
        private CameraOrbitRig cameraRig;
        private TrailRenderer speedTrail;
        private ParticleSystem wallSparks;
        private ParticleSystem speedWisps;
        private Material cyanMaterial;
        private Material magentaMaterial;
        private Material cyanLineMaterial;
        private Material magentaLineMaterial;
        private Transform visualRoot;
        private Vector3 visualRootScale;
        private float squashImpulse;
        private bool configured;
        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private Transform leftLowerArm;
        private Transform rightLowerArm;
        private float airborneBlend;
        private float airborneTimer;

        public void Configure(CharacterMover characterMover, CameraOrbitRig orbitRig)
        {
            Unsubscribe();
            mover = characterMover;
            cameraRig = orbitRig;
            EnsureVisuals();
            mover.Landed += OnLanded;
            mover.Jumped += OnJumped;
            mover.Blinked += OnBlinked;
            mover.Slid += OnSlid;
            mover.Stumbled += OnStumbled;
            configured = true;
        }

        private void Update()
        {
            if (!configured || mover == null)
            {
                return;
            }

            float speed = mover.NormalizedSpeed;
            speedTrail.emitting = speed > 0.45f || mover.IsWallRunning || mover.IsFlipping || mover.IsSliding;
            speedTrail.widthMultiplier = mover.IsSliding ? 0.35f : Mathf.Lerp(0.06f, 0.24f, speed);

            ParticleSystem.EmissionModule wispsEmission = speedWisps.emission;
            wispsEmission.rateOverTime = mover.IsSliding ? 48f : Mathf.Lerp(0f, 34f, Mathf.InverseLerp(0.45f, 1f, speed));
            ParticleSystem.EmissionModule sparksEmission = wallSparks.emission;
            sparksEmission.rateOverTime = mover.IsWallRunning ? 75f : mover.IsSliding ? 45f : 0f;
            if (mover.IsWallRunning)
            {
                bool left = mover.State == ParkourState.WallRunLeft;
                wallSparks.transform.localPosition = new Vector3(left ? -0.38f : 0.38f, 0.6f, 0f);
            }
            else
            {
                wallSparks.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null)
            {
                return;
            }

            squashImpulse *= Mathf.Exp(-11f * Time.deltaTime);
            float squash = squashImpulse;
            visualRoot.localScale = new Vector3(
                visualRootScale.x * (1f + squash * 0.09f),
                visualRootScale.y * (1f - squash * 0.14f),
                visualRootScale.z * (1f + squash * 0.09f));

            UpdateAirborneArmDynamics(Time.deltaTime);
        }

        private void UpdateAirborneArmDynamics(float deltaTime)
        {
            if (mover == null || leftUpperArm == null || rightUpperArm == null)
            {
                return;
            }

            bool isAirborne = !mover.IsGrounded && !mover.IsWallRunning && !mover.IsSliding && !mover.IsVaulting && !mover.IsFlipping;
            float targetBlend = isAirborne ? 1f : 0f;
            airborneBlend = Mathf.MoveTowards(airborneBlend, targetBlend, (isAirborne ? 6f : 10f) * deltaTime);

            if (airborneBlend <= 0.001f)
            {
                airborneTimer = 0f;
                return;
            }

            airborneTimer += deltaTime;

            float vy = mover.VerticalVelocity;

            float pitch = Mathf.Clamp(vy * -2.2f, -22f, 28f);
            float spread = Mathf.Lerp(12f, 32f, Mathf.InverseLerp(2f, -12f, vy));
            float elbowBend = Mathf.Lerp(10f, 26f, Mathf.InverseLerp(4f, -10f, vy));

            float sway = Mathf.Sin(airborneTimer * 5.2f) * 7f;
            float flutter = Mathf.Cos(airborneTimer * 4.4f) * 5f;

            Quaternion rotL = Quaternion.AngleAxis((-spread - sway) * airborneBlend, transform.forward)
                            * Quaternion.AngleAxis((pitch + flutter) * airborneBlend, transform.right);
            Quaternion rotR = Quaternion.AngleAxis((spread + sway) * airborneBlend, transform.forward)
                            * Quaternion.AngleAxis((pitch - flutter) * airborneBlend, transform.right);

            leftUpperArm.rotation = rotL * leftUpperArm.rotation;
            rightUpperArm.rotation = rotR * rightUpperArm.rotation;

            if (leftLowerArm != null)
            {
                Quaternion elbowL = Quaternion.AngleAxis(-elbowBend * airborneBlend, transform.right);
                leftLowerArm.rotation = elbowL * leftLowerArm.rotation;
            }

            if (rightLowerArm != null)
            {
                Quaternion elbowR = Quaternion.AngleAxis(-elbowBend * airborneBlend, transform.right);
                rightLowerArm.rotation = elbowR * rightLowerArm.rotation;
            }
        }

        private void EnsureVisuals()
        {
            if (speedTrail != null)
            {
                return;
            }

            cyanMaterial = CreateEffectMaterial("Velocity Cyan", new Color(0.05f, 1.4f, 2.4f, 1f));
            magentaMaterial = CreateEffectMaterial("Impact Magenta", new Color(2.6f, 0.08f, 1.1f, 1f));
            cyanLineMaterial = CreateEffectMaterial("Velocity Cyan Line", new Color(0.05f, 1.4f, 2.4f, 1f), false);
            magentaLineMaterial = CreateEffectMaterial("Impact Magenta Line", new Color(2.6f, 0.08f, 1.1f, 1f), false);
            Transform animationRoot = transform.Find("Visual Animation Root");
            Animator characterAnimator = animationRoot != null ? animationRoot.GetComponent<Animator>() : null;
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }
            visualRoot = characterAnimator != null ? characterAnimator.transform : null;
            visualRootScale = visualRoot != null ? visualRoot.localScale : Vector3.one;

            if (characterAnimator != null)
            {
                leftUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }

            GameObject trailObject = new GameObject("Velocity Trail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = new Vector3(0f, 0.9f, -0.18f);
            speedTrail = trailObject.AddComponent<TrailRenderer>();
            speedTrail.time = 0.28f;
            speedTrail.minVertexDistance = 0.04f;
            speedTrail.material = cyanLineMaterial;
            speedTrail.textureMode = LineTextureMode.Stretch;
            speedTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            speedTrail.receiveShadows = false;
            speedTrail.emitting = false;
            speedTrail.colorGradient = CreateGradient(new Color(0.1f, 1f, 1f, 0.8f), new Color(1f, 0.05f, 0.75f, 0f));
            speedTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.16f, 1f), new Keyframe(1f, 0f));

            wallSparks = CreateAttachedParticles("Wall Sparks", magentaMaterial, new Vector3(0f, 0.35f, 0f), 0.32f, 0.08f, 5.5f);
            speedWisps = CreateAttachedParticles("Speed Wisps", cyanMaterial, new Vector3(0f, 1f, -0.25f), 0.5f, 0.035f, 1.1f);
        }

        private ParticleSystem CreateAttachedParticles(string effectName, Material material, Vector3 localPosition, float lifetime, float size, float speed)
        {
            GameObject effectObject = new GameObject(effectName);
            effectObject.transform.SetParent(transform, false);
            effectObject.transform.localPosition = localPosition;
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startSpeed = speed;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.18f;
            renderer.lengthScale = 2.8f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return particles;
        }

        private void OnJumped()
        {
            squashImpulse = -0.45f;
            EmitBurst(transform.position + Vector3.up * 0.12f, cyanMaterial, 18, 0.07f, 3.2f);
            cameraRig?.AddImpulse(0.2f);
        }

        private void OnLanded(float impactSpeed)
        {
            float strength = Mathf.InverseLerp(2f, 12f, impactSpeed);
            squashImpulse = Mathf.Lerp(0.45f, 1f, strength);
            EmitBurst(transform.position + Vector3.up * 0.08f, magentaMaterial, Mathf.RoundToInt(Mathf.Lerp(20f, 52f, strength)), 0.1f, Mathf.Lerp(3f, 7f, strength));
            SpawnImpactRing(transform.position + Vector3.up * 0.035f, strength);
            cameraRig?.AddImpulse(Mathf.Lerp(0.18f, 0.75f, strength));
        }

        private void OnBlinked(Vector3 from, Vector3 to)
        {
            EmitBurst(from + Vector3.up, magentaMaterial, 42, 0.12f, 6f);
            EmitBurst(to + Vector3.up, cyanMaterial, 54, 0.13f, 7f);
            SpawnBlinkBeam(from + Vector3.up, to + Vector3.up);
            cameraRig?.AddImpulse(0.85f);
        }

        private void OnSlid()
        {
            squashImpulse = 0.45f;
            EmitBurst(transform.position + Vector3.up * 0.08f, cyanMaterial, 28, 0.09f, 4.8f);
            cameraRig?.AddImpulse(0.3f);
        }

        private void OnStumbled()
        {
            squashImpulse = 0.85f;
            EmitBurst(transform.position + Vector3.up * 0.9f, magentaMaterial, 44, 0.14f, 6.5f);
            cameraRig?.AddImpulse(0.8f);
        }

        private void EmitBurst(Vector3 position, Material material, int count, float size, float speed)
        {
            GameObject effectObject = new GameObject("Movement Burst");
            effectObject.transform.position = position;
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size * 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.25f;
            renderer.lengthScale = 3.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Emit(count);
            particles.Play();
        }

        private void SpawnBlinkBeam(Vector3 from, Vector3 to)
        {
            GameObject beam = new GameObject("Blink Slash");
            LineRenderer line = beam.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.material = cyanLineMaterial;
            line.widthCurve = AnimationCurve.EaseInOut(0f, 0.03f, 1f, 0.34f);
            line.colorGradient = CreateGradient(new Color(0.2f, 1f, 1f, 0.95f), new Color(1f, 0.05f, 0.7f, 0f));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.AddComponent<FadeAndDestroy>().Configure(line, 0.28f);
        }

        private void SpawnImpactRing(Vector3 position, float strength)
        {
            GameObject ring = new GameObject("Landing Shockwave");
            ring.transform.position = position;
            LineRenderer line = ring.AddComponent<LineRenderer>();
            const int segments = 48;
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.35f);
            }
            line.useWorldSpace = false;
            line.material = magentaLineMaterial;
            line.widthMultiplier = Mathf.Lerp(0.035f, 0.11f, strength);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.AddComponent<FadeAndDestroy>().Configure(line, 0.45f, Mathf.Lerp(2.4f, 5f, strength));
        }

        private static Material CreateEffectMaterial(string materialName, Color emission, bool softGlow = true)
        {
            return ParkourEffectMaterials.Get(emission, softGlow);
        }

        private static Gradient CreateGradient(Color start, Color end)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) }, new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
            return gradient;
        }

        private void Unsubscribe()
        {
            if (mover == null) return;
            mover.Landed -= OnLanded;
            mover.Jumped -= OnJumped;
            mover.Blinked -= OnBlinked;
            mover.Slid -= OnSlid;
            mover.Stumbled -= OnStumbled;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }

    public sealed class FadeAndDestroy : MonoBehaviour
    {
        private LineRenderer line;
        private float duration;
        private float expandSpeed;
        private float elapsed;

        public void Configure(LineRenderer target, float fadeDuration, float expansion = 0f)
        {
            line = target;
            duration = Mathf.Max(0.05f, fadeDuration);
            expandSpeed = expansion;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (expandSpeed > 0f) transform.localScale = Vector3.one * (1f + elapsed * expandSpeed);
            if (line != null)
            {
                line.widthMultiplier *= Mathf.Exp(-5f * Time.deltaTime);
                Color start = line.startColor;
                Color end = line.endColor;
                start.a = end.a = 1f - t;
                line.startColor = start;
                line.endColor = end;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
