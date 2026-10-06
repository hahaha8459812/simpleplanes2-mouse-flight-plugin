using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal sealed class PhysicsSampleHistory
    {
        private bool _hasPrevious;
        private float _time;
        private Vector3 _omega;
        public void Reset() { _hasPrevious = false; }
        public bool Observe(float time, Vector3 omega, Vector3 right, out float dt, out float acceleration, out bool valid)
        {
            dt = 0f; acceleration = 0f; valid = false;
            if (!FlightRecorder.Finite(time) || !FlightRecorder.Finite(omega) || !FlightRecorder.Finite(right))
            { Reset(); return false; }
            if (_hasPrevious)
            {
                dt = time - _time;
                if (dt <= 0.000001f) return false;
                valid = dt <= 0.25f;
                if (valid)
                {
                    acceleration = -Vector3.Dot((omega - _omega) / dt, right) * Mathf.Rad2Deg;
                    valid = FlightRecorder.Finite(acceleration);
                }
            }
            _hasPrevious = true; _time = time; _omega = omega;
            return true;
        }
    }
}
