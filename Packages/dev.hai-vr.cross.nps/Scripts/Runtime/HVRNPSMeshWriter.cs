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
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS MeshWriter")]
    public class HVRNPSMeshWriter : MonoBehaviour
    {
        public SkinnedMeshRenderer[] renderers;
        
        private readonly Dictionary<SkinnedMeshRenderer, GraphicsBuffer> _smrBuffers = new();
        
        public void OnEnable()
        {
            if (renderers.Length > 0 && _smrBuffers.Count == 0)
            {
                foreach (var smr in renderers)
                {
                    smr.vertexBufferTarget |= GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Structured;
                    var buffer = smr.GetVertexBuffer();
                    _smrBuffers.Add(smr, buffer);
                }
            }
        }
    }
}