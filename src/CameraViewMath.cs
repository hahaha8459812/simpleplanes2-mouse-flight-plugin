using System;

namespace SimplePlanes2MouseFlight
{
    internal static class CameraViewMath
    {
        public static void HorizontalRight(float forwardX,float forwardZ,float previousX,float previousZ,out float x,out float z)
        {
            float length=(float)Math.Sqrt(forwardX*forwardX+forwardZ*forwardZ);
            if(length>.02f) { x=forwardZ/length; z=-forwardX/length; }
            else
            {
                length=(float)Math.Sqrt(previousX*previousX+previousZ*previousZ);
                x=length>.001f?previousX/length:1; z=length>.001f?previousZ/length:0;
            }
        }
        public static float SmoothTurn(float previous,float rate,float dt)
        {
            if(float.IsNaN(rate)||float.IsInfinity(rate)) rate=0;
            float target=Math.Max(-1,Math.Min(1,rate));
            return previous+(target-previous)*(1-(float)Math.Exp(-Math.Max(0,Math.Min(.1f,dt))/.22f));
        }
        // Exclude the complete ring texture footprint, with a clearance for line thickness.
        public static bool LinkInterval(float distance,float ringClearance,float noseClearance,out float start,out float end)
        {
            start=Math.Max(0,ringClearance); end=distance-Math.Max(0,noseClearance);
            return end-start>=2;
        }
        public static void DashPattern(float baseStep,float length,float screenHeight,out float dash,out float gap)
        {
            float step=Math.Max(4,baseStep);
            dash=step*.25f;
            gap=Math.Max(dash*3,step*(.75f+Math.Max(0,length)/Math.Max(1,screenHeight)*2));
        }
    }
}
