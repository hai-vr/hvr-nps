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
#if UNITY_EDITOR // (AUDIT): UnityEditor function
using UnityEditor;
#endif
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Chain")]
    public class HVRNPSChain : MonoBehaviour
    {
        private const float FalloffDistance = 2f;
        private const float MarginDistance = 1f;
        
        public HVRNPSFinder finder;
        
        public Transform[] elements;
        public Transform[] idleProxies;
        
        public float girthRadius = 0.5f;
        public float tipLength = 0.1f;

        public HVRNPSBeacon[] beacons;
        
        private readonly List<HVRNPSBeacon> _sortedBeacons = new();
        private NPSSegment[] _memory;
        private float _totalLength;
        private float _curveApplies01;
        private float _girthRadiusInWorldSpace;
        private float _currentScale;

        private void Start()
        {
            _currentScale = transform.lossyScale.x;
            if (_memory == null)
            {
                _totalLength = 0;
                _memory = new NPSSegment[elements.Length];
                for (var i = 0; i < elements.Length; i++)
                {
                    var element = elements[i];
                    var segmentLength = i < elements.Length - 1
                        ? (elements[i + 1].position - element.position).magnitude
                        : tipLength * _currentScale;

                    var standardToModel = Quaternion.Inverse(element.rotation) * transform.rotation;
                    _memory[i] = new NPSSegment
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
            if (finder != null)
            {
                finder.OnBeaconsChanged += WhenBeaconsChanged;
            }
        }

        private void OnEnable()
        {
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

        private void WhenBeaconsChanged(HVRNPSFinder finder, List<HVRNPSBeacon> inBeacons)
        {
            beacons = inBeacons.ToArray();
        }

        private void Update()
        {
            _currentScale = transform.lossyScale.x;
            _girthRadiusInWorldSpace = girthRadius * _currentScale;
            HVRQuery.Instance.TryUpdateBeaconPositions();
            SortBeacons();
            IgnoreBeaconsFurtherThan(_totalLength + (MarginDistance + FalloffDistance) * _currentScale);
            DeformElements(_sortedBeacons);
        }

        private void IgnoreBeaconsFurtherThan(float maxDistance)
        {
            for (var i = 0; i < _sortedBeacons.Count; i++)
            {
                var center = _sortedBeacons[i].CalculateCenter(_girthRadiusInWorldSpace);
                if (Vector3.Distance(center, transform.position) > maxDistance)
                {
                    _sortedBeacons.RemoveRange(i, _sortedBeacons.Count - i);
                    return;
                }
            }
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
            
            var rootPosition = transform.position;
            _sortedBeacons.Sort((a, b) => (a.CalculateCenter(_girthRadiusInWorldSpace) - rootPosition).magnitude.CompareTo((b.CalculateCenter(_girthRadiusInWorldSpace) - rootPosition).magnitude));
            for (var index = 0; index < _sortedBeacons.Count -1; index++)
            {
                HVRNPSBeacon sortedBeacon = _sortedBeacons[index];
                if (sortedBeacon.passage == HVRNPSPassage.Termination)
                {
                    _sortedBeacons.RemoveRange(index + 1, _sortedBeacons.Count - index - 1);
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            HVRQuery.Instance.Dispose();
        }

        public void DeformElements(List<HVRNPSBeacon> inputBeacons)
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
            CalculateCurve(points, transform, inputBeacons);
            
            // I'm not sure when this can happen. This might be too defensive
            if (points.Count < 2)
            {
                FullyApplyIdle();
                return;
            }
            
            var firstPosition = inputBeacons[0].CalculateCenter(_girthRadiusInWorldSpace);
            var distanceToFirstPosition = Vector3.Distance(elements[0].transform.position, firstPosition);
            var falloff = FalloffDistance * _currentScale;
            var margin = MarginDistance * _currentScale;
            _curveApplies01 = Mathf.InverseLerp(_totalLength + margin + falloff, _totalLength + margin, distanceToFirstPosition);

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
                    Quaternion.LookRotation(forward, lastUpVector) * segment.standardToModel
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

        private void CalculateCurve(List<NPSPoint> points, Transform referential, List<HVRNPSBeacon> beacons)
        {
            var currentPos = referential.position;
            var currentDir = referential.forward;
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
                        
                        var nextPos = beacon.CalculateCenter(_girthRadiusInWorldSpace);
                        
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
                        
                        NPSMath.PrepareSeilerInterpolation(currentPos, nextPos, currentDir * _currentScale, nextDir * _currentScale, out var b0, out var b3, out var s1, out var s2);
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
                        nextConstriction = beacon.ActualConstriction() == HVRNPSConstriction.ConstrictToHide ? 0f : nextConstriction;
                    }
                }
            }

            if (lastBeacon != null)
            {
                var dirNormalized = currentDir.normalized;
                
                // for (var f = 0.1f; f <= _totalLength * 2; f += 0.1f)
                // {
                    // points.Add(new NPSPoint(currentPos + dirNormalized * f, nextConstriction));
                // }
                points.Add(new NPSPoint(currentPos + dirNormalized * 0.1f, nextConstriction));
                points.Add(new NPSPoint(currentPos + dirNormalized * (_totalLength * 2), nextConstriction));
                
                var color = k == 0 ? Color.cyan : Color.green;
                color.a = 0.5f;
                Debug.DrawLine(currentPos, currentPos + dirNormalized * (_totalLength * 0.5f), color, 0.01f);
            }
        }

#if UNITY_EDITOR // (AUDIT): UnityEditor function
        private void OnDrawGizmos()
        {
            {
                var rootPos = elements[0].position;
                var lastPos = elements[^1].position;
                var tipNormal = (elements[^1].rotation * _memory[^1].modelToStandard)  * Vector3.forward;
                var tipPos = lastPos + tipNormal * (tipLength * _currentScale);
                var normal = rootPos - lastPos;
                Handles.color = Color.red;
                Handles.DrawWireDisc(rootPos, normal, _girthRadiusInWorldSpace);
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(tipPos, tipNormal, _girthRadiusInWorldSpace);
            
                for (var i = 0; i < elements.Length; i++)
                {
                    Handles.color = i % 2 == 0 ? Color.red : Color.yellow;
                    Handles.DrawLine(elements[i].position, i == elements.Length - 1 ? tipPos : elements[i + 1].position);
                }

                if (Application.isPlaying && idleProxies.Length > 0 && _curveApplies01 < 1f && _curveApplies01 > 0f)
                {
                    for (var i = 0; i < idleProxies.Length - 1; i++)
                    {
                        Handles.color = i % 2 == 0 ? Color.red : Color.yellow;
                        Handles.DrawLine(idleProxies[i].position, idleProxies[i + 1].position);
                    }
                }
            }
            
            foreach (var beacon in beacons)
            {
                if (beacon == null) continue;
                
                var beaconPos = beacon.CalculateCenter(_girthRadiusInWorldSpace);
                var rotation = beacon.transform.rotation;
                var normal = rotation * Vector3.forward;

                Handles.color = beacon.passage == HVRNPSPassage.Termination ? Color.red : Color.green;
                
                var actualDirectionality = beacon.ActualDirectionality();
                if (actualDirectionality is HVRNPSDirectionality.OneWay or HVRNPSDirectionality.TwoWay)
                {
                    Handles.ArrowHandleCap(0, beaconPos, rotation, _girthRadiusInWorldSpace, EventType.Repaint);
                }
                if (actualDirectionality is HVRNPSDirectionality.ReverseWay or HVRNPSDirectionality.TwoWay)
                {
                    Handles.ArrowHandleCap(0, beaconPos, rotation * Quaternion.Euler(0, 180, 0), _girthRadiusInWorldSpace, EventType.Repaint);
                }
                if (actualDirectionality == HVRNPSDirectionality.AlongNormalPlane)
                {
                    var planeNormal = beacon.transform.up;
                    for (var degrees = 0; degrees < 360; degrees += 45)
                    {
                        Handles.ArrowHandleCap(0, beaconPos, Quaternion.AngleAxis(degrees, planeNormal) * rotation, _girthRadiusInWorldSpace, EventType.Repaint);
                    }
                    Handles.DrawLine(beacon.transform.position, beacon.transform.position + planeNormal * _girthRadiusInWorldSpace * 2);
                }
                else
                {
                    Handles.DrawWireDisc(beaconPos, normal, _girthRadiusInWorldSpace);
                }
                Handles.DrawWireDisc(beacon.transform.position, normal, _girthRadiusInWorldSpace * 0.1f);
            }

            if (!Application.isPlaying)
            {
                SortBeacons();
                // CalculateCurve(new List<NPSPoint>(), this, _sortedBeacons);
            }
        }
#endif

        private struct NPSSegment
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float segmentLength;
            public Quaternion modelToStandard;
            public Quaternion standardToModel;

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