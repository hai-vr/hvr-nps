using Unity.Mathematics;

namespace HVR.NPS
{
    internal static class NPSMath
    {
        internal static void PrepareSeilerInterpolation(float3 pos0, float3 pos1, float3 direction0, float3 direction1, out float3 b0, out float3 b3, out float3 s1, out float3 s2)
        {
            b0 = pos0;
            b3 = pos1;
            var b1 = b0 + direction0;
            var b2 = b3 + direction1;

            FromBezierToSeiler(b0, b1, b2, b3, out s1, out s2);
        }

        private static void FromBezierToSeiler(float3 b0, float3 b1, float3 b2, float3 b3, out float3 s1, out float3 s2)
        {
            s1 = 3 * b1 - b0 - b3;
            s2 = 3 * b2 - b3 - b0;
        }

        /// Based on https://www.cemyuksel.com/research/seilers_interpolation/
        internal static float3 SeilerInterpolate(float3 b0, float3 b3, float3 s1, float3 s2, float t)
        {
            var b03 = math.lerp(b0, b3, t);
            var s12 = math.lerp(s1, s2, t);
            return math.lerp(b03, s12, (1 - t) * t);
        }
        
        public static quaternion FromToOrientation(float3 fromDirection, float3 toDirection, float3 fromUpwards,
            float3 toUpwards)
        {
            var fromRotation = LookRotationSafe(fromDirection, fromUpwards);
            var toRotation = LookRotationSafe(toDirection, toUpwards);
            return math.mul(toRotation, math.inverse(fromRotation));
        }
    
        private static quaternion LookRotationSafe(float3 forward, float3 upward)
        {
            return quaternion.LookRotationSafe(forward, upward);
        }
        
        /**
         * Returns a normalized vector perpendicular to the axis.
         * That vector is straightened from a vector that is:
         * - similar to the first vector if that first vector is perpendicular to the axis,
         * - similar to the second vector if that first vector is not perpendicular to the axis.
         */
        public static float3 Screwdriver(float3 first, float3 second, float3 axis)
        {
            return math.normalize(Straighten(ImpracticalScrewdriver(first, second, axis), axis));
        }

        /**
         * Returns a vector that is:
         * - similar to the first vector if that first vector is perpendicular to the axis,
         * - similar to the second vector if that first vector is not perpendicular to the axis.
         */
        private static float3 ImpracticalScrewdriver(float3 first, float3 second, float3 axis)
        {
            return math.lerp(first, second, math.dot(axis, first));
        }

        /**
         * Makes the vector to be straightened perpendicular to the axis.
         */
        public static float3 Straighten(float3 toStraighten, float3 onAxis)
        {
            return math.normalize(math.cross(math.cross(onAxis, toStraighten), onAxis)) * math.length(toStraighten);
        }
    }
}