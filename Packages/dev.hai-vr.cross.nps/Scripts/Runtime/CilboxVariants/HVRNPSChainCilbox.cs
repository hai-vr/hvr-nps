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
    [AddComponentMenu("HVR/NPS/HVR NPS Chain (Cilbox)")]
    public class HVRNPSChainCilbox : MonoBehaviour
    {
        public HVRNPSFinderCilbox finder;
        
        public Transform[] elements;
        public Transform[] idleProxies;
        
        public float girthRadius = 0.5f;
        public float tipLength = 0.1f;

        public HVRNPSVirtualBeaconCilbox[] beacons;
        private Quaternion _reorient;
        
        private readonly List<HVRNPSVirtualBeaconCilbox> _sortedBeacons = new();
        private NPSSegment[] _memory;
        private float _totalLength;
        private float _curveApplies01;

        private void OnEnable()
        {
            if (_memory == null)
            {
                _totalLength = 0;
                _memory = new NPSSegment[elements.Length];
                for (var i = 0; i < elements.Length; i++)
                {
                    var segmentLength = i < elements.Length - 1
                        ? (elements[i + 1].position - elements[i].position).magnitude
                        : tipLength;
                    
                    _memory[i] = new NPSSegment
                    {
                        transform = elements[i],
                        position = elements[i].localPosition,
                        rotation = elements[i].localRotation,
                        scale = elements[i].localScale,
                        segmentLength = segmentLength
                    };
                    _totalLength += segmentLength;
                }
            }

            if (finder != null)
            {
                finder.OnBeaconsChanged += WhenBeaconsChanged;
            }
        }

        private void OnDisable()
        {
            if (_memory != null)
            {
                for (var i = 0; i < _memory.Length; i++)
                {
                    _memory[i].Restore();
                }
            }

            if (finder != null)
            {
                finder.OnBeaconsChanged -= WhenBeaconsChanged;
            }
        }

        private void WhenBeaconsChanged(HVRNPSFinderCilbox finder, List<HVRNPSVirtualBeaconCilbox> inBeacons)
        {
            beacons = inBeacons.ToArray();
        }

        private void Update()
        {
            HVRQuery.Instance.TryUpdateBeaconPositions();
            SortBeacons();
            DeformElements(_sortedBeacons);
        }

        private void SortBeacons()
        {
            _sortedBeacons.Clear();
            _sortedBeacons.AddRange(beacons);
            
            var rootPosition = transform.position;
            _sortedBeacons.Sort((a, b) => (CalculateCenter(a) - rootPosition).magnitude.CompareTo((CalculateCenter(b) - rootPosition).magnitude));
            for (var index = 0; index < _sortedBeacons.Count -1; index++)
            {
                HVRNPSVirtualBeaconCilbox sortedBeacon = _sortedBeacons[index];
                if (sortedBeacon.passage == (int)HVRNPSPassage.Termination)
                {
                    _sortedBeacons.RemoveRange(index + 1, _sortedBeacons.Count - index - 1);
                    break;
                }
            }
        }

        private Vector3 CalculateCenter(HVRNPSVirtualBeaconCilbox beacon)
        {
            return beacon.alignment switch
            {
                (int)HVRNPSAlignment.Edge => transform.position + transform.up * girthRadius,
                _ => transform.position
            };
        }

        private void OnDestroy()
        {
            HVRQuery.Instance.Dispose();
        }

        public void DeformElements(List<HVRNPSVirtualBeaconCilbox> inputBeacons)
        {
            if (inputBeacons.Count == 0)
            {
                FullyApplyIdle();
                return;
            }
            
            // This should work similarly to an IK system (see HVR IK documentation):
            // - Find a good matching curve that pass through the beacons, although not necessarily through all of them.
            // - Based on the real distances, calculate the points.
            // - Those points restrict two freedoms from the rotation. Calculate the rolls.
            // - Apply the changes in rotation.

            var points = new List<NPSPoint>();
            CalculateCurve(points, inputBeacons);
            
            // I'm not sure when this can happen. This might be too defensive
            if (points.Count < 2)
            {
                FullyApplyIdle();
                return;
            }
            
            var firstPosition = CalculateCenter(inputBeacons[0]);
            var distanceToFirstPosition = Vector3.Distance(elements[0].transform.position, firstPosition);
            var TODO_FALLOFF = 2f;
            var TODO_MARGIN = 1f;
            _curveApplies01 = Mathf.InverseLerp(_totalLength + TODO_MARGIN + TODO_FALLOFF, _totalLength + TODO_MARGIN, distanceToFirstPosition);

            if (_curveApplies01 == 0f)
            {
                FullyApplyIdle();
                return;
            }

            var distances = new List<float>();
            distances.Add(0);
            for (var i = 1; i < points.Count; i++)
            {
                distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1].position, points[i].position));
            }

            _reorient = NPSMath.FromToOrientation(Vector3.forward, Vector3.right, Vector3.up, Vector3.forward);
        
            var currentDist = 0f;
            var lastForward = Vector3.zero;
            var lastUpVector = transform.up;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var segment = _memory[i];
                
                var pos = SampleCurve(points, distances, currentDist);
                var nextPos = SampleCurve(points, distances, currentDist + segment.segmentLength);
                
                var forward = (nextPos.position - pos.position).normalized;
                if (forward == Vector3.zero) forward = transform.up;
                
                var constriction = pos.constriction;
                
                if (i != 0)
                {
                    var similarityToForward = Vector3.Dot(transform.forward, forward);
                    var similatiryToBack = -similarityToForward;
                    var similarityToUp = Vector3.Dot(transform.up, forward);
                    var similarityToDown = -similarityToUp;

                    var sequentialLerp = NPSMath.ReprojectTwistToArm(forward, lastForward, lastUpVector); // Lowest priority, for left and right directions (since it's unclear whether it's right-hand direction, or right-hand direction while upside down).
                    sequentialLerp = Vector3.Lerp(sequentialLerp, transform.forward, Mathf.Clamp01(similarityToDown));
                    sequentialLerp = Vector3.Lerp(sequentialLerp, -transform.forward, Mathf.Clamp01(similarityToUp));
                    sequentialLerp = Vector3.Lerp(sequentialLerp, -transform.up, Mathf.Clamp01(similatiryToBack)); // <-- It might be possible to remove this one, so that it uses the same as ReprojectToTwist too? Not sure
                    sequentialLerp = Vector3.Lerp(sequentialLerp, transform.up, Mathf.Clamp01(similarityToForward)); // Highest priority, for forward direction.
                    sequentialLerp = sequentialLerp.normalized;
                    
                    lastUpVector = sequentialLerp;
                }
                
                // Order matters in this version of the code.
                // This is because curveApplies01 makes use of the local position deduced from applying this world space position.
                element.SetPositionAndRotation(
                    pos.position,
                    Quaternion.LookRotation(forward, lastUpVector) * _reorient
                );

                var localScaleToApply = Vector3.Scale(segment.scale, new Vector3(constriction, 1f, constriction));
                if (_curveApplies01 < 1f)
                {
                    if (idleProxies.Length == elements.Length)
                    {
                        var idleProxy = idleProxies[i];
                        element.SetPositionAndRotation(
                            Vector3.Lerp(idleProxy.position, element.position, _curveApplies01),
                            Quaternion.Lerp(idleProxy.rotation, element.rotation, _curveApplies01)
                        );
                    }
                    else
                    {
                        element.SetLocalPositionAndRotation(
                            Vector3.Lerp(segment.position, element.localPosition, _curveApplies01),
                            Quaternion.Lerp(segment.rotation, element.localRotation, _curveApplies01)
                        );
                    }
                    element.localScale = Vector3.Lerp(segment.scale, localScaleToApply, _curveApplies01);
                }
                else
                {
                    element.localScale = localScaleToApply;
                }
                
                currentDist += segment.segmentLength;
                lastForward = forward;
            }
        }

        private void FullyApplyIdle()
        {
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var segment = _memory[i];
                element.localScale = segment.scale;
                if (idleProxies.Length == elements.Length)
                {
                    var idleProxy = idleProxies[i];
                    element.SetPositionAndRotation(idleProxy.position, idleProxy.rotation);
                }
                else
                {
                    element.SetLocalPositionAndRotation( segment.position, segment.rotation);
                }
            }
        }

        private NPSPoint SampleCurve(List<NPSPoint> points, List<float> distances, float distance)
        {
            if (distance <= 0) return points[0];
            if (distance >= distances[^1]) return points[^1];

            for (var i = 0; i < distances.Count - 1; i++)
            {
                if (distance >= distances[i] && distance <= distances[i + 1])
                {
                    var t = (distance - distances[i]) / (distances[i + 1] - distances[i]);
                    return new NPSPoint(Vector3.Lerp(points[i].position, points[i + 1].position, t), Mathf.Lerp(points[i].constriction, points[i + 1].constriction, t));
                }
            }
            return points[^1];
        }

        private void CalculateCurve(List<NPSPoint> points, List<HVRNPSVirtualBeaconCilbox> beacons)
        {
            var currentPos = transform.position;
            var currentDir = transform.forward;
            var k = 0;
            HVRNPSBeacon lastBeacon = null;
            
            float nextConstriction = 1f;
            foreach (var mainBeacon in beacons)
            {
                for (var i = -1; i < mainBeacon.next.Length; i++)
                {
                    var beacon = i == -1 ? mainBeacon : mainBeacon.next[i];
                    if (beacon.isActiveAndEnabled)
                    {
                        static Vector3 CalculateTwoWay(Vector3 currentPos, Vector3 nextPos, Vector3 beaconForward)
                        {
                            var dot = Vector3.Dot((currentPos - nextPos).normalized, beaconForward);
                            
                            // Avoid the sudden change in direction when the direction becomes perpendicular.
                            var multiplier = Mathf.Lerp(0.05f, 1f, Mathf.InverseLerp(0f, 0.1f, Mathf.Abs(dot)));
                            
                            return (dot > 0f ? beaconForward : -beaconForward) * multiplier;
                        }
                        
                        var nextPos = beacon.CalculateCenter(girthRadius);
                        
                        var directionality = beacon.ActualDirectionality();
                        var beaconForward = beacon.transform.forward;
                        var nextDir = directionality switch
                        {
                            HVRNPSDirectionality.OneWay => -beaconForward,
                            HVRNPSDirectionality.ReverseWay => beaconForward,
                            HVRNPSDirectionality.TwoWay => CalculateTwoWay(currentPos, nextPos, beaconForward),
                            HVRNPSDirectionality.AlongNormalPlane => NPSMath.Straighten((currentPos - nextPos).normalized, beacon.transform.up),
                            _ => -beaconForward
                        };
                        
                        NPSMath.PrepareSeilerInterpolation(currentPos, nextPos, currentDir, nextDir, out var b0, out var b3, out var s1, out var s2);
                        var prev = NPSMath.SeilerInterpolate(b0, b3, s1, s2, 0f);
                        points.Add(new NPSPoint(b0, nextConstriction));
                
                        var color = k == 0 ? Color.cyan : Color.green;
                        for (var f = 0.1f; f < 1f; f += 0.1f)
                        {
                            var pos = NPSMath.SeilerInterpolate(b0, b3, s1, s2, f);
                            points.Add(new NPSPoint(pos, nextConstriction));
                            Debug.DrawLine(prev, pos, color, 0.01f);
                            prev = pos;
                        }
                        points.Add(new NPSPoint(b3, nextConstriction));
                        Debug.DrawLine(prev, b3, color, 0.01f);
                
                        currentPos = nextPos;
                        currentDir = -nextDir;
                        k++;
                        lastBeacon = beacon;
                        nextConstriction = beacon.constriction == HVRNPSConstriction.ConstrictToHide ? 0f : nextConstriction;
                    }
                }
            }

            if (lastBeacon != null)
            {
                var dirNormalized = currentDir.normalized;
                
                for (var f = 0.1f; f <= _totalLength * 2; f += 0.1f)
                {
                    points.Add(new NPSPoint(currentPos + dirNormalized * f, nextConstriction));
                }
                
                var color = k == 0 ? Color.cyan : Color.green;
                color.a = 0.5f;
                Debug.DrawLine(currentPos, currentPos + dirNormalized * (_totalLength * 0.5f), color, 0.01f);
            }
        }

        private struct NPSSegment
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float segmentLength;

            public void Restore()
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }
        
        private struct NPSPoint
        {
            public Vector3 position;
            public float constriction;

            public NPSPoint(Vector3 position, float constriction)
            {
                this.position = position;
                this.constriction = constriction;
            }
        }
    }
}