using ComponentLogic;
using JiggleComparison;
using JigglePhysics;
using JigglePhysics.Demo;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterLogic
{
    public class CharacterBridge : MonoBehaviour
    {
        [SerializeField] private CharacterControllerConfig controllerConfig;
        [SerializeField] private CameraOrbitConfig cameraConfig;
        [SerializeField] private InputActionAsset inputAsset;
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private Animator animator;

        private const string ArmColliderName = "Jiggle Arm Collider";
        private const string TorsoColliderName = "Jiggle Torso Collider";
        private const float TorsoDepth = 0.07f;
        private const float TorsoClearance = 0.01f;
        private const float VisualLeanResponse = 10f;

        private ComponentRepository repository;
        private InputReader inputReader;
        private CharacterMover mover;
        private CameraOrbitRig cameraRig;
        private Vector3 animationRootBindPosition;
        private Quaternion animationRootBindRotation;
        private bool hasAnimationRootBind;
        private Quaternion visualRotationOffset = Quaternion.identity;
        private Transform hipsBone;
        private Transform headBone;
        private bool started;

        public ComponentRepository Repository
        {
            get { return repository; }
        }

        public CharacterMover Mover
        {
            get { return mover; }
        }

        private void Awake()
        {
            repository = new ComponentRepository();

            CharacterController controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CharacterController>();
            }

            if (controllerConfig == null)
            {
                Debug.LogError("[CharacterBridge] CharacterControllerConfig is not assigned.", this);
            }

            if (inputAsset == null)
            {
                Debug.LogWarning("[CharacterBridge] InputActionAsset is not assigned.", this);
            }

            inputReader = new InputReader(inputAsset);
            GroundDetector ground = new GroundDetector(controller, controllerConfig);
            mover = new CharacterMover(controller, transform, controllerConfig, inputReader, ground);

            ParkourHUD hud = GetComponent<ParkourHUD>();
            if (hud == null)
            {
                hud = gameObject.AddComponent<ParkourHUD>();
            }
            hud.Configure(mover);

            RunnerScoreSystem scoreSystem = GetComponent<RunnerScoreSystem>();
            if (scoreSystem == null)
            {
                scoreSystem = gameObject.AddComponent<RunnerScoreSystem>();
            }

            repository.Add(inputReader);
            repository.Add(ground);
            repository.Add(mover);

            if (orbitalFollow != null)
            {
                mover.ViewReference = orbitalFollow.transform;
            }

            if (cameraConfig != null && orbitalFollow != null)
            {
                cameraRig = new CameraOrbitRig(orbitalFollow, cameraConfig, inputReader, mover);
                repository.Add(cameraRig);
            }

            animator = ResolveAnimator(animator);

            if (animator != null)
            {
                ApplyAnimationRootOrientation(animator);
                animationRootBindPosition = animator.transform.localPosition;
                animationRootBindRotation = animator.transform.localRotation;
                hasAnimationRootBind = true;
                repository.Add(new AnimatorDriver(animator, mover, inputReader, controllerConfig));

                WallRunIK wallRunIk = animator.gameObject.GetComponent<WallRunIK>();
                if (wallRunIk == null)
                {
                    wallRunIk = animator.gameObject.AddComponent<WallRunIK>();
                }
                wallRunIk.Configure(animator, mover);
                wallRunIk.enabled = false;

                FootPlantIK footPlantIk = animator.gameObject.GetComponent<FootPlantIK>();
                if (footPlantIk == null)
                {
                    footPlantIk = animator.gameObject.AddComponent<FootPlantIK>();
                }
                footPlantIk.Configure(animator, mover);
                footPlantIk.enabled = false;

                SkeletonSymmetryFixer symmetry = animator.gameObject.GetComponent<SkeletonSymmetryFixer>();
                if (symmetry == null)
                {
                    symmetry = animator.gameObject.AddComponent<SkeletonSymmetryFixer>();
                }
                symmetry.Configure(animator);
                symmetry.enabled = false;
            }

            ParkourPresentation presentation = GetComponent<ParkourPresentation>();
            if (presentation == null)
            {
                presentation = gameObject.AddComponent<ParkourPresentation>();
            }
            presentation.Configure(mover, cameraRig);

            ConfigureSecondaryMotion();
        }

        private void ConfigureSecondaryMotion()
        {
            JiggleRig jiggleRig = GetComponent<JiggleRig>();
            if (jiggleRig == null)
            {
                return;
            }

            AddArmColliders(jiggleRig);

            if (GetComponent<JiggleStressTester>() == null)
            {
                gameObject.AddComponent<JiggleStressTester>();
            }

            JiggleComparisonLab lab = GetComponent<JiggleComparisonLab>();
            if (lab == null)
            {
                lab = gameObject.AddComponent<JiggleComparisonLab>();
            }
            lab.Configure(jiggleRig);

            BreastShowcaseCamera showcase = GetComponent<BreastShowcaseCamera>();
            if (showcase == null)
            {
                showcase = gameObject.AddComponent<BreastShowcaseCamera>();
            }
            showcase.Configure(jiggleRig);
        }

        private void AddArmColliders(JiggleRig jiggleRig)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            AddLimbCollider(jiggleRig, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.045f);
            AddLimbCollider(jiggleRig, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.045f);
            AddLimbCollider(jiggleRig, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.035f);
            AddLimbCollider(jiggleRig, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.035f);
            AddTorsoCollider(jiggleRig);
        }

        private void AddTorsoCollider(JiggleRig jiggleRig)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null || chest.Find(TorsoColliderName) != null || chest.GetComponentInChildren<JiggleSphereCollider>() != null)
            {
                return;
            }

            jiggleRig.Initialize();
            Vector3 roots = Vector3.zero;
            int count = 0;
            float nearestTip = float.MaxValue;
            for (int i = 0; i < jiggleRig.ChainCount; i++)
            {
                JiggleChain chain = jiggleRig.GetChain(i);
                if (chain == null || chain.GetBone(0) == null)
                {
                    continue;
                }

                roots += chain.GetBone(0).position;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            Vector3 rootCenter = roots / count;
            Vector3 back = chest.position - rootCenter;
            back.y = 0f;
            Vector3 center = rootCenter + (back.sqrMagnitude > 1e-6f ? back.normalized : -transform.forward) * TorsoDepth;
            for (int i = 0; i < jiggleRig.ChainCount; i++)
            {
                JiggleChain chain = jiggleRig.GetChain(i);
                Transform root = chain != null ? chain.GetBone(0) : null;
                if (root != null && root.childCount > 0)
                {
                    nearestTip = Mathf.Min(nearestTip, Vector3.Distance(center, root.GetChild(0).position));
                }
            }

            if (nearestTip == float.MaxValue)
            {
                return;
            }

            float pointRadius = jiggleRig.DefaultProfile != null ? jiggleRig.DefaultProfile.PointRadius : 0.03f;
            float radius = Mathf.Max(0.02f, nearestTip - pointRadius - TorsoClearance);

            GameObject colliderObject = new GameObject(TorsoColliderName);
            colliderObject.transform.SetParent(chest, false);
            colliderObject.transform.position = center;
            JiggleSphereCollider sphere = colliderObject.AddComponent<JiggleSphereCollider>();
            sphere.Radius = radius;
            jiggleRig.AddCollider(sphere);
        }

        private void AddLimbCollider(JiggleRig jiggleRig, HumanBodyBones startBone, HumanBodyBones endBone, float radius)
        {
            Transform start = animator.GetBoneTransform(startBone);
            Transform end = animator.GetBoneTransform(endBone);
            if (start == null || end == null || start.Find(ArmColliderName) != null)
            {
                return;
            }

            Vector3 localEnd = start.InverseTransformPoint(end.position);
            GameObject colliderObject = new GameObject(ArmColliderName);
            colliderObject.transform.SetParent(start, false);
            colliderObject.transform.localPosition = localEnd * 0.5f;

            JiggleCapsuleCollider capsule = colliderObject.AddComponent<JiggleCapsuleCollider>();
            capsule.Direction = localEnd.normalized;
            capsule.Radius = radius;
            capsule.Height = Vector3.Distance(start.position, end.position) + radius * 2f;
            jiggleRig.AddCollider(capsule);
        }

        private void ApplyAnimationRootOrientation(Animator anim)
        {
            Transform root = anim.transform;
            if (root.parent != transform)
            {
                return;
            }

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (root.localPosition != position)
            {
                root.localPosition = position;
            }

            if (root.localRotation != rotation)
            {
                root.localRotation = rotation;
            }

            if (root.localScale != Vector3.one)
            {
                root.localScale = Vector3.one;
            }
        }

        private Animator ResolveAnimator(Animator requested)
        {
            Transform visualRoot = transform.Find("Visual Animation Root");
            Animator best = visualRoot != null ? visualRoot.GetComponent<Animator>() : null;
            if (best == null && requested != null && requested.transform != transform)
            {
                best = requested;
            }
            if (best == null)
            {
                Debug.LogError("[CharacterBridge] Visual Animation Root is missing. Run the parkour animation setup.", this);
                return null;
            }

            best.applyRootMotion = false;
            best.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            best.enabled = true;

            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate == best) continue;
                candidate.runtimeAnimatorController = null;
                candidate.enabled = false;
            }

            return best;
        }

        private void Start()
        {
            repository.Play();
            started = true;
            if (animator != null)
            {
                animator.Update(0f);
            }
            ConfigureCinemachineBrain();
        }

        private void OnEnable()
        {
            if (started && repository != null)
            {
                repository.Play();
            }
        }

        private void OnDisable()
        {
            if (started && repository != null)
            {
                repository.Stop();
            }
        }

        private void ConfigureCinemachineBrain()
        {
            if (orbitalFollow == null)
            {
                return;
            }

            CinemachineBrain brain = orbitalFollow.GetComponentInParent<CinemachineBrain>();
            if (brain == null && Camera.main != null)
            {
                brain = Camera.main.GetComponent<CinemachineBrain>();
            }

            if (brain != null)
            {
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            }
        }

        private void Update()
        {
            if (repository != null)
            {
                repository.Tick();
            }
        }

        private void FixedUpdate()
        {
            if (repository != null)
            {
                repository.FixedTick();
            }
        }

        private void LateUpdate()
        {
            LockAnimationRoot();
            if (repository != null)
            {
                repository.LateTick();
            }
        }

        private void LockAnimationRoot()
        {
            if (animator == null || !hasAnimationRootBind)
            {
                return;
            }

            Transform root = animator.transform;
            if (root.localPosition != animationRootBindPosition)
            {
                root.localPosition = animationRootBindPosition;
            }

            if (root.localRotation != animationRootBindRotation)
            {
                root.localRotation = animationRootBindRotation;
            }

            Quaternion targetOffset = mover != null ? mover.VisualRotationOffset : Quaternion.identity;
            float blend = 1f - Mathf.Exp(-VisualLeanResponse * Time.deltaTime);
            visualRotationOffset = Quaternion.Slerp(visualRotationOffset, targetOffset, blend);
            Quaternion targetRotation = animationRootBindRotation * visualRotationOffset;
            if (root.localRotation != targetRotation)
            {
                root.localRotation = targetRotation;
            }
        }

        private Quaternion MeasureCounterRoll()
        {
            if (hipsBone == null)
            {
                hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            }

            if (headBone == null)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (hipsBone == null || headBone == null)
            {
                return Quaternion.identity;
            }

            Vector3 bodyDirection = transform.InverseTransformDirection(headBone.position - hipsBone.position).normalized;
            float lateral = Mathf.Clamp(bodyDirection.x, -1f, 1f);
            float rollDegrees = Mathf.Asin(lateral) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(-rollDegrees, Vector3.forward);
        }

        private void OnDestroy()
        {
            if (repository != null)
            {
                repository.Stop();
                repository.RemoveAll();
                repository = null;
            }
        }
    }
}
