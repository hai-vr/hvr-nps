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
using UnityEditor;
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Chain")]
    public class HVRNPSChain : MonoBehaviour
    {
        public HVRNPSFinder finder;

        public Transform[] elements;
        private NPSSegment[] _memory;
        
        public float girthRadius = 0.5f;
        public float tipLength = 0.1f;

        private float _totalLength;
        public HVRNPSBeacon[] beacons;
        private Quaternion _reorient;
        
        private List<HVRNPSBeacon> _sortedBeacons = new();

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

        private void WhenBeaconsChanged(HVRNPSFinder finder, List<HVRNPSBeacon> inBeacons)
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
            _sortedBeacons.Sort((a, b) => (a.CalculateCenter(girthRadius) - rootPosition).magnitude.CompareTo((b.CalculateCenter(girthRadius) - rootPosition).magnitude));
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
            // This should work similarly to an IK system (see HVR IK documentation):
            // - Find a good matching curve that pass through the beacons, although not necessarily through all of them.
            // - Based on the real distances, calculate the points.
            // - Those points restrict two freedoms from the rotation. Calculate the rolls.
            // - Apply the changes in rotation.

            var points = new List<NPSPoint>();
            CalculateCurve(points, this, inputBeacons);

            if (points.Count < 2) return;

            var distances = new List<float>();
            distances.Add(0);
            for (var i = 1; i < points.Count; i++)
            {
                distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1].position, points[i].position));
            }

            _reorient = NPSMath.FromToOrientation(Vector3.forward, Vector3.right, Vector3.up, Vector3.forward);
        
            var currentDist = 0f;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var segment = _memory[i];
                
                var pos = SampleCurve(points, distances, currentDist);
                var nextPos = SampleCurve(points, distances, currentDist + segment.segmentLength);
                
                var forward = (nextPos.position - pos.position).normalized;
                if (forward == Vector3.zero) forward = transform.up;
                
                element.position = pos.position;
                element.rotation = Quaternion.LookRotation(forward, transform.up) * _reorient;
                
                var constriction = pos.constriction;
                element.localScale = new Vector3(constriction, 1f, constriction);
                
                currentDist += segment.segmentLength;
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

        private void CalculateCurve(List<NPSPoint> points, HVRNPSChain chain, List<HVRNPSBeacon> beacons)
        {
            var currentPos = chain.transform.position;
            var currentDir = chain.transform.forward;
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

        private void OnDrawGizmos()
        {
            {
                var rootPos = elements[0].position;
                var lastPos = elements[^1].position;
                var tipNormal = elements[^1].up.normalized;
                var tipPos = lastPos + tipNormal * tipLength;
                var normal = rootPos - lastPos;
                Handles.color = Color.red;
                Handles.DrawWireDisc(rootPos, normal, girthRadius);
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(tipPos, tipNormal, girthRadius);
            
                for (int i = 0; i < elements.Length; i++)
                {
                    Handles.color = i % 2 == 0 ? Color.red : Color.yellow;
                    Handles.DrawLine(elements[i].position, i == elements.Length - 1 ? tipPos : elements[i + 1].position);
                }
            }
            
            foreach (var beacon in beacons)
            {
                if (beacon == null) continue;
                
                var beaconPos = beacon.CalculateCenter(girthRadius);
                var rotation = beacon.transform.rotation;
                var normal = rotation * Vector3.forward;

                Handles.color = beacon.passage == HVRNPSPassage.Termination ? Color.red : Color.green;
                
                var actualDirectionality = beacon.ActualDirectionality();
                if (actualDirectionality is HVRNPSDirectionality.OneWay or HVRNPSDirectionality.TwoWay)
                {
                    Handles.ArrowHandleCap(0, beaconPos, rotation, girthRadius, EventType.Repaint);
                }
                if (actualDirectionality is HVRNPSDirectionality.ReverseWay or HVRNPSDirectionality.TwoWay)
                {
                    Handles.ArrowHandleCap(0, beaconPos, rotation * Quaternion.Euler(0, 180, 0), girthRadius, EventType.Repaint);
                }
                if (actualDirectionality == HVRNPSDirectionality.AlongNormalPlane)
                {
                    var planeNormal = beacon.transform.up;
                    for (var degrees = 0; degrees < 360; degrees += 45)
                    {
                        Handles.ArrowHandleCap(0, beaconPos, Quaternion.AngleAxis(degrees, planeNormal) * rotation, girthRadius, EventType.Repaint);
                    }
                    Handles.DrawLine(beacon.transform.position, beacon.transform.position + planeNormal * girthRadius * 2);
                }
                else
                {
                    Handles.DrawWireDisc(beaconPos, normal, girthRadius);
                }
                Handles.DrawWireDisc(beacon.transform.position, normal, girthRadius * 0.1f);
            }

            if (!Application.isPlaying)
            {
                SortBeacons();
                // CalculateCurve(new List<NPSPoint>(), this, _sortedBeacons);
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