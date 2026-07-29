using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;

namespace HVR.NPS
{
    public class HVRNPSQuery
    {
        private static readonly int BeaconPositions = Shader.PropertyToID("BeaconPositions");
        private static readonly int FinderPositions = Shader.PropertyToID("FinderPositions");
        private static readonly int FinderRangesSq = Shader.PropertyToID("FinderRangesSq");
        private static readonly int Results = Shader.PropertyToID("Results");
        private static readonly int BeaconCount = Shader.PropertyToID("BeaconCount");
        private static readonly int FinderCount = Shader.PropertyToID("FinderCount");

        public delegate void BeaconEnterOrExit(HVRNPSBeacon beacon, bool isEntering);
        
        public static HVRNPSQuery Instance { get; private set; } = new();
        
        private readonly List<HVRNPSBeacon> _beacons = new();
        private readonly List<HVRNPSFinder> _finderKeys = new();
        private readonly Dictionary<HVRNPSFinder, BeaconEnterOrExit> _finders = new();
        private readonly Dictionary<HVRNPSFinder, HashSet<HVRNPSBeacon>> _findersToBeacons = new();

        private ComputeShader _proximityShader;
        private AsyncOperationHandle<ComputeShader> _shaderHandle;
        private ComputeBuffer _beaconPositionsBuffer;
        private ComputeBuffer _finderPositionsBuffer;
        private ComputeBuffer _finderRangesSqBuffer;
        private ComputeBuffer _resultsBuffer;
        
        private uint[] _resultsData;
        private bool _isComputeScheduled;
        private int _scheduledBeaconCount;
        private int _scheduledFinderCount;

        private bool _isDataReady;
        private float _lastCheckTime;

