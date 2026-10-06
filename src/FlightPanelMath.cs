using System;

namespace SimplePlanes2MouseFlight
{
    internal static class FlightPanelMath
    {
        // The observer is on the positive bearing side and looks toward the origin.
        public static float ScreenRight(float x, float z, float bearing)
        { double angle = bearing * Math.PI / 180; return (float)(-x * Math.Cos(angle) + z * Math.Sin(angle)); }
        public static float GridShift(float travelled, float scale, float spacing)
        { return (travelled / scale) % spacing; }
        public static float GroundLevel(float referenceAltitude, float altitude, float scale)
        { return -1.5f + (referenceAltitude - altitude) / scale; }
        // Return the marker centre, inset by its radius at both ends of the track.
        public static float MeterCentre(float value, float start, float length, float diameter, bool bipolar)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 0;
            float fraction = bipolar ? (Math.Max(-1, Math.Min(1, value)) + 1) * .5f : Math.Max(0, Math.Min(1, value));
            float inset = Math.Min(Math.Max(0, diameter), Math.Max(0, length)) * .5f;
            return start + inset + fraction * Math.Max(0, length - 2 * inset);
        }
        public static float CameraBearing(float previous, float vx, float vz, float dt, bool initialized)
        {
            // Keep the last horizontal reference during vertical flight or low speed.
            if (vx * vx + vz * vz < 4) return previous;
            float target = (float)(Math.Atan2(vx, vz) * 180 / Math.PI) + 145;
            if (!initialized) return target;
            float delta = (target - previous) % 360;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            return previous + delta * (1 - (float)Math.Exp(-Math.Max(0, dt) / .35f));
        }
        public static bool ClipSegment(float depthA,float depthB,float nearLimit,out float start,out float end)
        {
            start=0; end=1;
            if(float.IsNaN(depthA)||float.IsInfinity(depthA)||float.IsNaN(depthB)||float.IsInfinity(depthB)) return false;
            if(depthA>nearLimit && depthB>nearLimit) return false;
            if(depthA>nearLimit) start=(nearLimit-depthA)/(depthB-depthA);
            else if(depthB>nearLimit) end=(nearLimit-depthA)/(depthB-depthA);
            return true;
        }
    }
}
