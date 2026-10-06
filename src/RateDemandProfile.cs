using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Shape increases in commanded rotation, without delaying deceleration or reversal.
    internal sealed class RateDemandProfile
    {
        private float _value;
        public void Reset() { _value = 0f; }
        public float Update(float wanted, float dt, float acceleration)
        {
            if (dt > 0.1f) Reset();
            if (acceleration <= 0f || wanted * _value < 0f || Mathf.Abs(wanted) <= Mathf.Abs(_value))
                _value = wanted;
            else
                _value = Mathf.MoveTowards(_value, wanted, acceleration * dt);
            return _value;
        }
    }
}
