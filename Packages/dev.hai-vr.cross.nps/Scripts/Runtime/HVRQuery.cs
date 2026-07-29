using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;

namespace HVR.Query
{
    public interface IHVRBeacon
    {
        Transform AsTransform { get; }
    }

    public interface IHVRFinder
    {
        Transform AsTransform { get; }
        float Range { get; }
    }
    
    /// Every 0.1 second, we will check if beacons have entered or exited the range of finders.
    /// The processing is done asynchronously using a compute shader.
    public class HVRQuery
    {
        private const float TimeDelaySeconds = 0.1f;
        
        private static readonly int BeaconPositions = Shader.PropertyToID("BeaconPositions");
        private static readonly int FinderPositions = Shader.PropertyToID("FinderPositions");
        private static readonly int FinderRangesSq = Shader.PropertyToID("FinderRangesSq");
        private static readonly int Results = Shader.PropertyToID("Results");
        private static readonly int BeaconCount = Shader.PropertyToID("BeaconCount");
        private static readonly int FinderCount = Shader.PropertyToID("FinderCount");

        public delegate void BeaconEnterOrExit(IHVRBeacon beacon, bool isEntering);
        
        public static HVRQuery Instance { get; private set; } = new();
        
        private readonly List<IHVRBeacon> _beacons = new();
        private readonly List<IHVRFinder> _finderKeys = new();
        private readonly Dictionary<IHVRFinder, BeaconEnterOrExit> _finderToCallbackDict = new();
        private readonly Dictionary<IHVRFinder, HashSet<IHVRBeacon>> _finderToBeaconsDict = new();

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
        
        public void Register(IHVRBeacon beacon)
        {
            if (!_beacons.Contains(beacon)) _beacons.Add(beacon);
        }

        public void Unregister(IHVRBeacon beacon)
        {
            _beacons.Remove(beacon);
            foreach (var finderPair in _finderToBeaconsDict)
            {
                var finder = finderPair.Key;
                var finderToBeacons = finderPair.Value;
                if (finderToBeacons.Remove(beacon))
                {
                    _finderToCallbackDict[finder](beacon, false);
                }
            }
        }

        public void Unregister(IHVRFinder finder)
        {
            if (_finderToBeaconsDict.TryGetValue(finder, out var finderToBeacons))
            {
                _tempBeacons.Clear();
                _tempBeacons.AddRange(finderToBeacons);
                foreach (var beacon in _tempBeacons)
                {
                    _finderToCallbackDict[finder](beacon, false);
                }
            }

            _finderKeys.Remove(finder);
            _finderToCallbackDict.Remove(finder);
            _finderToBeaconsDict.Remove(finder);
        }

        public void Register(IHVRFinder finder, BeaconEnterOrExit beaconEnterOrExit)
        {
            if (!_finderToCallbackDict.ContainsKey(finder))
            {
                _finderKeys.Add(finder);
                _finderToBeaconsDict[finder] = new HashSet<IHVRBeacon>();
            }
            _finderToCallbackDict[finder] = beaconEnterOrExit;
        }

        public void TryUpdateBeaconPositions()
        {
            ScheduleUpdateBeaconPositions();
            ResolveUpdateBeaconPositions();
        }

        public void ScheduleUpdateBeaconPositions()
        {
            if (_isComputeScheduled) return;

            if (Time.time - _lastCheckTime < TimeDelaySeconds) return;

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

            var beaconPositions = new Vector3[beaconCount];
            for (var i = 0; i < beaconCount; i++)
            {
                beaconPositions[i] = _beacons[i].AsTransform.position;
            }
            _beaconPositionsBuffer.SetData(beaconPositions);

            var finderPositions = new Vector3[finderCount];
            var finderRangesSq = new float[finderCount];
            for (var i = 0; i < finderCount; i++)
            {
                var finder = _finderKeys[i];
                finderPositions[i] = finder.AsTransform.position;
                finderRangesSq[i] = finder.Range * finder.Range;
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

        private readonly List<IHVRBeacon> _tempBeacons = new();

        private void ProcessResults()
        {
            var beaconCount = _scheduledBeaconCount;
            var finderCount = _scheduledFinderCount;

            for (var f = 0; f < finderCount; f++)
            {
                var finder = _finderKeys[f];
                var onBeaconEnterOrExit = _finderToCallbackDict[finder];
                var containedBeacons = _finderToBeaconsDict[finder];

                for (var b = 0; b < beaconCount; b++)
                {
                    var beacon = _beacons[b];
                    var isInside = _resultsData[f * beaconCount + b] != 0;
                    var wasInside = containedBeacons.Contains(beacon);

                    if (isInside && !wasInside)
                    {
                        containedBeacons.Add(beacon);
                        onBeaconEnterOrExit.Invoke(beacon, true);
                    }
                    else if (!isInside && wasInside)
                    {
                        containedBeacons.Remove(beacon);
                        onBeaconEnterOrExit.Invoke(beacon, false);
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