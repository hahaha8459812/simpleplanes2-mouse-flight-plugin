using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Measured-rate PI feedback retains the equilibrium stick. No authority estimate is assumed.
    internal sealed class PitchRateController
    {
        // Feedback uses a finite reference in unlimited mode; it does not cap demand.
        private const float UnlimitedFeedbackScale = 30f;
        private float _integral, _stick, _lastRate;
        private bool _hasRate;
        private bool _releaseArmed;
        private float _releaseTime, _lastWanted, _settledTime;
        private float _lastActual, _fastEvidence;
        private bool _hasActual;
        public float Stick { get { return _stick; } }
        public float HeldStick { get { return _integral; } }
        public bool TrackingLimited { get; private set; }
        public bool Releasing { get; private set; }
        public float ReleaseRemaining { get { return _releaseTime; } }
        public bool FastResponse { get; private set; }
        public void Reset() { _integral = _stick = _lastRate = _releaseTime = _lastWanted = _settledTime = _lastActual = _fastEvidence = 0f; _hasRate = _hasActual = _releaseArmed = TrackingLimited = Releasing = FastResponse = false; }
        public void SuspendForPlayer()
        {
            bool protectedResponse = FastResponse;
            Reset();
            FastResponse = protectedResponse;
        }

        public float Update(float dt, float wantedRate, float measuredRate, float maxRate,
            float actualStick, float previousCommand, float balance,
            float trackingGain, float integralGain, float accelerationDamping, float releaseGain,
            bool responseProtection)
        {
            if (dt > 0.1f) Reset();
            if (maxRate <= 0f) maxRate = UnlimitedFeedbackScale;
            float acceleration = _hasRate ? (measuredRate - _lastRate) / dt : 0f;
            _lastRate = measuredRate; _hasRate = true;
            float actualChange = _hasActual ? actualStick - _lastActual : 0f;
            _lastActual = actualStick; _hasActual = true;
            // Conservative response detection, not an authority fit. Evidence requires
            // a read-back stick change and rapid motion changing in the same direction.
            if (responseProtection && Mathf.Abs(actualChange) > 0.005f
                && Mathf.Abs(acceleration) > 25f
                && Mathf.Abs(acceleration) > 800f * Mathf.Abs(actualChange)
                && actualChange * acceleration > 0f)
            {
                _fastEvidence += dt;
                if (_fastEvidence >= 0.03f) FastResponse = true;
            }
            if (responseProtection && FastResponse) { trackingGain *= 0.4f; accelerationDamping *= 0.2f; }
            float error = (wantedRate - measuredRate) / maxRate;
            float proportional = trackingGain * error;
            float damping = accelerationDamping * acceleration / maxRate;
            float total = proportional + _integral - damping + balance;
            float lag = previousCommand - actualStick;
            TrackingLimited = Mathf.Abs(lag) > 0.15f && error * lag > 0f;
            bool saturated = (total >= 1f && error > 0f) || (total <= -1f && error < 0f);
            // Arm on a substantial maneuver, then briefly release on its transition
            // to braking. Never turn faster integration into the steady-state law.
            if (_releaseArmed && (Mathf.Abs(wantedRate) < maxRate * 0.3f || wantedRate * _lastWanted < 0f))
            { _releaseTime = 3f; _settledTime = 0f; _releaseArmed = false; }
            else if (Mathf.Abs(wantedRate) > maxRate * 0.5f && _releaseTime <= 0f)
                _releaseArmed = true;
            _lastWanted = wantedRate;
            _releaseTime = Mathf.Max(0f, _releaseTime - dt);
            // End once the rate really settles, rather than after an unconditional
            // short window. Keep a finite timeout and stop once the old state is gone.
            _settledTime = Mathf.Abs(error) < 0.05f && Mathf.Abs(wantedRate) < maxRate * 0.15f
                ? _settledTime + dt : 0f;
            if (_settledTime >= 0.3f || Mathf.Abs(_integral) < 0.03f) _releaseTime = 0f;
            // Unwind a maneuver's retained stick when it opposes a substantial braking
            // demand. Keep ordinary integration around equilibrium; do not clear trim.
            Releasing = _releaseTime > 0f && error * _integral < 0f && Mathf.Abs(error) > 0.1f
                && (wantedRate * _integral < 0f || Mathf.Abs(wantedRate) < Mathf.Abs(measuredRate));
            if (!TrackingLimited && !saturated)
            {
                float change = integralGain * error * dt;
                if (Releasing)
                {
                    float release = Mathf.Max(integralGain, releaseGain) * error * dt;
                    // Faster release only removes the old state; opposite stick is
                    // subsequently learned with the normal integral gain.
                    change = Mathf.Clamp(release, -Mathf.Abs(_integral), Mathf.Abs(_integral));
                }
                _integral = Mathf.Clamp(_integral + change, -1f, 1f);
            }
            _stick = Mathf.Clamp(proportional + _integral - damping, -1f - balance, 1f - balance);
            return _stick;
        }

        internal static float ArrivalRate(float pitchError, float lateralError,
            float pitchRate, float yawRate, float radius, float leadSeconds, float maxRate)
        {
            // Prediction accounts for motion and input delay; fast turns may brake outside
            // the nominal radius. Within the radius there is no additional near-zero dead zone.
            float p = pitchError - pitchRate * leadSeconds;
            float y = lateralError - yawRate * leadSeconds;
            // Outside the arrival region, zero removes the constant-rate plateau.
            // Demand continues to grow with remaining pitch error; braking stays active.
            if (maxRate <= 0f) return UnlimitedFeedbackScale * p / Mathf.Max(radius, .0001f);
            float off = Mathf.Sqrt(p * p + y * y);
            if (off < 1e-5f) return 0f;
            return maxRate * p / off * Mathf.Clamp01(off / radius);
        }
    }
}
