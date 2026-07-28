using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Finder")]
    public class HVRNPSFinder : MonoBehaviour
    {
        public HVRNPSChain chain;

        public void OnEnable()
        {
            HVRNPSQuery.Instance.Register(this);
        }
        
        public void OnDisable()
        {
            HVRNPSQuery.Instance.Unregister(this);
        }
    }
}