        private void EnsureShaderLoaded()
        {
            if (_proximityShader == null && !_shaderHandle.IsValid())
            {
                _shaderHandle = Addressables.LoadAssetAsync<ComputeShader>("HVR.NPS.Proximity");
                _shaderHandle.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        _proximityShader = handle.Result;
                    }
                };
            }
        }
        
        public void Register(HVRNPSBeacon beacon)
        {
            if (!_beacons.Contains(beacon)) _beacons.Add(beacon);
        }

        public void Unregister(HVRNPSBeacon beacon)
        {
            _beacons.Remove(beacon);
            foreach (var finderPair in _findersToBeacons)
            {
                var finder = finderPair.Key;
                var finderToBeacons = finderPair.Value;
                if (finderToBeacons.Remove(beacon))
                {
                    _finders[finder](beacon, false);
                }
            }
        }

        public void Unregister(HVRNPSFinder finder)
        {
            if (_findersToBeacons.TryGetValue(finder, out var finderToBeacons))
            {
                _tempBeacons.Clear();
                _tempBeacons.AddRange(finderToBeacons);
                foreach (var beacon in _tempBeacons)
                {
                    _finders[finder](beacon, false);
                }
            }

            _finderKeys.Remove(finder);
            _finders.Remove(finder);
            _findersToBeacons.Remove(finder);
        }

        public void Register(HVRNPSFinder finder, BeaconEnterOrExit beaconEnterOrExit)
        {
            if (!_finders.ContainsKey(finder))
            {
                _finderKeys.Add(finder);
                _findersToBeacons[finder] = new HashSet<HVRNPSBeacon>();
            }
            _finders[finder] = beaconEnterOrExit;
        }

        public void TryUpdateBeaconPositions()
        {
            ScheduleUpdateBeaconPositions();
            ResolveUpdateBeaconPositions();
        }

        public void ScheduleUpdateBeaconPositions()
        {
            if (_isComputeScheduled) return;

            if (Time.time - _lastCheckTime < 0.1f) return;

            var beaconCount = _beacons.Count;
            var finderCount = _finderKeys.Count;

            if (beaconCount == 0 || finderCount == 0)
            {
                return;
            }

            EnsureShaderLoaded();
            if (_proximityShader == null) return;

            if (_beaconPositionsBuffer == null || _beaconPositionsBuffer.count != beaconCount)
            {
                _beaconPositionsBuffer?.Release();
                _beaconPositionsBuffer = new ComputeBuffer(beaconCount, sizeof(float) * 3);
            }

            if (_finderPositionsBuffer == null || _finderPositionsBuffer.count != finderCount)
            {
                _finderPositionsBuffer?.Release();
                _finderPositionsBuffer = new ComputeBuffer(finderCount, sizeof(float) * 3);
                _finderRangesSqBuffer?.Release();
                _finderRangesSqBuffer = new ComputeBuffer(finderCount, sizeof(float));
            }

            var totalResults = finderCount * beaconCount;
            if (_resultsBuffer == null || _resultsBuffer.count != totalResults)
            {
                _resultsBuffer?.Release();
                _resultsBuffer = new ComputeBuffer(totalResults, sizeof(uint));
                _resultsData = new uint[totalResults];
            }

            var beaconPositions = new float3[beaconCount];
            for (var i = 0; i < beaconCount; i++)
            {
                beaconPositions[i] = _beacons[i].transform.position;
            }
            _beaconPositionsBuffer.SetData(beaconPositions);

            var finderPositions = new float3[finderCount];
            var finderRangesSq = new float[finderCount];
            for (var i = 0; i < finderCount; i++)
            {
                var finder = _finderKeys[i];
                finderPositions[i] = finder.transform.position;
                finderRangesSq[i] = finder.range * finder.range;
            }
            _finderPositionsBuffer.SetData(finderPositions);
            _finderRangesSqBuffer.SetData(finderRangesSq);

            var kernel = _proximityShader.FindKernel("CSMain");
            _proximityShader.SetBuffer(kernel, BeaconPositions, _beaconPositionsBuffer);
            _proximityShader.SetBuffer(kernel, FinderPositions, _finderPositionsBuffer);
            _proximityShader.SetBuffer(kernel, FinderRangesSq, _finderRangesSqBuffer);
            _proximityShader.SetBuffer(kernel, Results, _resultsBuffer);
            _proximityShader.SetInt(BeaconCount, beaconCount);
            _proximityShader.SetInt(FinderCount, finderCount);

            _proximityShader.Dispatch(kernel, Mathf.CeilToInt(finderCount / 64f), 1, 1);
            
            _lastCheckTime = Time.time;
            _isComputeScheduled = true;
            _isDataReady = false;
            _scheduledBeaconCount = beaconCount;
            _scheduledFinderCount = finderCount;

            AsyncGPUReadback.Request(_resultsBuffer, request =>
            {
                if (request.hasError)
                {
                    _isComputeScheduled = false;
                    return;
                }

                var data = request.GetData<uint>();
                data.CopyTo(_resultsData);
                _isDataReady = true;
            });
        }

        public void ResolveUpdateBeaconPositions()
        {
            if (!_isComputeScheduled || !_isDataReady) return;

            ProcessResults();
            _isComputeScheduled = false;
            _isDataReady = false;
        }

        private readonly List<HVRNPSBeacon> _tempBeacons = new();

        private void ProcessResults()
        {
            var beaconCount = _scheduledBeaconCount;
            var finderCount = _scheduledFinderCount;

            for (var f = 0; f < finderCount; f++)
            {
                var finder = _finderKeys[f];
                var onBeaconEnterOrExit = _finders[finder];
                var containedBeacons = _findersToBeacons[finder];

                for (var b = 0; b < beaconCount; b++)
                {
                    var beacon = _beacons[b];
                    var isInside = _resultsData[f * beaconCount + b] != 0;
                    var wasInside = containedBeacons.Contains(beacon);

                    if (isInside && !wasInside)
                    {
                        containedBeacons.Add(beacon);
                        onBeaconEnterOrExit(beacon, true);
                    }
                    else if (!isInside && wasInside)
                    {
                        containedBeacons.Remove(beacon);
                        onBeaconEnterOrExit(beacon, false);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_shaderHandle.IsValid())
            {
                Addressables.Release(_shaderHandle);
            }
            _beaconPositionsBuffer?.Release();
            _finderPositionsBuffer?.Release();
            _finderRangesSqBuffer?.Release();
            _resultsBuffer?.Release();
        }
    }
}