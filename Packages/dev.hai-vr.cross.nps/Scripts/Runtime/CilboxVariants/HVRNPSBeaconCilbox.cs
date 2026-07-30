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

using HVR.Query;
using UnityEngine;

namespace HVR.NPS.CilboxVariants
{
    [Cilboxable]
    [AddComponentMenu("HVR/NPS/Cilbox/HVR NPS Beacon (Cilbox)")]
    public class HVRNPSBeaconCilbox : MonoBehaviour
    {
        private HVRQueryBeacon _beacon;
        
        private void OnEnable()
        {
            _beacon ??= new HVRQueryBeacon(transform);
            HVRQuery.Instance.Register(_beacon);
        }
        
        private void OnDisable()
        {
            HVRQuery.Instance.Unregister(_beacon);
        }
    }
}