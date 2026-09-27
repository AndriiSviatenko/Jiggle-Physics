using System.Collections.Generic;
using JigglePhysics;
using UnityEngine;

namespace JiggleComparison
{

    public sealed class JiggleBoneSet
    {
        private readonly Transform[] roots;
        private readonly Transform[] tips;
        private readonly Quaternion[] restLocalRotations;
        private readonly Vector3[] restLocalScales;

        private JiggleBoneSet(List<Transform> rootList, List<Transform> tipList)
        {
            roots = rootList.ToArray();
            tips = tipList.ToArray();
            restLocalRotations = new Quaternion[roots.Length];
            restLocalScales = new Vector3[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                restLocalRotations[i] = roots[i].localRotation;
                restLocalScales[i] = roots[i].localScale;
            }
        }

        public int Count
        {
            get { return roots.Length; }
        }

        public Transform GetRoot(int index)
        {
            return roots[index];
        }

        public Transform GetTip(int index)
        {
            return tips[index];
        }

        public Quaternion GetRestLocalRotation(int index)
        {
            return restLocalRotations[index];
        }

        public void RestoreRestPose()
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                roots[i].localRotation = restLocalRotations[i];
                roots[i].localScale = restLocalScales[i];
            }
        }

        public static JiggleBoneSet FromRig(JiggleRig rig)
        {
            List<Transform> rootList = new List<Transform>();
            List<Transform> tipList = new List<Transform>();
            if (rig == null)
            {
                return new JiggleBoneSet(rootList, tipList);
            }

            rig.Initialize();
            for (int i = 0; i < rig.ChainCount; i++)
            {
                JiggleChain chain = rig.GetChain(i);
                Transform root = chain != null ? chain.GetBone(0) : null;
                if (root == null || rootList.Contains(root) || root.childCount == 0)
                {
                    continue;
                }

                rootList.Add(root);
                tipList.Add(root.GetChild(0));
            }

            return new JiggleBoneSet(rootList, tipList);
        }
    }
}
