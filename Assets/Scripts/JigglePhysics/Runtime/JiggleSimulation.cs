using System.Collections.Generic;
using UnityEngine;

namespace JigglePhysics
{
    [DefaultExecutionOrder(10000)]
    public class JiggleSimulation : MonoBehaviour
    {
        private static readonly List<JiggleRig> Rigs = new List<JiggleRig>();
        private static JiggleSimulation instance;

        public static JiggleSimulation Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = FindFirstObjectByType<JiggleSimulation>();
                if (instance == null)
                {
                    GameObject host = new GameObject(nameof(JiggleSimulation));
                    instance = host.AddComponent<JiggleSimulation>();
                }

                return instance;
            }
        }

        public static int RigCount
        {
            get { return Rigs.Count; }
        }

        public static void Register(JiggleRig rig)
        {
            if (rig == null || Rigs.Contains(rig))
            {
                return;
            }

            Rigs.Add(rig);
            rig.Initialize();
        }

        public static void Unregister(JiggleRig rig)
        {
            if (rig == null)
            {
                return;
            }

            Rigs.Remove(rig);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < Rigs.Count; i++)
            {
                if (Rigs[i] != null)
                {
                    Rigs[i].ResetToAnimatedPose();
                }
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance == null)
            {
                EnsureInstance();
            }
        }

        public static JiggleSimulation EnsureInstance()
        {
            return Instance;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void LateUpdate()
        {
            for (int i = Rigs.Count - 1; i >= 0; i--)
            {
                JiggleRig rig = Rigs[i];
                if (rig == null)
                {
                    Rigs.RemoveAt(i);
                    continue;
                }

                if (!rig.isActiveAndEnabled)
                {
                    continue;
                }

                float deltaTime = rig.GetDeltaTime();
                rig.Step(deltaTime);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Rigs.Clear();
            instance = null;
        }
    }
}
