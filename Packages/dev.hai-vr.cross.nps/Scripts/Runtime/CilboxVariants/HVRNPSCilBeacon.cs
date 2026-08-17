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

using UnityEngine;

namespace HVR.NPS.ForCilbox
{
    [Cilboxable]
    [AddComponentMenu("HVR/NPS/HVR NPS Beacon (Cilbox)")]
    public class HVRNPSCilBeacon : MonoBehaviour
    {
        /// The finder ends at this point, and will not go through any further points.
        internal const int HVRNPSCilPassage_Termination = 0;

        /// This is an intermediate point. The finder may find passage through
        /// another intermediate point, or end in a termination point.
        internal const int HVRNPSCilPassage_Intermediate = 1;
        
        /// This is an internal point. It cannot be found by finders, but it may be referenced by another beacon,
        /// which then serves as points of passage.
        internal const int HVRNPSCilPassage_Internal = 2;
        
        //
        
        // The finder will always go through the center of this beacon.
        internal const int HVRNPSCilAlignment_Center = 0;

        // The finder will move along the up vector, away by its radius.
        internal const int HVRNPSCilAlignment_Edge = 1;
        
        //
        
        // ConstrictToHide if the passage is a Termination and that Termination has no next, No Change otherwise.
        internal const int HVRNPSCilConstriction_Default = 0;
        
        /// Entry does not constrict the mesh.
        internal const int HVRNPSCilConstriction_NoChange = 1;
        
        /// Entry constricts the mesh entirely to hide it.
        internal const int HVRNPSCilConstriction_ConstrictToHide = 2;
        
        //
        
        /// One-way if the passage is a termination, two-way otherwise
        internal const int HVRNPSCilDirectionality_Default = 0;
        
        /// Can accept entrance in both ways, as defined by the forward direction.
        internal const int HVRNPSCilDirectionality_TwoWay = 1;
        
        /// Can accept entrance in only the forward direction.
        internal const int HVRNPSCilDirectionality_OneWay = 2;
        
        /// Can accept entrance in only the backward direction. This value is intended to be used by scripting to freeze a state when grabbing without having to rotate the beacon.
        internal const int HVRNPSCilDirectionality_ReverseWay = 3;
        
        /// Can accept entrance in any direction going through the plane defined by the up vector (green).
        internal const int HVRNPSCilDirectionality_AlongNormalPlane = 4;
        
        public int passage;
        public int alignment;
        public int constriction;
        public int directionality;

        public HVRNPSCilBeacon[] next;
        
        public Transform AsTransform => transform;
        
        private bool _registered;

        public Vector3 CalculateCenter(float girthRadius)
        {
            return alignment switch
            {
                HVRNPSCilAlignment_Edge => transform.position + transform.up * girthRadius,
                _ => transform.position
            };
        }

        public int ActualConstriction()
        {
            if (constriction != HVRNPSCilConstriction_Default) return constriction;
            
            return passage == HVRNPSCilPassage_Termination && next.Length == 0
                ? HVRNPSCilConstriction_ConstrictToHide
                : HVRNPSCilConstriction_NoChange;
        }

        public int ActualDirectionality()
        {
            if (directionality != HVRNPSCilDirectionality_Default) return directionality;
            
            return passage == HVRNPSCilPassage_Termination ? HVRNPSCilDirectionality_OneWay : HVRNPSCilDirectionality_TwoWay;
        }
    }
}