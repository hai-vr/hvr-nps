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
        public event BeaconsChangedCilbox OnBeaconsChanged;
        public delegate void BeaconsChangedCilbox(HVRNPSFinderCilbox finder, List<HVRNPSVirtualBeaconCilbox> beacons);
        
        public float range = 1f;
        
        private HVRQueryFinder _beacon;
        private readonly Dictionary<HVRQueryBeacon, HVRNPSVirtualBeaconCilbox> _queryBeaconToVirtualBeacon = new();

        private void OnEnable()
        {
            if (_beacon == null)
            {
                // ReSharper disable once UseArrayEmptyMethod
                _beacon = new HVRQueryFinder(this, range, WhenBeaconEnterOrExit);
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

            if (!isEntering && !_queryBeaconToVirtualBeacon.ContainsKey(beacon)) return;
            if (isEntering && _queryBeaconToVirtualBeacon.ContainsKey(beacon)) return;

            if (isEntering)
            {
                if (beacon.TryGetScriptValue("duckType", out var duckType)
                    && duckType is string stringDuckType
                    && stringDuckType == "HVR.NPS.HVRNPSBeacon")
                {
                    var newVirtualBeacon = new HVRNPSVirtualBeaconCilbox
                    {
                        passage = (int)beacon.GetScriptValue("passage"),
                        alignment = (int)beacon.GetScriptValue("alignment"),
                        constriction = (int)beacon.GetScriptValue("constriction"),
                        directionality = (int)beacon.GetScriptValue("directionality"),
                        next = (HVRNPSVirtualBeaconCilbox[])beacon.GetScriptValue("next") // FIXME: 
                    };
                    _queryBeaconToVirtualBeacon.Add(beacon, newVirtualBeacon);

                    OnBeaconsChanged?.Invoke(this, new List<HVRNPSVirtualBeaconCilbox>(_queryBeaconToVirtualBeacon.Values));
                }
            }
            else
            {
                _queryBeaconToVirtualBeacon.Remove(beacon);
            }
        }

        public bool TryGetScriptingValue(object[] scriptingData, string key, out object value)
        {
            for (var i = 0; i < scriptingData.Length; i += 2)
            {
                if (scriptingData[i] is string stringKey && stringKey == key)
                {
                    value = scriptingData[i + 1];
                    return true;
                }
            }

            value = null;
            return false;
        }
        
        public object GetScriptingValue(object[] scriptingData, string key)
        {
            TryGetScriptingValue(scriptingData, key, out var value);
            return value;
        }
    }

    [Cilboxable]
    public class HVRNPSVirtualBeaconCilbox
    {
        public int passage;
        public int alignment;
        public int constriction;
        public int directionality;
        public HVRNPSVirtualBeaconCilbox[] next;
    }
}