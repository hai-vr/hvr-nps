using System;
using HVR.Query;
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Beacon")]
    public class HVRNPSBeacon : MonoBehaviour, IHVRBeacon
    {
        public HVRNPSPassage passage;
        public HVRNPSAlignment alignment;
        public HVRNPSConstriction constriction;

        public HVRNPSBeacon[] next = Array.Empty<HVRNPSBeacon>();
        
        public Transform AsTransform => transform;
        
        private bool _registered;

        private void OnEnable()
        {
            if (passage != HVRNPSPassage.Internal)
            {
                _registered = true;
                HVRQuery.Instance.Register(this);
            }
        }
        
        private void OnDisable()
        {
            if (_registered)
            {
                HVRQuery.Instance.Unregister(this);
            }
        }

        public Vector3 CalculateCenter(float girthRadius)
        {
            return alignment switch
            {
                HVRNPSAlignment.Edge => transform.position + transform.up * girthRadius,
                _ => transform.position
            };
        }
    }

    public enum HVRNPSPassage
    {
        /// The finder ends at this point, and will not go through any further points.
        Termination,

        /// This is an intermediate point. The finder may find passage through
        /// another intermediate point, or end in a termination point.
        Intermediate,
        
        /// This is an internal point. It cannot be found by finders, but it may be referenced by another beacon,
        /// which then serves as points of passage.
        Internal,
    }

    public enum HVRNPSAlignment
    {
        // The finder will always go through the center of this beacon.
        Center,

        // The finder will move along the up vector, away by its radius.
        Edge
    }

    public enum HVRNPSConstriction
    {
        /// Entry does not constrict the mesh.
        NoChange,
        
        /// Entry constricts the mesh entirely to hide it.
        ConstrictToHide,
    }
}