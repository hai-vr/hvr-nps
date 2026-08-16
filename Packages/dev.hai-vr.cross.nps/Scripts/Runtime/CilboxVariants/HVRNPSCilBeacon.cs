// Copyright 2026 Haï~ (@vr_hai github.com/hai-vr)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace HVR.NPS.ForCilbox
{
    [Cilboxable]
    [AddComponentMenu("HVR/NPS/HVR NPS Beacon (Cilbox)")]
    public class HVRNPSCilBeacon : MonoBehaviour
    {
        public HVRNPSCilPassage passage;
        public HVRNPSCilAlignment alignment;
        public HVRNPSCilConstriction constriction;
        public HVRNPSCilDirectionality directionality;

        public HVRNPSCilBeacon[] next;
        
        public Transform AsTransform => transform;
        
        private bool _registered;

        public Vector3 CalculateCenter(float girthRadius)
        {
            return alignment switch
            {
                HVRNPSCilAlignment.Edge => transform.position + transform.up * girthRadius,
                _ => transform.position
            };
        }

        public HVRNPSCilConstriction ActualConstriction()
        {
            if (constriction != HVRNPSCilConstriction.Default) return constriction;
            
            return passage == HVRNPSCilPassage.Termination && next.Length == 0
                ? HVRNPSCilConstriction.ConstrictToHide
                : HVRNPSCilConstriction.NoChange;
        }

        public HVRNPSCilDirectionality ActualDirectionality()
        {
            if (directionality != HVRNPSCilDirectionality.Default) return directionality;
            
            return passage == HVRNPSCilPassage.Termination ? HVRNPSCilDirectionality.OneWay : HVRNPSCilDirectionality.TwoWay;
        }
    }

    [Cilboxable]
    public enum HVRNPSCilPassage
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

    [Cilboxable]
    public enum HVRNPSCilAlignment
    {
        // The finder will always go through the center of this beacon.
        Center,

        // The finder will move along the up vector, away by its radius.
        Edge
    }

    [Cilboxable]
    public enum HVRNPSCilConstriction
    {
        // ConstrictToHide if the passage is a Termination and that Termination has no next, No Change otherwise.
        Default,
        
        /// Entry does not constrict the mesh.
        NoChange,
        
        /// Entry constricts the mesh entirely to hide it.
        ConstrictToHide,
    }

    [Cilboxable]
    public enum HVRNPSCilDirectionality
    {
        /// One-way if the passage is a termination, two-way otherwise
        Default,
        
        /// Can accept entrance in both ways, as defined by the forward direction.
        TwoWay,
        
        /// Can accept entrance in only the forward direction.
        OneWay,
        
        /// Can accept entrance in only the backward direction. This value is intended to be used by scripting to freeze a state when grabbing without having to rotate the beacon.
        ReverseWay,
        
        /// Can accept entrance in any direction going through the plane defined by the up vector (green).
        AlongNormalPlane
    }
}