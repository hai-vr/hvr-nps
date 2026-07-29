using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HVR.NPS
{
    [AddComponentMenu("HVR/NPS/HVR NPS Chain")]
    public class HVRNPSChain : MonoBehaviour
    {
        public HVRNPSFinder finder;
        
        public Transform[] elements;
        private Segment[] _memory;
        
        public float girthRadius = 0.5f;
        public float tipLength = 0.1f;

        private void OnEnable()
        {
            if (_memory == null)
            {
                _memory = new Segment[elements.Length];
                for (var i = 0; i < elements.Length; i++)
                {
                    var segmentLength = i < elements.Length - 1
                        ? (elements[i + 1].position - elements[i].position).magnitude
                        : tipLength;
                    
                    _memory[i] = new Segment
                    {
                        transform = elements[i],
                        position = elements[i].localPosition,
                        rotation = elements[i].localRotation,
                        scale = elements[i].localScale,
                        segmentLength = segmentLength
                    };
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

        public HVRNPSBeacon[] beacons;
        private quaternion _reorient;
        
        private List<HVRNPSBeacon> _sortedBeacons = new();

        private void Update()
        {
            HVRNPSQuery.Instance.TryUpdateBeaconPositions();
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
            HVRNPSQuery.Instance.Dispose();
        }

        public void DeformElements(IEnumerable<HVRNPSBeacon> inputBeacons)
        {
            // This should work similarly to an IK system (see HVR IK documentation):
            // - Find a good matching curve that pass through the beacons, although not necessarily through all of them.
            // - Based on the real distances, calculate the points.
            // - Those points restrict two freedoms from the rotation. Calculate the rolls.
            // - Apply the changes in rotation.

            var points = new List<Vector3>();
            CalculateCurve(points, this, inputBeacons);

            if (points.Count < 2) return;

            var distances = new float[points.Count];
            distances[0] = 0;
            for (var i = 1; i < points.Count; i++)
            {
                distances[i] = distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }

            _reorient = NPSMath.FromToOrientation(math.forward(), math.right(), math.up(), math.forward());
        
            var currentDist = 0f;
            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var segment = _memory[i];
                
                var pos = SampleCurve(points, distances, currentDist);
                var nextPos = SampleCurve(points, distances, currentDist + segment.segmentLength);
                
                var forward = (nextPos - pos).normalized;
                if (forward == Vector3.zero) forward = transform.up;
                
                element.position = pos;
                element.rotation = Quaternion.LookRotation(forward, elements[0].transform.right) * _reorient;
                
                currentDist += segment.segmentLength;
            }
        }

        private Vector3 SampleCurve(List<Vector3> points, float[] distances, float distance)
        {
            if (distance <= 0) return points[0];
            if (distance >= distances[distances.Length - 1]) return points[points.Count - 1];

            for (var i = 0; i < distances.Length - 1; i++)
            {
                if (distance >= distances[i] && distance <= distances[i + 1])
                {
                    var t = (distance - distances[i]) / (distances[i + 1] - distances[i]);
                    return Vector3.Lerp(points[i], points[i + 1], t);
                }
            }
            return points[points.Count - 1];
        }

        private void CalculateCurve(List<Vector3> points, HVRNPSChain chain, IEnumerable<HVRNPSBeacon> beacons)
        {
            var currentPos = chain.transform.position;
            var currentDir = chain.transform.forward;
            var k = 0;
            HVRNPSBeacon lastBeacon = null;
            foreach (var beacon in beacons)
            {
                var nextPos = beacon.CalculateCenter(girthRadius);
                var nextDir = -beacon.transform.forward;
                NPSMath.PrepareSeilerInterpolation(currentPos, nextPos, currentDir, nextDir, out var b0, out var b3, out var s1, out var s2);
                var prev = NPSMath.SeilerInterpolate(b0, b3, s1, s2, 0f);
                points.Add(b0);
                
                var color = k == 0 ? Color.cyan : Color.green;
                for (var f = 0.1f; f < 1f; f += 0.1f)
                {
                    var pos = NPSMath.SeilerInterpolate(b0, b3, s1, s2, f);
                    points.Add(pos);
                    Debug.DrawLine(prev, pos, color, 0.01f);
                    prev = pos;
                }
                points.Add(b3);
                Debug.DrawLine(prev, b3, color, 0.01f);
                
                currentPos = nextPos;
                currentDir = -nextDir;
                k++;
                lastBeacon = beacon;
            }

            if (lastBeacon != null)
            {
                var lastPos = lastBeacon.CalculateCenter(girthRadius);
                points.Add(lastPos + lastBeacon.transform.forward * 10);
                
                var color = k == 0 ? Color.cyan : Color.green;
                color.a = 0.5f;
                Debug.DrawLine(lastPos, lastPos + lastBeacon.transform.forward * 2f, color, 0.01f);
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

                Handles.color = beacon.passage == HVRNPSPassage.Termination ? Color.red : Color.green;
                Handles.ArrowHandleCap(0, beaconPos, rotation, girthRadius, EventType.Repaint);
                var normal = rotation * Vector3.forward;
                Handles.DrawWireDisc(beaconPos, normal, girthRadius);
                Handles.DrawWireDisc(beacon.transform.position, normal, girthRadius * 0.1f);
            }

            if (!Application.isPlaying)
            {
                SortBeacons();
                CalculateCurve(new List<Vector3>(), this, _sortedBeacons);
            }
        }

        private struct Segment
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
    }
}