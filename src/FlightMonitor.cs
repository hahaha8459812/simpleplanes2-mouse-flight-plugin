using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal sealed class FlightMonitor
    {
        public bool Valid { get; private set; }
        public float Roll, Pitch, Yaw, Throttle, Brake;
        public float Speed, Altitude, Attack, Slip, RollRate, PitchRate, YawRate, Heading;
        public float PitchError, YawError, FuelFraction, FuelCapacity;
        public bool AimValid;
        public bool? LandingGearDown, ParkingBrake;
        public readonly bool?[] ActivationGroups = new bool?[8];
        private object _aircraft;
        private float _nextSample;
        public void Reset()
        {
            Valid = AimValid = false; _aircraft = null; _nextSample = 0;
            LandingGearDown = ParkingBrake = null;
            FuelFraction = FuelCapacity = float.NaN;
            for (int i=0;i<ActivationGroups.Length;i++) ActivationGroups[i]=null;
        }
        public void Update(bool available, bool active, Vector3 aim)
        {
            if (!available) { Reset(); return; }
            float now=Time.realtimeSinceStartup;
            if (now<_nextSample) return;
            object aircraft,controls; string error,source; Vector3 position; Quaternion rotation;
            if (!GameApi.TryGetLocalAircraft(out aircraft,out error)
                || !GameApi.TryGetAircraftControls(aircraft,out controls,out error)
                || !GameApi.TryGetAircraftPose(aircraft,out position,out rotation,out source,out error)
                || !FlightRecorder.Finite(rotation)) { Valid=false; return; }
            if (!ReferenceEquals(aircraft,_aircraft)) { Reset(); _aircraft=aircraft; }
            _nextSample=now+.05f;
            Roll=Read(controls,GameApi.AircraftControlsType,"Roll");
            Pitch=Read(controls,GameApi.AircraftControlsType,"Pitch");
            Yaw=Read(controls,GameApi.AircraftControlsType,"Yaw");
            Throttle=Read(controls,GameApi.AircraftControlsType,"Throttle");
            Brake=Read(controls,GameApi.AircraftControlsType,"Brake");
            LandingGearDown=ReadBoolean(controls,"LandingGearDown");
            ParkingBrake=ReadBoolean(controls,"ParkingBrake");
            for(int i=0;i<ActivationGroups.Length;i++) ActivationGroups[i]=ReadBoolean(controls,"Activate"+(i+1));
            FuelFraction=Read(aircraft,GameApi.AircraftScriptType,"FuelProportion");
            FuelCapacity=Read(aircraft,GameApi.AircraftScriptType,"InitialFuelCapacity");
            Speed=Read(aircraft,GameApi.AircraftScriptType,"AirSpeed");
            Altitude=Read(aircraft,GameApi.AircraftScriptType,"Altitude");
            Attack=Read(aircraft,GameApi.AircraftScriptType,"AngleOfAttack");
            Slip=Read(aircraft,GameApi.AircraftScriptType,"AngleOfSlip");
            RollRate=Read(aircraft,GameApi.AircraftScriptType,"RollRate");
            PitchRate=Read(aircraft,GameApi.AircraftScriptType,"PitchRate");
            YawRate=Read(aircraft,GameApi.AircraftScriptType,"YawRate");
            Vector3 nose=rotation*Vector3.forward;
            Heading=Mathf.Repeat(Mathf.Atan2(nose.x,nose.z)*Mathf.Rad2Deg,360);
            Vector3 local=Quaternion.Inverse(rotation)*aim;
            AimValid=active&&FlightRecorder.Finite(local)&&local.sqrMagnitude>.000001f;
            PitchError=Mathf.Atan2(local.y,local.z)*Mathf.Rad2Deg;
            YawError=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            Valid=true;
        }
        private static float Read(object instance,string type,string member)
        { float value; return FlightRecorder.ReadFloat(instance,type,member,out value)?value:float.NaN; }
        private static bool? ReadBoolean(object controls,string member)
        {
            object value; string error;
            return Reflect.TryGetMember(controls,GameApi.AircraftControlsType,member,out value,out error)&&value is bool ? (bool?)value : null;
        }
        public void Dispose() { Reset(); }
    }
}