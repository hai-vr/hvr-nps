using System;
using UnityEngine;

namespace HVR.NPS.ForCilbox
{
    [Cilboxable]
    [Serializable]
    public class NPSCilSegment
    {
        public Transform transform;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float segmentLength;
        public Quaternion modelToStandard;
        public Quaternion standardToModel;

        public void Restore()
        {
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }
    }
}