using JigglePhysics;
using UnityEngine;

namespace JiggleComparison
{

    public sealed class CustomJiggleBackend : MonoBehaviour, IJiggleBackend
    {
        [SerializeField] private JiggleRig rig;

        public string DisplayName
        {
            get { return "Наша (JiggleRig)"; }
        }

        public string Summary
        {
            get { return "Spring-damper у просторі грудної клітки, 90 Гц, інерція, конус, squash & stretch"; }
        }

        public bool IsAvailable
        {
            get { return rig != null; }
        }

        public bool IsActive
        {
            get { return rig != null && rig.isActiveAndEnabled; }
        }

        public float LastCostMilliseconds
        {
            get { return IsActive ? rig.LastStepMilliseconds : 0f; }
        }

        public JiggleRig Rig
        {
            get { return rig; }
        }

        public void Configure(JiggleRig jiggleRig)
        {
            rig = jiggleRig;
        }

        public void SetActive(bool active)
        {
            if (rig == null || rig.enabled == active)
            {
                return;
            }

            rig.enabled = active;
            if (active)
            {
                rig.ResetSimulation();
            }
        }

        public void ResetSimulation()
        {
            if (IsActive)
            {
                rig.ResetSimulation();
            }
        }
    }
}
