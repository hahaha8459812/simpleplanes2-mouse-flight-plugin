using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // A capture needs both proximity and slower pointing motion. Once captured,
    // small overshoot cannot repeatedly switch the aircraft back into banking.
    internal sealed class RollCaptureState
    {
        private float _entryTime, _capturedBlend;
        public bool Captured { get; private set; }
        public void Reset() { Captured = false; _entryTime = 0f; _capturedBlend = 1f; }
        public float Update(float dt, float off, float pointingRate, float geometricBlend,
            float enterAngle, float leaveAngle, float captureRate, float dwell)
        {
            if (dt > 0.1f) Reset();
            if (Captured && off > leaveAngle) Reset();
            if (!Captured)
            {
                _entryTime = off <= enterAngle && pointingRate <= captureRate ? _entryTime + dt : 0f;
                if (off <= enterAngle && pointingRate <= captureRate && _entryTime >= dwell)
                { Captured = true; _capturedBlend = geometricBlend; }
            }
            if (!Captured) return 1f;
            _capturedBlend = Mathf.Min(_capturedBlend, geometricBlend);
            return _capturedBlend;
        }
    }
}
