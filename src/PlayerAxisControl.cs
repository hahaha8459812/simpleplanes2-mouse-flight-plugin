using System;

namespace SimplePlanes2MouseFlight
{
    // Game-mapped player input takes priority. Only the return to automation is ramped.
    internal sealed class PlayerAxisControl
    {
        private bool _returning;
        public float PlayerValue { get; private set; }
        public float Output { get; private set; }
        public bool Manual { get; private set; }
        public int Engagement { get; private set; }

        public void Reset()
        {
            PlayerValue = Output = 0f; Manual = _returning = false; Engagement = 0;
        }

        public void Sample(float player, bool playerOnly)
        {
            PlayerValue = player;
            // Small numerical tolerance, not an extra joystick dead zone.
            bool manual = playerOnly || Math.Abs(player) > 0.0001f;
            if (manual && !Manual) Engagement++;
            if (!manual && Manual) _returning = true;
            Manual = manual;
            if (manual) { Output = player; _returning = false; }
        }

        public float ApplyAutomatic(float automatic, float dt, float slew)
        {
            if (Manual) return Output;
            if (_returning)
            {
                float step = Math.Max(0f, Math.Min(dt, 0.1f)) * Math.Max(0f, slew);
                float difference = automatic - Output;
                Output += Math.Sign(difference) * Math.Min(Math.Abs(difference), step);
                if (Math.Abs(automatic - Output) < 0.0001f) _returning = false;
            }
            else Output = automatic;
            return Output;
        }
    }
}
