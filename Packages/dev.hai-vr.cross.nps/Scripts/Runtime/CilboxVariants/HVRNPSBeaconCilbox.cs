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
    [AddComponentMenu("HVR/NPS/Cilbox/HVR NPS Beacon (Cilbox)")]
    public class HVRNPSBeaconCilbox : MonoBehaviour
    {
        public int passage;
        public int alignment;
        public int constriction;
        public int directionality;
        
        private HVRQueryBeacon _beacon;
        
        private void OnEnable()
        {
            _beacon ??= new HVRQueryBeacon(this, new Dictionary<string, object>
            {
                { "duckType", "HVR.NPS.HVRNPSBeacon" },
                { "version", 1 },
                { "passage", passage },
                { "alignment", alignment },
                { "constriction", constriction },
                { "directionality", directionality },
            });
            HVRQuery.Instance.Register(_beacon);
        }
        
        private void OnDisable()
        {
            if (_beacon != null) // In case we triggered an issue with Cilbox where OnEnable doesn't get triggered.
            {
                HVRQuery.Instance.Unregister(_beacon);
            }
        }
    }
}