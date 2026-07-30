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
using UnityEditor;
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Finder")]
    public class HVRNPSFinder : MonoBehaviour
    {
        public event BeaconsChanged OnBeaconsChanged;
        public delegate void BeaconsChanged(HVRNPSFinder finder, List<HVRNPSBeacon> beacons);
        
        public float range = 1f;

        private readonly List<HVRNPSBeacon> _beacons = new();
        private HVRQueryFinder _finder;

        public void OnEnable()
        {
            _finder ??= new HVRQueryFinder(this, range, WhenBeaconEnterOrExit, new Dictionary<string, object>());
            HVRQuery.Instance.Register(_finder);
        }
        
        public void OnDisable()
        {
            HVRQuery.Instance.Unregister(_finder);
            OnBeaconsChanged?.Invoke(this, new List<HVRNPSBeacon>());
        }

        private void WhenBeaconEnterOrExit(HVRQueryBeacon iBeacon, bool isEntering)
        {
            if (iBeacon.Component is not HVRNPSBeacon beacon) return;
            
            Debug.Log($"Beacon {beacon.name} is {(isEntering ? "entering" : "exiting")} range");
            
            if (isEntering)
            {
                if (!_beacons.Contains(beacon)) _beacons.Add(beacon);
            }
            else
            {
                _beacons.Remove(beacon);
            }
            
            OnBeaconsChanged?.Invoke(this, _beacons);
        }

        private void OnDrawGizmosSelected()
        {
            Handles.color = Color.yellow;
            foreach (var beacon in _beacons)
            {
                Handles.DrawLine(transform.position, beacon.transform.position);
            }
        }
    }
}