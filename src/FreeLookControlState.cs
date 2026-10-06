namespace SimplePlanes2MouseFlight
{
    internal sealed class FreeLookControlState
    {
        public bool PlayerOnly { get; private set; }
        public bool Observe(bool held, bool targetOutside, bool playerManeuver)
        {
            if (!held || PlayerOnly || !targetOutside || !playerManeuver) return false;
            PlayerOnly = true;
            return true;
        }
        public bool Release()
        {
            bool wasPlayerOnly = PlayerOnly;
            PlayerOnly = false;
            return wasPlayerOnly;
        }
    }
}
