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

using System.Collections.Generic;
using HVR.Query;
using UnityEngine;

namespace HVR.NPS.CilboxVariants
{
    [Cilboxable]
    [AddComponentMenu("HVR/NPS/Cilbox/HVR NPS Finder (Cilbox)")]
    public class HVRNPSFinderCilbox : MonoBehaviour
    {
        public float range = 1f;
        
        private HVRQueryFinder _beacon;
        
        private void OnEnable()
        {
            if (_beacon == null)
            {
                _beacon = new HVRQueryFinder(this, range, WhenBeaconEnterOrExit, new Dictionary<string, object>());
            }
            
            HVRQuery.Instance.Register(_beacon);
            Debug.Log($"Enabled Cilbox HVRNPSFinder");
        }

        private void OnDisable()
        {
            if (_beacon != null) // In case we triggered an issue with Cilbox where OnEnable doesn't get triggered.
            {
                HVRQuery.Instance.Unregister(_beacon);
                Debug.Log($"Disabled Cilbox HVRNPSFinder");
            }
        }

        private void WhenBeaconEnterOrExit(HVRQueryBeacon beacon, bool isEntering)
        {
            Debug.Log($"CILBOX Beacon {beacon.Component.name}: {isEntering}");
        }
    }
}