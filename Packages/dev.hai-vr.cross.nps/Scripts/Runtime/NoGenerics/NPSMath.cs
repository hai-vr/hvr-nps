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

using UnityEngine;

namespace HVR.NPS
{
    internal static class NPSMath
    {
        internal static void PrepareSeilerInterpolation(Vector3 pos0, Vector3 pos1, Vector3 direction0, Vector3 direction1, out Vector3 b0, out Vector3 b3, out Vector3 s1, out Vector3 s2)
        {
            b0 = pos0;
            b3 = pos1;
            var b1 = b0 + direction0;
            var b2 = b3 + direction1;

            FromBezierToSeiler(b0, b1, b2, b3, out s1, out s2);
        }
        
        private static void FromBezierToSeiler(Vector3 b0, Vector3 b1, Vector3 b2, Vector3 b3, out Vector3 s1, out Vector3 s2)
        {
            s1 = 3 * b1 - b0 - b3;
            s2 = 3 * b2 - b3 - b0;
        }

        /// Based on https://www.cemyuksel.com/research/seilers_interpolation/
        internal static Vector3 SeilerInterpolate(Vector3 b0, Vector3 b3, Vector3 s1, Vector3 s2, float t)
        {
            var b03 = Vector3.Lerp(b0, b3, t);
            var s12 = Vector3.Lerp(s1, s2, t);
            return Vector3.Lerp(b03, s12, (1 - t) * t);
        }
        
        public static Quaternion FromToOrientation(Vector3 fromDirection, Vector3 toDirection, Vector3 fromUpwards,
            Vector3 toUpwards)
        {
            var fromRotation = LookRotationSafe(fromDirection, fromUpwards);
            var toRotation = LookRotationSafe(toDirection, toUpwards);
            return toRotation * Quaternion.Inverse(fromRotation);
        }
    
        private static Quaternion LookRotationSafe(Vector3 forward, Vector3 upward)
        {
            return Quaternion.LookRotation(forward, upward);
        }
        
        /**
         * Returns a normalized vector perpendicular to the axis.
         * That vector is straightened from a vector that is:
         * - similar to the first vector if that first vector is perpendicular to the axis,
         * - similar to the second vector if that first vector is not perpendicular to the axis.
         */
        public static Vector3 Screwdriver(Vector3 first, Vector3 second, Vector3 axis)
        {
            return Straighten(ImpracticalScrewdriver(first, second, axis), axis).normalized;
        }

        /**
         * Returns a vector that is:
         * - similar to the first vector if that first vector is perpendicular to the axis,
         * - similar to the second vector if that first vector is not perpendicular to the axis.
         */
        private static Vector3 ImpracticalScrewdriver(Vector3 first, Vector3 second, Vector3 axis)
        {
            return Vector3.Lerp(first, second, Vector3.Dot(axis, first));
        }

        /**
         * Makes the vector to be straightened perpendicular to the axis.
         */
        public static Vector3 Straighten(Vector3 toStraighten, Vector3 onAxis)
        {
            return (Vector3.Cross(Vector3.Cross(onAxis, toStraighten), onAxis)).normalized * toStraighten.magnitude;
        }
        
        /// Imported from HVR IK.
        public static Vector3 ReprojectTwistToArm(Vector3 armDirection, Vector3 handDirection, Vector3 handTwist)
        {
            armDirection = armDirection.normalized;
            handDirection = handDirection.normalized;
            if (Vector3.Dot(armDirection, handDirection) >= 0.9999f)
            {
                // axis could have become NaN if this check were not in place.
                return handTwist;
            }
            
            var axis = Vector3.Cross(armDirection, handDirection).normalized;
            
            var axisArmCross = Vector3.Cross(axis, armDirection).normalized;
            var axisHandCross = Vector3.Cross(axis, handDirection).normalized;
            
            return axis * Vector3.Dot(handTwist, axis) + axisArmCross * Vector3.Dot(handTwist, axisHandCross);
        }
    }
}