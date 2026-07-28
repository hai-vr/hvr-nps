using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Beacon")]
    public class HVRNPSBeacon : MonoBehaviour
    {
        public HVRNPSPassage passage;
        public HVRNPSAlignment alignment;

        private void OnEnable()
        {
            HVRNPSQuery.Instance.Register(this);
        }
        
        private void OnDisable()
        {
            HVRNPSQuery.Instance.Unregister(this);
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
        Intermediate
    }

    public enum HVRNPSAlignment
    {
        // The finder will always go through the center of this beacon.
        Center,

        // The finder will move along the up vector, away by its radius.
        Edge
    }
}