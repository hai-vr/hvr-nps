using System.Collections.Generic;

namespace HVR.NPS
{
    public class HVRNPSQuery
    {
        public static HVRNPSQuery Instance { get; private set; } = new();
        
        private readonly HashSet<HVRNPSBeacon> _beacons = new();
        private readonly HashSet<HVRNPSFinder> _finders = new();
        
        public void Register(HVRNPSBeacon beacon) { _beacons.Add(beacon); }
        public void Unregister(HVRNPSBeacon beacon) { _beacons.Remove(beacon); }
        public void Register(HVRNPSFinder finder) { _finders.Add(finder); }
        public void Unregister(HVRNPSFinder finder) { _finders.Remove(finder); }
    }
}