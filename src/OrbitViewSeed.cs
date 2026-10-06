using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal static class OrbitViewSeed
    {
        // Native orbit angles are world-space pitch/yaw, independent of aircraft bank and aim.
        public static Vector2 Angles(Vector3 cameraToPivot)
        {
            float horizontal = Mathf.Sqrt(cameraToPivot.x * cameraToPivot.x + cameraToPivot.z * cameraToPivot.z);
            return new Vector2(Mathf.Clamp(-Mathf.Atan2(cameraToPivot.y, horizontal) * Mathf.Rad2Deg, -89f, 89f),
                Mathf.Atan2(cameraToPivot.x, cameraToPivot.z) * Mathf.Rad2Deg);
        }
    }
}
