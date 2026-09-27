using System;
using UnityEngine;

namespace JigglePhysics
{
    [Serializable]
    public class JiggleChainDefinition
    {
        public Transform RootBone;
        public Transform EndBone;
        public JiggleProfile ProfileOverride;
    }
}
