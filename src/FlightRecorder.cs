using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Read-only observations after physics, buffered separately from the main log.
    internal sealed class FlightRecorder
    {
        private readonly PluginSettings _settings;
        private readonly PluginLog _log;
        private KeyCode _key, _markerKey;
        private bool _parsed, _recording;
        private float _nextSampleTime, _nextScan, _nextFlush;
        private int _count, _segment, _generation;
        private int _lastControlState = -1;
        private object _aircraft;
        private StreamWriter _samples, _surfaces, _events;
        private string _path;
        private readonly List<Surface> _surfaceList = new List<Surface>();
        private readonly PhysicsSampleHistory _history = new PhysicsSampleHistory();
        private sealed class Surface { public object Script, Data; public string Id; }
        public FlightRecorder(PluginSettings settings, PluginLog log) { _settings = settings; _log = log; }
        public bool IsRecording { get { return _recording; } }
        public string Describe() { return _recording ? "记录中：第 " + _segment + " 段 / " + _count + " 点" : "未记录"; }
        public void RecordTuningChanged(string parameters)
        {
            if (!_recording) return;
            try { Event("tuning_applied", parameters); }
            catch (Exception ex) { _log.Warn("[REC] 参数变化记录失败：" + ex.Message); Stop("参数事件写入失败"); }
        }
        public void DrawStatus()
        {
            if (_recording) GUI.Box(new Rect(12f, Screen.height - 76f, 430f, 60f), Describe()
                + "\n" + _settings.RecordMarkerKey + " 标记下一段；" + _settings.RecordKey + " 结束记录");
        }
        public void Update()
        {
            if (!_parsed) { _parsed = true; _key = KeyUtil.Parse(_settings.RecordKey, KeyCode.Slash); _markerKey = KeyUtil.Parse(_settings.RecordMarkerKey, KeyCode.Backslash); }
            if (_key != KeyCode.None && Input.GetKeyDown(_key)) { if (_recording) Stop("手动停止"); else Start(); }
            if (!_recording) return;
            if (_markerKey != KeyCode.None && _markerKey != _key && Input.GetKeyDown(_markerKey))
            {
                _segment++;
                try { Event("marker", "第 " + _segment + " 段"); }
                catch (Exception ex) { Stop("写入测试标记失败：" + ex.Message); return; }
                _log.Info("[REC] 测试标记：第 " + _segment + " 段。");
            }
            bool inFlight, paused; string detail, error;
            if (!GameApi.TryGetFlightSceneActive(out inFlight, out detail) || !inFlight) Stop("离开飞行场景");
            else if (!GameApi.TryGetPaused(out paused, out error) || paused) Stop("暂停或暂停状态不可读");
        }
        private void Start()
        {
            bool inFlight, paused, primary; string detail, error; object aircraft;
            if (!GameApi.TryGetFlightSceneActive(out inFlight, out detail) || !inFlight
                || !GameApi.TryGetPaused(out paused, out error) || paused
                || !GameApi.TryGetLocalAircraft(out aircraft, out error)
                || !GameApi.TryGetIsPrimaryLocalPlayer(aircraft, out primary, out error) || !primary)
            { _log.Warn("[REC] 记录未开始：请在未暂停的本机飞行场景中操作。"); return; }
            try
            {
                string directory = Path.Combine(BepInEx.Paths.PluginPath, "SimplePlanes2MouseFlight", "Records");
                Directory.CreateDirectory(directory);
                string stem = "FlightRecord-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
                _path = Path.Combine(directory, stem + ".csv");
                _samples = Open(_path); _surfaces = Open(Path.Combine(directory, stem + "-surfaces.csv")); _events = Open(Path.Combine(directory, stem + "-events.csv"));
                File.WriteAllText(Path.Combine(directory, stem + "-metadata.txt"), "Plugin=" + PluginInfo.Version
                    + "\nConfig=" + _settings.DescribeEffectiveConfig() + "\nSampling=WaitForFixedUpdate; observation after physics, no injected control"
                    + "\nRequestedInterval=" + F(_settings.RecordIntervalSeconds)
                    + "\nPitchRate=nose-up positive, deg/s; acceleration=world omega difference projected on current body right"
                    + "\ncmd*=most recent instructor Update selected output, may precede the current physics step"
                    + "\nplayer*=most recent game-mapped player axis read; manual*=axis ownership at observation; playerOnly=free-look latch"
                    + "\nSpeed=world velocity magnitude, not wind-corrected airspeed; empty cells=unavailable"
                    + "\nSurface IDs stable only within surfaceGeneration; angles are not averaged aircraft authority\n", new UTF8Encoding(false));
                _samples.WriteLine("n,segment,physicsTime,realtime,dt,renderFrame,active,cameraActive,freeLook,actualPitch,actualRoll,actualYaw,trim,cmdPitch,cmdRoll,cmdYaw,speed,bank,pitchAngle,pitchError,yawError,pitchRate,rollRate,yawRate,angularAcceleration,accelerationValid,wx,wy,wz,px,py,pz,qx,qy,qz,qw,aimx,aimy,aimz,surfaceGeneration,surfaceCount,cmdUpdatedTime,cmdRenderFrame,balanceStick,balanceApplied,balanceLearning,balanceResponseSeen,rollBlend,yawAimGain,pullGate,pitchDemand,pitchRateTarget,pitchRateStick,pitchTrackingLimited,pitchHeldStick,pitchIntegralReleasing,pitchRateDemand,rollRateTarget,rollCaptured,pitchReleaseRemaining,pitchFastResponse,playerPitch,playerRoll,playerYaw,manualPitch,manualRoll,manualYaw,playerOnly");
                _surfaces.WriteLine("n,segment,physicsTime,surfaceGeneration,id,inputId,angle,maxDeflection,damaged,invert,autoInvert,trimType,readValid");
                _events.WriteLine("realtime,physicsTime,segment,event,detail");
                _aircraft = aircraft; _nextSampleTime = _nextScan = _nextFlush = 0f; _count = 0; _segment = 1; _generation = 0;
                _history.Reset(); _surfaceList.Clear(); _lastControlState = -1; _recording = true; Event("start", "第 1 段");
                _log.Info("[REC] 物理采样开始：第 1 段；" + _settings.RecordMarkerKey + " 标记下一段，" + _settings.RecordKey + " 结束。文件：" + _path);
            }
            catch (Exception ex) { _log.Warn("[REC] 创建记录失败：" + ex.Message); Stop("创建记录失败"); }
        }
        public void SamplePhysics(Instructor instructor, bool cameraActive, Vector3 aim, bool freeLook)
        {
            if (!_recording || Time.fixedTime + Time.fixedDeltaTime * 0.25f < _nextSampleTime) return;
            try
            {
                object aircraft, controls; string error, source; Vector3 position, velocity, omega; Quaternion rotation; bool paused;
                if (!GameApi.TryGetPaused(out paused, out error) || paused) { Stop("暂停"); return; }
                if (!GameApi.TryGetLocalAircraft(out aircraft, out error) || !ReferenceEquals(aircraft, _aircraft)) { Stop("当前载具变化或消失"); return; }
                if (!GameApi.TryGetAircraftControls(aircraft, out controls, out error)
                    || !GameApi.TryGetAircraftPose(aircraft, out position, out rotation, out source, out error)
                    || !TryMotion(aircraft, out velocity, out omega) || !Finite(position) || !Finite(rotation))
                { Stop("载具状态无效：" + error); return; }
                float pitch, roll, yaw, trim;
                if (!ReadFloat(controls, GameApi.AircraftControlsType, "Pitch", out pitch)
                    || !ReadFloat(controls, GameApi.AircraftControlsType, "Roll", out roll)
                    || !ReadFloat(controls, GameApi.AircraftControlsType, "Yaw", out yaw)) { Stop("实际操纵通道不可读"); return; }
                bool trimValid = ReadFloat(controls, GameApi.AircraftControlsType, "Trim", out trim);
                Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
                float dt, acceleration; bool accelerationValid;
                if (!_history.Observe(Time.fixedTime, omega, right, out dt, out acceleration, out accelerationValid)) return;
                _nextSampleTime = Time.fixedTime + _settings.RecordIntervalSeconds;
                if (Time.fixedTime >= _nextScan) { Scan(); _nextScan = Time.fixedTime + 1f; }
                bool active = instructor != null && instructor.IsBoundTo(controls);
                Vector3 local = Quaternion.Inverse(rotation) * aim; _count++;
                StringBuilder row = new StringBuilder(512);
                Add(row, _count); Add(row, _segment); Add(row, Time.fixedTime); Add(row, Time.realtimeSinceStartup); Add(row, dt); Add(row, Time.frameCount);
                Add(row, active); Add(row, cameraActive); Add(row, freeLook); Add(row, pitch); Add(row, roll); Add(row, yaw); Add(row, trimValid ? F(trim) : "");
                Add(row, active ? F(instructor.PitchCommand) : ""); Add(row, active ? F(instructor.RollCommand) : ""); Add(row, active ? F(instructor.YawCommand) : "");
                Add(row, velocity.magnitude); Add(row, -Mathf.Atan2(right.y, up.y) * Mathf.Rad2Deg); Add(row, Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg);
                Add(row, cameraActive && Finite(aim) ? F(Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg) : ""); Add(row, cameraActive && Finite(aim) ? F(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) : "");
                Add(row, -Vector3.Dot(omega, right) * Mathf.Rad2Deg); Add(row, -Vector3.Dot(omega, forward) * Mathf.Rad2Deg); Add(row, Vector3.Dot(omega, up) * Mathf.Rad2Deg);
                Add(row, accelerationValid ? F(acceleration) : ""); Add(row, accelerationValid); Add(row, omega.x); Add(row, omega.y); Add(row, omega.z);
                Add(row, position.x); Add(row, position.y); Add(row, position.z); Add(row, rotation.x); Add(row, rotation.y); Add(row, rotation.z); Add(row, rotation.w);
                Add(row, cameraActive ? F(aim.x) : ""); Add(row, cameraActive ? F(aim.y) : ""); Add(row, cameraActive ? F(aim.z) : ""); Add(row, _generation); Add(row, _surfaceList.Count);
                Add(row, active ? F(instructor.CommandTime) : ""); Add(row, active ? instructor.CommandFrame.ToString(CultureInfo.InvariantCulture) : "");
                Add(row, active ? F(instructor.BalanceStick) : ""); Add(row, active ? F(instructor.BalanceApplied) : "");
                Add(row, active ? (instructor.BalanceLearning ? "1" : "0") : "");
                Add(row, active ? (instructor.BalanceResponseSeen ? "1" : "0") : "");
                Add(row, active ? F(instructor.RollBlend) : ""); Add(row, active ? F(instructor.YawAimGain) : "");
                Add(row, active ? F(instructor.PullGate) : ""); Add(row, active ? F(instructor.PitchDemand) : "");
                Add(row, active ? F(instructor.PitchRateTarget) : ""); Add(row, active ? F(instructor.PitchRateStick) : "");
                Add(row, active ? (instructor.PitchTrackingLimited ? "1" : "0") : "");
                Add(row, active ? F(instructor.PitchHeldStick) : "");
                Add(row, active ? (instructor.PitchIntegralReleasing ? "1" : "0") : "");
                Add(row, active ? F(instructor.PitchRateDemand) : "");
                Add(row, active ? F(instructor.RollRateTarget) : "");
                Add(row, active ? (instructor.RollCaptured ? "1" : "0") : "");
                Add(row, active ? F(instructor.PitchReleaseRemaining) : "");
                Add(row, active ? (instructor.PitchFastResponse ? "1" : "0") : "");
                Add(row, active ? F(instructor.PlayerPitch) : "");
                Add(row, active ? F(instructor.PlayerRoll) : "");
                Add(row, active ? F(instructor.PlayerYaw) : "");
                Add(row, active ? (instructor.PlayerPitchActive ? "1" : "0") : "");
                Add(row, active ? (instructor.PlayerRollActive ? "1" : "0") : "");
                Add(row, active ? (instructor.PlayerYawActive ? "1" : "0") : "");
                Add(row, active ? (instructor.PlayerOnly ? "1" : "0") : "");
                int ownership = !active ? 0 : 1 | (instructor.PlayerPitchActive ? 2 : 0)
                    | (instructor.PlayerRollActive ? 4 : 0) | (instructor.PlayerYawActive ? 8 : 0)
                    | (instructor.PlayerOnly ? 16 : 0);
                if (ownership != _lastControlState)
                {
                    _lastControlState = ownership;
                    Event("control_ownership", "active=" + active + ";pitch=" + (active && instructor.PlayerPitchActive)
                        + ";roll=" + (active && instructor.PlayerRollActive) + ";yaw=" + (active && instructor.PlayerYawActive)
                        + ";playerOnly=" + (active && instructor.PlayerOnly));
                }
                _samples.WriteLine(row.ToString()); WriteSurfaces();
                if (Time.realtimeSinceStartup >= _nextFlush) { _samples.Flush(); _surfaces.Flush(); _events.Flush(); _nextFlush = Time.realtimeSinceStartup + 1f; }
            }
            catch (Exception ex) { Stop("采样失败：" + ex.GetType().Name + " " + ex.Message); }
        }
        private void Scan()
        {
            _surfaceList.Clear(); _generation++; object wings; string error;
            if (!Reflect.TryGetMember(_aircraft, GameApi.AircraftScriptType, "Wings", out wings, out error) || !(wings is IEnumerable)) { Event("surfaces_unavailable", error); return; }
            int wingIndex = 0, skipped = 0;
            foreach (object wing in (IEnumerable)wings)
            {
                object surfaces;
                if (Reflect.TryGetMember(wing, "Assets.Scripts.Craft.Parts.Modifiers.WingScript", "ControlSurfaces", out surfaces, out error) && surfaces is IEnumerable)
                {
                    int surfaceIndex = 0;
                    foreach (object script in (IEnumerable)surfaces)
                    {
                        object data;
                        if (Reflect.TryGetMember(script, "Assets.Scripts.Craft.Parts.Modifiers.ControlSurfaceScript", "ControlSurface", out data, out error) && data != null)
                            _surfaceList.Add(new Surface { Script = script, Data = data, Id = "w" + wingIndex + "s" + surfaceIndex });
                        else skipped++;
                        surfaceIndex++;
                    }
                }
                else skipped++;
                wingIndex++;
            }
            if (_generation == 1 || skipped > 0) Event("surface_scan", "count=" + _surfaceList.Count + " skipped=" + skipped);
        }
        private void WriteSurfaces()
        {
            const string st = "Assets.Scripts.Craft.Parts.Modifiers.ControlSurfaceScript", dt = "Assets.Scripts.Craft.Parts.Modifiers.ControlSurfaceData";
            foreach (Surface s in _surfaceList)
            {
                float angle, maximum;
                bool valid = ReadFloat(s.Script, st, "Angle", out angle) & ReadFloat(s.Data, dt, "MaxDeflectionDegree", out maximum);
                _surfaces.WriteLine(_count + "," + _segment + "," + F(Time.fixedTime) + "," + _generation + "," + s.Id
                    + "," + Csv(Text(s.Data, dt, "InputId")) + "," + (valid ? F(angle) : "") + "," + (valid ? F(maximum) : "")
                    + "," + Csv(Text(s.Script, st, "Damaged")) + "," + Csv(Text(s.Data, dt, "Invert"))
                    + "," + Csv(Text(s.Data, dt, "AutoInvert")) + "," + Csv(Text(s.Data, dt, "Trim")) + "," + (valid ? "1" : "0"));
            }
        }
        public void Stop(string reason)
        {
            bool wasRecording = _recording;
            try { if (_events != null) Event("stop", reason); } catch (Exception) { }
            _recording = false; Close(ref _samples); Close(ref _surfaces); Close(ref _events);
            _history.Reset(); _surfaceList.Clear(); _aircraft = null;
            if (wasRecording) _log.Info("[REC] 记录结束：" + reason + "；" + _count + " 点；文件：" + _path);
        }
        private void Event(string kind, string detail) { _events.WriteLine(F(Time.realtimeSinceStartup) + "," + F(Time.fixedTime) + "," + _segment + "," + Csv(kind) + "," + Csv(detail)); _events.Flush(); }
        private static StreamWriter Open(string path) { return new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false), 65536); }
        private static void Close(ref StreamWriter writer) { try { if (writer != null) writer.Dispose(); } catch (Exception) { } writer = null; }
        private static string F(float v) { return v.ToString("F6", CultureInfo.InvariantCulture); }
        private static void Add(StringBuilder r, object v) { if (r.Length > 0) r.Append(','); r.Append(v is float ? F((float)v) : v is bool ? ((bool)v ? "1" : "0") : v); }
        private static string Csv(string v) { return "\"" + (v ?? "").Replace("\"", "\"\"") + "\""; }
        internal static bool ReadFloat(object instance, string type, string member, out float value)
        {
            object raw; string error; value = 0f;
            if (!Reflect.TryGetMember(instance, type, member, out raw, out error) || !(raw is float || raw is int)) return false;
            value = Convert.ToSingle(raw, CultureInfo.InvariantCulture); return Finite(value);
        }
        private static string Text(object instance, string type, string member)
        { object raw; string error; return Reflect.TryGetMember(instance, type, member, out raw, out error) && raw != null ? Convert.ToString(raw, CultureInfo.InvariantCulture) : ""; }
        internal static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        internal static bool Finite(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
        internal static bool Finite(Quaternion q) { return Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w); }
        private static bool TryMotion(object aircraft, out Vector3 velocity, out Vector3 omega)
        {
            velocity = omega = Vector3.zero; object raw; string error;
            if (!Reflect.TryGetMember(aircraft, GameApi.AircraftScriptType, "Velocity", out raw, out error) || !(raw is Vector3)) return false;
            velocity = (Vector3)raw;
            if (!Reflect.TryGetMember(aircraft, GameApi.AircraftScriptType, "AngularVelocity", out raw, out error) || !(raw is Vector3)) return false;
            omega = (Vector3)raw; return Finite(velocity) && Finite(omega);
        }
    }
}
