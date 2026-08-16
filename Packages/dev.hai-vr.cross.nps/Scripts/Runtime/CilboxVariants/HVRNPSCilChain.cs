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

namespace HVR.NPS.ForCilbox
{
    [Cilboxable]
    [AddComponentMenu("HVR/NPS/HVR NPS Chain (Cilbox)")]
    public class HVRNPSCilChain : MonoBehaviour
    {
        private const float InterpolationStep = 0.2f;
        private const float FalloffDistance = 2f;
        private const float MarginDistance = 1f;
        // public HVRNPSCilFinder finder;
        
        public Transform[] elements;
        public Transform[] idleProxies;
        
        public float girthRadius = 0.5f;
        public float tipLength = 0.1f;

        public HVRNPSCilBeacon[] beacons;
        
        private readonly List<object/*cilbox::HVRNPSCilBeacon*/> _sortedBeacons = new();
        private object[]/*cilbox::NPSCilSegment*/ _memory; public NPSCilSegment _MEMORY(int i) { return (NPSCilSegment)_memory[i]; }
        private float _totalLength;
        private float _curveApplies01;
        private float _girthRadiusInWorldSpace;
        private NPSCilMath _NPSCilMath;
        private Vector3 __cil__rootPosition;
        private readonly List<float> _distances = new();
        
        private readonly List<Vector3> _pPoints = new();
        private readonly List<float> _pConstrictions = new();
        private float _currentScale;

        private void Start()
        {
            _NPSCilMath = new NPSCilMath();
            _currentScale = transform.lossyScale.x;
            if (_memory == null)
            {
                _totalLength = 0;
                _memory = new object[elements.Length];
                for (var i = 0; i < elements.Length; i++)
                {
                    var element = elements[i];
                    var segmentLength = i < elements.Length - 1
                        ? (elements[i + 1].position - element.position).magnitude
                        : tipLength * _currentScale;

                    var standardToModel = Quaternion.Inverse(element.rotation) * transform.rotation;
                    _memory[i] = new NPSCilSegment
                    {
                        transform = element,
                        position = element.localPosition,
                        rotation = element.localRotation,
                        scale = element.localScale,
                        segmentLength = segmentLength,
                        modelToStandard = standardToModel,
                        standardToModel = Quaternion.Inverse(standardToModel),
                    };
                    _totalLength += segmentLength;
                }
            }
        }
        
        private void OnDisable()
        {
            if (_memory != null)
            {
                for (var i = 0; i < _memory.Length; i++)
                {
                    _MEMORY(i).Restore();
                }
            }
        }

        private void Update()
        {
            _currentScale = transform.lossyScale.x;
            _girthRadiusInWorldSpace = girthRadius * _currentScale;
            SortBeacons();
            DeformElements(_sortedBeacons);
        }

        private void SortBeacons()
        {
            _sortedBeacons.Clear();
            foreach (var beacon in beacons)
            {
                if (beacon.isActiveAndEnabled)
                {
                    _sortedBeacons.Add(beacon);
                }
            }
            
            __cil__rootPosition = transform.position;
            
            _sortedBeacons.Sort(SortBeaconsCompareFn);
            for (var index = 0; index < _sortedBeacons.Count -1; index++)
            {
                HVRNPSCilBeacon sortedBeacon = (HVRNPSCilBeacon)_sortedBeacons[index];
                if (sortedBeacon.passage == HVRNPSCilPassage.Termination)
                {
                    _sortedBeacons.RemoveRange(index + 1, _sortedBeacons.Count - index - 1);
                    break;
                }
            }
        }

        private int SortBeaconsCompareFn(object a_cil, object b_cil)
        {
            var a = (HVRNPSCilBeacon)a_cil;
            var b = (HVRNPSCilBeacon)b_cil;
            return (a.CalculateCenter(_girthRadiusInWorldSpace) - __cil__rootPosition).magnitude.CompareTo((b.CalculateCenter(_girthRadiusInWorldSpace) - __cil__rootPosition).magnitude);
        }

        public void DeformElements(List<object/*cilbox::HVRNPSCilBeacon*/> inputBeacons)
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

            _pPoints.Clear();
            _pConstrictions.Clear();
            CalculateCurve(transform, inputBeacons);
            
            // I'm not sure when this can happen. This might be too defensive
            if (_pPoints.Count < 2)
            {
                FullyApplyIdle();
                return;
            }
            
            var firstPosition = ((HVRNPSCilBeacon)inputBeacons[0]).CalculateCenter(_girthRadiusInWorldSpace);
            var distanceToFirstPosition = Vector3.Distance(elements[0].transform.position, firstPosition);
            var falloff = FalloffDistance * _currentScale;
            var margin = MarginDistance * _currentScale;
            _curveApplies01 = Mathf.InverseLerp(_totalLength + margin + falloff, _totalLength + margin, distanceToFirstPosition);

            if (_curveApplies01 == 0f)
            {
                FullyApplyIdle();
                return;
            }

            _distances.Clear();
            _distances.Add(0);
            for (var i = 1; i < _pPoints.Count; i++)
            {
                _distances.Add(_distances[i - 1] + Vector3.Distance(_pPoints[i - 1], _pPoints[i]));
            }

            var currentDist = 0f;
            var lastForward = Vector3.zero;
            var lastUpVector = transform.up;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var segment = _MEMORY(i);
                
                var pos = SampleCurve(_distances, currentDist);
                var nextPos = SampleCurve(_distances, currentDist + segment.segmentLength);
                
