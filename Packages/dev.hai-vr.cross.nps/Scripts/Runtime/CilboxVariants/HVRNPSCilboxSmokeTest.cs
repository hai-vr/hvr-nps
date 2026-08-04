#if NPS_HAS_CILBOX
using UnityEngine;

namespace HVR.NPS
{
    [Cilboxable]
    public class HVRNPSCilboxSmokeTest : MonoBehaviour
    {
        private void OnEnable()
        {
            Debug.Log("HVRNPSCilboxSmokeTest OnEnable Hello World");
        }
    }
}
#endif