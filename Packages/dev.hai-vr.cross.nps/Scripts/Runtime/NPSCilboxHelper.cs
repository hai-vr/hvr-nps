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

using System;
using UnityEngine;

namespace HVR.NPS
{
    // A class that represents a list of beacons. It also retains the data structures needed to sort it.
    public class HVRNPSSortedBeaconArray
    {
        public HVRNPSBeacon[] beacons = new HVRNPSBeacon[10];
        public int size;
        
        public float[] comparisonBuffer = new float[10];
        public int[] sortingBuffer = new int[10];
    }
    
    /// An unsorted collection of beacons. The order of the list may change when elements are removed.
    public class HVRNPSUnsortedBeaconCollection
    {
        public HVRNPSBeacon[] beacons = new HVRNPSBeacon[10];
        public int size;

        public void AddIfNotExists(HVRNPSBeacon beacon)
        {
            for (var i = 0; i < size; i++)
            {
                if (beacons[i] == beacon)
                {
                    return;
                }
            }

            Add(beacon);
        }

        public void Add(HVRNPSBeacon beacon)
        {
            if (size == beacons.Length)
            {
                var newBeacons = new HVRNPSBeacon[beacons.Length + 10];
                Array.Copy(beacons, newBeacons, beacons.Length);
                beacons = newBeacons;
            }

            beacons[size] = beacon;
            size++;
        }

        public void Remove(HVRNPSBeacon beacon)
        {
            for (var i = 0; i < size; i++)
            {
                if (beacons[i] == beacon)
                {
                    // Take the last element and put it where we found that.
                    beacons[i] = beacons[size - 1];
                    size--;
                    return;
                }
            }
        }
    }
    
    public static class NPSCilboxHelper
    {
        public static void SortBeacons(HVRNPSUnsortedBeaconCollection beacons, HVRNPSSortedBeaconArray sortedBeacons, Vector3 rootPosition, float girthRadius)
        {
            if (beacons.size > sortedBeacons.beacons.Length)
            {
                sortedBeacons.beacons = new HVRNPSBeacon[(beacons.size / 10 + 1) * 10];
                sortedBeacons.comparisonBuffer = new float[sortedBeacons.beacons.Length];
                sortedBeacons.sortingBuffer = new int[sortedBeacons.beacons.Length];
            }

            sortedBeacons.size = beacons.size;
            
            // Sort the array without using any API that requires generics (because of Cilbox)
            for (var i = 0; i < beacons.size; i++)
            {
                sortedBeacons.comparisonBuffer[i] = (beacons.beacons[i].CalculateCenter(girthRadius) - rootPosition).magnitude;
                sortedBeacons.sortingBuffer[i] = i;
            }
            if (beacons.size > 1)
            {
                QuickSort(sortedBeacons.sortingBuffer, sortedBeacons.comparisonBuffer, 0, beacons.size - 1);
            }
            for (var i = 0; i < beacons.size; i++)
            {
                sortedBeacons.beacons[i] = beacons.beacons[sortedBeacons.sortingBuffer[i]];
            }

            // Any beacon beyond the termination is ignored.
            for (var index = 0; index < sortedBeacons.size; index++)
            {
                if (sortedBeacons.beacons[index].passage == HVRNPSPassage.Termination)
                {
                    sortedBeacons.size = index + 1;
                    break;
                }
            }
        }

        private static void QuickSort(int[] sortingBuffer, float[] comparisonBuffer, int left, int right)
        {
            if (left >= right) return;

            var pivotIndex = Partition(sortingBuffer, comparisonBuffer, left, right);
            QuickSort(sortingBuffer, comparisonBuffer, left, pivotIndex - 1);
            QuickSort(sortingBuffer, comparisonBuffer, pivotIndex + 1, right);
        }

        private static int Partition(int[] sortingBuffer, float[] comparisonBuffer, int left, int right)
        {
            var pivotValue = comparisonBuffer[sortingBuffer[right]];
            var i = left - 1;

            for (var j = left; j < right; j++)
            {
                if (comparisonBuffer[sortingBuffer[j]] <= pivotValue)
                {
                    i++;
                    var temp = sortingBuffer[i];
                    sortingBuffer[i] = sortingBuffer[j];
                    sortingBuffer[j] = temp;
                }
            }

            var nextPivot = i + 1;
            var finalTemp = sortingBuffer[nextPivot];
            sortingBuffer[nextPivot] = sortingBuffer[right];
            sortingBuffer[right] = finalTemp;

            return nextPivot;
        }
    }
}