using System.Collections.Generic;
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
        
        public void OnEnable()
        {
            HVRNPSQuery.Instance.Register(this, WhenBeaconEnterOrExit);
        }
        
        public void OnDisable()
        {
            HVRNPSQuery.Instance.Unregister(this);
            OnBeaconsChanged?.Invoke(this, new List<HVRNPSBeacon>());
        }

        private void WhenBeaconEnterOrExit(HVRNPSBeacon beacon, bool isEntering)
        {
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