using UnityEngine;

namespace HVR.NPS.ForCilbox
{
    [Cilboxable]
    public class NPSCilMath
    {
        public Vector3[] PrepareSeilerInterpolation_result = new Vector3[4]; // CILBOX: Cilbox doesn't accept "out var" nor "tuple" return values at this time of writing.

        public void PrepareSeilerInterpolation(Vector3 pos0, Vector3 pos1, Vector3 direction0, Vector3 direction1)
        {
            var b0 = pos0;
            var b3 = pos1;
            var b1 = b0 + direction0;
            var b2 = b3 + direction1;

            FromBezierToSeiler(b0, b1, b2, b3);
            PrepareSeilerInterpolation_result[0] = b0;
            PrepareSeilerInterpolation_result[1] = b3;
        }

        private void FromBezierToSeiler(Vector3 b0, Vector3 b1, Vector3 b2, Vector3 b3)
        {
            var s1 = 3 * b1 - b0 - b3;
            var s2 = 3 * b2 - b3 - b0;
            PrepareSeilerInterpolation_result[2] = s1;
            PrepareSeilerInterpolation_result[3] = s2;
        }

        /// Based on https://www.cemyuksel.com/research/seilers_interpolation/
        public Vector3 SeilerInterpolate(Vector3 b0, Vector3 b3, Vector3 s1, Vector3 s2, float t)
        {
            var b03 = Vector3.Lerp(b0, b3, t);
            var s12 = Vector3.Lerp(s1, s2, t);
            return Vector3.Lerp(b03, s12, (1 - t) * t);
        }

        /**
         * Makes the vector to be straightened perpendicular to the axis.
         */
        public Vector3 Straighten(Vector3 toStraighten, Vector3 onAxis)
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