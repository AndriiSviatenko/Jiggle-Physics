using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics.Demo
{
    [DefaultExecutionOrder(10001)]
    public sealed class JiggleLabController : MonoBehaviour
    {
        [SerializeField] private JiggleRig[] duelRigs = new JiggleRig[0];
        [SerializeField] private JiggleRig[] presetRigs = new JiggleRig[0];
        [SerializeField] private JiggleLabCharacterDriver characterDriver;
        [SerializeField] private JiggleLabCameraRig cameraRig;

        private readonly List<JiggleRig> allRigs = new List<JiggleRig>();
        private readonly Dictionary<JiggleRig, JiggleProfile> runtimeProfiles = new Dictionary<JiggleRig, JiggleProfile>();
        private readonly Dictionary<JiggleRig, JiggleProfile> sourceProfiles = new Dictionary<JiggleRig, JiggleProfile>();
        private readonly Dictionary<JiggleRig, float> peakDeviations = new Dictionary<JiggleRig, float>();
        private JiggleProfile sharedDuelProfile;
        private bool ready;

        public bool IsReady
        {
            get { return ready; }
        }

        public bool HasDuel
        {
            get { return duelRigs.Length >= 2 && duelRigs[0] != null && duelRigs[1] != null; }
        }

        public JiggleRig DuelRigA
        {
            get { return HasDuel ? duelRigs[0] : null; }
        }

        public JiggleRig DuelRigB
        {
            get { return HasDuel ? duelRigs[1] : null; }
        }

        public IReadOnlyList<JiggleRig> AllRigs
        {
            get { return allRigs; }
        }

        public IReadOnlyList<JiggleRig> PresetRigs
        {
            get { return presetRigs; }
        }

        public JiggleProfile SharedDuelProfile
        {
            get { return sharedDuelProfile; }
        }

        public JiggleLabCharacterDriver CharacterDriver
        {
            get { return characterDriver; }
        }

        public JiggleLabCameraRig CameraRig
        {
            get { return cameraRig; }
        }

        public JiggleProfile GetProfile(JiggleRig rig)
        {
            if (rig == null)
            {
                return null;
            }

            if (runtimeProfiles.TryGetValue(rig, out JiggleProfile runtime) && runtime != null)
            {
                return runtime;
            }

            return rig.DefaultProfile;
        }

        public void ResetProfileFromSource(JiggleRig rig)
        {
            if (rig == null || !runtimeProfiles.TryGetValue(rig, out JiggleProfile runtime) || !sourceProfiles.TryGetValue(rig, out JiggleProfile source))
            {
                return;
            }

            if (runtime == null || source == null)
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), runtime);
            rig.Rebuild();
        }

        public void RebuildRig(JiggleRig rig)
        {
            if (rig != null)
            {
                rig.Rebuild();
            }
        }

        public void SetSolver(JiggleRig rig, JiggleSolverKind kind)
        {
            if (rig == null)
            {
                return;
            }

            rig.SetSolverKind(kind);
            rig.ResetSimulation();
            ResetPeak(rig);
        }

        public JiggleSolverKind GetSolver(JiggleRig rig)
        {
            if (rig == null)
            {
                return JiggleSolverKind.SpringDamper;
            }

            JiggleChain chain = rig.ChainCount > 0 ? rig.GetChain(0) : null;
            if (chain != null && chain.IsValid)
            {
                return chain.ActiveSolverKind;
            }

            JiggleProfile profile = GetProfile(rig);
            return profile != null ? profile.SolverKind : JiggleSolverKind.SpringDamper;
        }

        public void ResetRig(JiggleRig rig)
        {
            if (rig == null)
            {
                return;
            }

            rig.ResetSimulation();
            ResetPeak(rig);
        }

        public void ResetAll()
        {
            if (characterDriver != null)
            {
                characterDriver.ClearTeleportOffset();
            }

            JiggleSimulation.ResetAll();
            ResetPeaks();
        }

        public void ResetPeaks()
        {
            for (int i = 0; i < allRigs.Count; i++)
            {
                ResetPeak(allRigs[i]);
            }
        }

        public float GetDeviationMillimeters(JiggleRig rig)
        {
            if (rig == null)
            {
                return 0f;
            }

            float maximum = 0f;
            for (int c = 0; c < rig.ChainCount; c++)
            {
                JiggleChain chain = rig.GetChain(c);
                if (chain == null || !chain.IsValid || chain.ParticleCount == 0)
                {
                    continue;
                }

                maximum = Mathf.Max(maximum, chain.GetTipDeviation(chain.ParticleCount - 1));
            }

            return maximum * 1000f;
        }

        public float GetLimitMillimeters(JiggleRig rig)
        {
            JiggleProfile profile = GetProfile(rig);
            return profile != null ? profile.MaxOffset * 1000f : 0f;
        }

        public float GetPeakDeviationMillimeters(JiggleRig rig)
        {
            if (rig != null && peakDeviations.TryGetValue(rig, out float peak))
            {
                return peak;
            }

            return 0f;
        }

        public float GetStepMilliseconds(JiggleRig rig)
        {
            return rig != null ? rig.LastStepMilliseconds : 0f;
        }

        public float GetDuelTargetDeltaMillimeters()
        {
            return ComputeDuelDeltaMillimeters(false, false);
        }

        public float GetDuelTipDeltaMillimeters()
        {
            return ComputeDuelDeltaMillimeters(true, false);
        }

        public float GetDuelOffsetDeltaMillimeters()
        {
            return ComputeDuelDeltaMillimeters(true, true);
        }

        public bool AutoShow
        {
            get { return characterDriver != null && characterDriver.AutoShow; }
        }

        public string AutoShowName
        {
            get { return characterDriver != null ? characterDriver.AutoShowName : string.Empty; }
        }

        public bool IsCrouching
        {
            get { return characterDriver != null && characterDriver.IsCrouching; }
        }

        public bool IsWallRunning
        {
            get { return characterDriver != null && characterDriver.IsWallRunning; }
        }

        public void SetLocomotion(int index)
        {
            if (characterDriver != null)
            {
                characterDriver.SetLocomotion(index);
            }
        }

        public void PlayAction(JiggleLabCharacterDriver.LabAction action)
        {
            if (characterDriver == null)
            {
                return;
            }

            switch (action)
            {
                case JiggleLabCharacterDriver.LabAction.Idle:
                    characterDriver.SetLocomotion(0);
                    break;
                case JiggleLabCharacterDriver.LabAction.Walk:
                    characterDriver.SetLocomotion(1);
                    break;
                case JiggleLabCharacterDriver.LabAction.Jog:
                    characterDriver.SetLocomotion(2);
                    break;
                case JiggleLabCharacterDriver.LabAction.Sprint:
                    characterDriver.SetLocomotion(3);
                    break;
                case JiggleLabCharacterDriver.LabAction.Jump:
                    characterDriver.PlayJump();
                    break;
                case JiggleLabCharacterDriver.LabAction.FallLand:
                    characterDriver.PlayFall(false);
                    break;
                case JiggleLabCharacterDriver.LabAction.FallRoll:
                    characterDriver.PlayFall(true);
                    break;
                case JiggleLabCharacterDriver.LabAction.Slide:
                    characterDriver.PlaySlide();
                    break;
                case JiggleLabCharacterDriver.LabAction.Attack:
                    characterDriver.PlayAttack();
                    break;
                case JiggleLabCharacterDriver.LabAction.Flip:
                    characterDriver.PlayFlip();
                    break;
                case JiggleLabCharacterDriver.LabAction.Dash:
                    characterDriver.PlayDash();
                    break;
                case JiggleLabCharacterDriver.LabAction.Vault:
                    characterDriver.PlayVault();
                    break;
                case JiggleLabCharacterDriver.LabAction.Stumble:
                    characterDriver.PlayStumble();
                    break;
                case JiggleLabCharacterDriver.LabAction.CrouchOn:
                    characterDriver.SetCrouch(true);
                    break;
                case JiggleLabCharacterDriver.LabAction.CrouchOff:
                    characterDriver.SetCrouch(false);
                    break;
                case JiggleLabCharacterDriver.LabAction.WallRunOn:
                    characterDriver.SetWallRun(true);
                    break;
                case JiggleLabCharacterDriver.LabAction.WallRunOff:
                    characterDriver.SetWallRun(false);
                    break;
            }
        }

        public void ToggleAutoShow()
        {
            if (characterDriver != null)
            {
                characterDriver.ToggleAutoShow();
            }
        }

        public void TriggerJump()
        {
            if (characterDriver != null)
            {
                characterDriver.PlayJump();
            }
        }

        public void TriggerAttack()
        {
            if (characterDriver != null)
            {
                characterDriver.PlayAttack();
            }
        }

        public void TeleportTest()
        {
            if (characterDriver != null && characterDriver.Configured)
            {
                characterDriver.Teleport(1.5f);
                return;
            }

            for (int i = 0; i < duelRigs.Length; i++)
            {
                if (duelRigs[i] != null)
                {
                    duelRigs[i].transform.position += Vector3.right * 1.5f;
                }
            }
        }

        private void Awake()
        {
            if (duelRigs.Length == 0 || presetRigs.Length == 0 || ContainsNull(duelRigs) || ContainsNull(presetRigs))
            {
                DiscoverRigs();
            }

            allRigs.Clear();
            for (int i = 0; i < duelRigs.Length; i++)
            {
                if (duelRigs[i] != null && !allRigs.Contains(duelRigs[i]))
                {
                    allRigs.Add(duelRigs[i]);
                }
            }

            for (int i = 0; i < presetRigs.Length; i++)
            {
                if (presetRigs[i] != null && !allRigs.Contains(presetRigs[i]))
                {
                    allRigs.Add(presetRigs[i]);
                }
            }
        }

        private void Start()
        {
            AssignRuntimeProfiles();
            ApplyInitialDuelSolvers();

            if (characterDriver == null)
            {
                characterDriver = FindFirstObjectByType<JiggleLabCharacterDriver>();
            }

            if (characterDriver != null)
            {
                characterDriver.Configure(duelRigs);
            }

            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<JiggleLabCameraRig>();
            }

            ConfigureDuelVisualizers();
            ready = true;
        }

        private void Update()
        {
            if (!ready)
            {
                return;
            }

            for (int i = 0; i < allRigs.Count; i++)
            {
                JiggleRig rig = allRigs[i];
                if (rig == null || !peakDeviations.ContainsKey(rig))
                {
                    continue;
                }

                float deviation = GetDeviationMillimeters(rig);
                if (deviation > peakDeviations[rig])
                {
                    peakDeviations[rig] = deviation;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<JiggleRig, JiggleProfile> pair in runtimeProfiles)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            runtimeProfiles.Clear();
        }

        private void DiscoverRigs()
        {
            JiggleRig[] found = FindObjectsByType<JiggleRig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            List<JiggleRig> duel = new List<JiggleRig>();
            List<JiggleRig> presets = new List<JiggleRig>();

            for (int i = 0; i < found.Length; i++)
            {
                if (HasAnimator(found[i]))
                {
                    duel.Add(found[i]);
                }
                else
                {
                    presets.Add(found[i]);
                }
            }

            duel.Sort(CompareByPositionX);
            presets.Sort(CompareByPositionX);
            duelRigs = duel.ToArray();
            presetRigs = presets.ToArray();
        }

        private static bool HasAnimator(JiggleRig rig)
        {
            return rig != null && rig.GetComponentInChildren<Animator>() != null;
        }

        private static bool ContainsNull(JiggleRig[] rigs)
        {
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareByPositionX(JiggleRig first, JiggleRig second)
        {
            float firstValue = first != null ? first.transform.position.x : 0f;
            float secondValue = second != null ? second.transform.position.x : 0f;
            return firstValue.CompareTo(secondValue);
        }

        private void AssignRuntimeProfiles()
        {
            if (duelRigs.Length > 0 && duelRigs[0] != null && duelRigs[0].DefaultProfile != null)
            {
                JiggleProfile source = duelRigs[0].DefaultProfile;
                sharedDuelProfile = Instantiate(source);
                sharedDuelProfile.name = source.name + " (Lab A/B)";
                for (int i = 0; i < duelRigs.Length; i++)
                {
                    AssignProfile(duelRigs[i], sharedDuelProfile, source);
                }
            }

            for (int i = 0; i < presetRigs.Length; i++)
            {
                JiggleRig rig = presetRigs[i];
                if (rig == null || rig.DefaultProfile == null)
                {
                    continue;
                }

                JiggleProfile source = rig.DefaultProfile;
                JiggleProfile copy = Instantiate(source);
                copy.name = source.name + " (Lab " + rig.name + ")";
                AssignProfile(rig, copy, source);
            }
        }

        private void ApplyInitialDuelSolvers()
        {
            if (!HasDuel)
            {
                return;
            }

            JiggleSolverKind first = GetSolver(DuelRigA);
            JiggleSolverKind second = first == JiggleSolverKind.StableVerlet ? JiggleSolverKind.SpringDamper : JiggleSolverKind.StableVerlet;
            SetSolver(DuelRigA, first);
            SetSolver(DuelRigB, second);
        }

        private void AssignProfile(JiggleRig rig, JiggleProfile runtime, JiggleProfile source)
        {
            sourceProfiles[rig] = source;
            runtimeProfiles[rig] = runtime;
            peakDeviations[rig] = 0f;
            rig.SetProfile(runtime);
        }

        private void ResetPeak(JiggleRig rig)
        {
            if (rig != null && peakDeviations.ContainsKey(rig))
            {
                peakDeviations[rig] = 0f;
            }
        }

        private void ConfigureDuelVisualizers()
        {
            if (!HasDuel)
            {
                return;
            }

            JiggleTargetVisualizer[] visualizers = FindObjectsByType<JiggleTargetVisualizer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < visualizers.Length; i++)
            {
                JiggleTargetVisualizer visualizer = visualizers[i];
                if (visualizer.Rig == duelRigs[0])
                {
                    visualizer.SetDuelPartner(duelRigs[1]);
                }
                else if (visualizer.Rig == duelRigs[1])
                {
                    visualizer.SetDuelPartner(duelRigs[0]);
                }
            }
        }

        private float ComputeDuelDeltaMillimeters(bool compareSimulated, bool compareOffsets)
        {
            if (!HasDuel)
            {
                return 0f;
            }

            JiggleRig first = duelRigs[0];
            JiggleRig second = duelRigs[1];
            int chains = Mathf.Min(first.ChainCount, second.ChainCount);
            float maximum = 0f;

            for (int c = 0; c < chains; c++)
            {
                JiggleChain firstChain = first.GetChain(c);
                JiggleChain secondChain = second.GetChain(c);
                if (firstChain == null || secondChain == null || !firstChain.IsValid || !secondChain.IsValid)
                {
                    continue;
                }

                if (firstChain.ParticleCount != secondChain.ParticleCount || firstChain.ParticleCount == 0)
                {
                    continue;
                }

                int last = firstChain.ParticleCount - 1;
                Vector3 firstLocal = ResolveLocalPoint(first, firstChain, last, compareSimulated);
                Vector3 secondLocal = ResolveLocalPoint(second, secondChain, last, compareSimulated);

                if (compareOffsets)
                {
                    firstLocal -= ResolveLocalPoint(first, firstChain, last, false);
                    secondLocal -= ResolveLocalPoint(second, secondChain, last, false);
                }

                maximum = Mathf.Max(maximum, Vector3.Distance(firstLocal, secondLocal));
            }

            return maximum * 1000f;
        }

        private static Vector3 ResolveLocalPoint(JiggleRig rig, JiggleChain chain, int index, bool simulated)
        {
            Vector3 worldPoint = simulated ? chain.GetSimulatedTip(index) : chain.GetAnimatedTip(index);
            return rig.transform.InverseTransformPoint(worldPoint);
        }
    }
}
