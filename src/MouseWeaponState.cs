namespace SimplePlanes2MouseFlight
{
    // A held drag/UI click must be released before it can become a weapon request.
    internal sealed class MouseWeaponState
    {
        private bool _gunsReady;
        private bool _weaponsReady;
        public bool Guns { get; private set; }
        public bool Weapons { get; private set; }

        public void Update(bool enabled, bool left, bool right)
        {
            if (!enabled) { Reset(); return; }
            if (!left) _gunsReady = true;
            if (!right) _weaponsReady = true;
            Guns = _gunsReady && left;
            Weapons = _weaponsReady && right;
        }

        public void Reset()
        {
            _gunsReady = _weaponsReady = Guns = Weapons = false;
        }
    }
}
