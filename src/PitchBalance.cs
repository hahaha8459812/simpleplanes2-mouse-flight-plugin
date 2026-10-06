using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Nose-up stick convention. This learns a bounded equilibrium input, not aircraft authority.
    internal sealed class PitchBalance
    {
        public float Value { get; private set; }
        public bool Learning { get; private set; }
        public bool ResponseSeen { get; private set; }
        private float _stableTime, _responseAge, _probeTime, _probeStick, _probeRate, _responseDwell;
        private bool _hasProbe;

        public void Reset()
        {
            Value = 0f; Learning = ResponseSeen = _hasProbe = false;
            _stableTime = _responseAge = _probeTime = _probeStick = _probeRate = _responseDwell = 0f;
        }

        public void SuspendLearning()
        {
            // Preserve learned equilibrium, discard manual-maneuver response probes.
            Learning = ResponseSeen = _hasProbe = false;
            _stableTime = _responseAge = _probeTime = _responseDwell = 0f;
        }

        public void Update(float dt, float error, float rate, float rawRate, float rollRate,
            float bank, float lateralError, float pullGate, float forwardSpeed, float aimRate,
            float baseStick, float appliedWeight, bool actualValid, float actualStick,
            float previousStick, float gain, float limit, float slew)
        {
            Learning = false;
            if (dt <= 0f || dt > 0.1f || forwardSpeed < 10f || gain <= 0f || limit <= 0f)
            { Reset(); return; }
            _responseAge += dt;
            if (_responseAge > 60f) ResponseSeen = false;

            // A read-back control change followed by a matching rate change is evidence of
            // response, not a torque estimate. A frozen/no-response test must not accumulate.
            if (actualValid)
            {
                // Sustained motion in the applied pitch direction also permits learning.
                // This is only a conservative motion check, not proof of aerodynamic authority.
                if (Mathf.Abs(actualStick) > 0.03f && Mathf.Abs(rawRate) > 0.03f
                    && actualStick * rawRate > 0.001f)
                {
                    _responseDwell += dt;
                    if (_responseDwell >= 0.1f) { ResponseSeen = true; _responseAge = 0f; }
                }
                else _responseDwell = 0f;
                if (!_hasProbe) { _probeStick = actualStick; _probeRate = rawRate; _hasProbe = true; }
                _probeTime += dt;
                if (_probeTime >= 0.25f)
                {
                    float ds = actualStick - _probeStick, dr = rawRate - _probeRate;
                    if (Mathf.Abs(ds) > 0.001f && Mathf.Abs(dr) > 0.002f && ds * dr > 0f)
                    { ResponseSeen = true; _responseAge = 0f; }
                    _probeTime = 0f; _probeStick = actualStick; _probeRate = rawRate;
                }
            }
            else { _hasProbe = false; _probeTime = _responseDwell = 0f; }

            bool stable = actualValid && ResponseSeen && appliedWeight > 0.99f
                && Mathf.Abs(error) < 5f && Mathf.Abs(lateralError) < 1.5f
                && Mathf.Abs(rate) < 2f && Mathf.Abs(rawRate) < 2f
                && Mathf.Abs(rollRate) < 5f && Mathf.Abs(bank) < 15f
                && (error <= 0f || pullGate > 0.9f) && aimRate < 3f
                && Mathf.Abs(actualStick - previousStick) < 0.03f;
            if (!stable) { _stableTime = 0f; return; }
            _stableTime += dt;
            if (_stableTime < 0.5f) return;

            float change = Mathf.Clamp(gain * error, -slew, slew) * dt;
            float total = baseStick + Value * appliedWeight;
            if ((total >= 0.98f && change > 0f) || (total <= -0.98f && change < 0f)) return;
            Value = Mathf.Clamp(Value + change, -limit, limit);
            Learning = true;
        }
    }
}