                var forward = (nextPos - pos).normalized;
                if (forward == Vector3.zero) forward = transform.up;
                
                var constriction = SampleCurve_Constrictions(_distances, currentDist);
                
                if (i != 0)
                {
                    var similarityToForward = Vector3.Dot(transform.forward, forward);
                    var similatiryToBack = -similarityToForward;
                    var similarityToUp = Vector3.Dot(transform.up, forward);
                    var similarityToDown = -similarityToUp;

                    var sequentialLerp = NPSCilMath.ReprojectTwistToArm(forward, lastForward, lastUpVector); // Lowest priority, for left and right directions (since it's unclear whether it's right-hand direction, or right-hand direction while upside down).
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
                    pos,
                    Quaternion.LookRotation(forward, lastUpVector) * segment.standardToModel
                );

                // var localScaleToApply = Vector3.Scale(segment.scale, new Vector3(constriction, 1f, constriction));
                var localScaleToApply = Vector3.Scale(segment.scale, Vector3.one); // TODO: FIX THIS, CONSTRICTION DOESNT WORK
                
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
                var segment = _MEMORY(i);
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

        private Vector3 SampleCurve(List<float> distances, float distance)
        {
            if (distance <= 0) return _pPoints[0];
            if (distance >= distances[^1]) return _pPoints[^1];

            for (var i = 0; i < distances.Count - 1; i++)
            {
                if (distance >= distances[i] && distance <= distances[i + 1])
                {
                    var t = (distance - distances[i]) / (distances[i + 1] - distances[i]);
                    var pointA = _pPoints[i];
                    var pointB = _pPoints[i + 1];
                    return Vector3.Lerp(pointA, pointB, t);
                }
            }
            return _pPoints[^1];
        }

        private float SampleCurve_Constrictions(List<float> distances, float distance)
        {
            if (distance <= 0) return _pConstrictions[0];
            if (distance >= distances[^1]) return _pConstrictions[^1];

            for (var i = 0; i < distances.Count - 1; i++)
            {
                if (distance >= distances[i] && distance <= distances[i + 1])
                {
                    var t = (distance - distances[i]) / (distances[i + 1] - distances[i]);
                    var pointA = _pConstrictions[i];
                    var pointB = _pConstrictions[i + 1];
                    return Mathf.Lerp(pointA, pointB, t);
                }
            }
            return _pConstrictions[^1];
        }

        private void CalculateCurve(Transform referential, List<object/*cilbox::HVRNPSCilBeacon*/> beacons)
        {
            var currentPos = referential.position;
            var currentDir = referential.forward;
            var k = 0;
            HVRNPSCilBeacon lastBeacon = null;
            
            float nextConstriction = 1f;
            foreach (var mainBeacon_cil in beacons)
            {
                var mainBeacon = (HVRNPSCilBeacon)mainBeacon_cil;
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
                        
                        var nextPos = beacon.CalculateCenter(_girthRadiusInWorldSpace);
                        
                        var directionality = beacon.ActualDirectionality();
                        var beaconForward = beacon.transform.forward;
                        var nextDir = directionality switch
                        {
                            HVRNPSCilDirectionality.OneWay => -beaconForward,
                            HVRNPSCilDirectionality.ReverseWay => beaconForward,
                            HVRNPSCilDirectionality.TwoWay => CalculateTwoWay(currentPos, nextPos, beaconForward),
                            HVRNPSCilDirectionality.AlongNormalPlane => _NPSCilMath.Straighten((currentPos - nextPos).normalized, beacon.transform.up),
                            _ => -beaconForward
                        };
                        
                        _NPSCilMath.PrepareSeilerInterpolation(currentPos, nextPos, currentDir * _currentScale, nextDir * _currentScale);
                        var b0 = _NPSCilMath.PrepareSeilerInterpolation_result[0];
                        var b3 = _NPSCilMath.PrepareSeilerInterpolation_result[1];
                        var s1 = _NPSCilMath.PrepareSeilerInterpolation_result[2];
                        var s2 = _NPSCilMath.PrepareSeilerInterpolation_result[3];
                        
                        _pPoints.Add(b0);
                        _pConstrictions.Add(nextConstriction);
                
                        for (var f = InterpolationStep; f < 1f; f += InterpolationStep)
                        {
                            var pos = _NPSCilMath.SeilerInterpolate(b0, b3, s1, s2, f);
                            _pPoints.Add(pos);
                            _pConstrictions.Add(nextConstriction);
                        }
                        _pPoints.Add(b3);
                        _pConstrictions.Add(nextConstriction);
                
                        currentPos = nextPos;
                        currentDir = -nextDir;
                        k++;
                        lastBeacon = beacon;
                        nextConstriction = beacon.ActualConstriction() == HVRNPSCilConstriction.ConstrictToHide ? 0f : nextConstriction;
                    }
                }
            }

            if (lastBeacon != null)
            {
                var dirNormalized = currentDir.normalized;
                
                // for (var f = 0.1f; f <= _totalLength * 2; f += 0.1f)
                // {
                //     _pPoints.Add(currentPos + dirNormalized * f);
                //     _pConstrictions.Add(nextConstriction);
                // }
                //
                _pPoints.Add(currentPos + dirNormalized * 0.1f);
                _pConstrictions.Add(nextConstriction);
                
                _pPoints.Add(currentPos + dirNormalized * (_totalLength * 2));
                _pConstrictions.Add(nextConstriction);
            }
        }
    }
}