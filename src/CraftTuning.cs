using System;
using System.Collections;
using System.Collections.Generic;

namespace SimplePlanes2MouseFlight
{
    internal sealed class CraftTuning
    {
        private readonly PluginSettings _settings;
        private readonly PluginLog _log;
        private object _aircraft, _data;
        public bool Ready { get; private set; }
        public bool Applied { get; private set; }
        public string CraftName { get; private set; }
        public string Status { get; private set; }
        public bool HasCraftParameters { get; private set; }
        private Dictionary<string, float> _baseline, _source, _applied;

        public CraftTuning(PluginSettings settings, PluginLog log)
        { _settings = settings; _log = log; Status = "等待飞行场景中的飞机"; }
        public Dictionary<string, float> EditValues()
        { return FlightTuning.Copy(Applied ? _applied : _source); }
        public Dictionary<string, float> SourceValues() { return FlightTuning.Copy(_source); }
        public Dictionary<string, float> BaselineValues() { return FlightTuning.Copy(_baseline); }
        public bool Apply(IDictionary<string, float> values, out string error)
        {
            if (!Ready) { error = "当前飞机参数尚未就绪"; return false; }
            if (!FlightTuning.Validate(values, out error)) return false;
            _settings.SetFlightTuning(values); _applied = FlightTuning.Copy(values); Applied = true;
            _log.Info("面板会话参数已应用：" + _settings.DescribeEffectiveConfig());
            return true;
        }
        // Return true on identity/scene changes so callers release ownership before resuming.
        public bool Update()
        {
            bool inFlight, primary; int load; string detail, error; object aircraft, data;
            bool valid = _settings.Enabled && GameApi.TryGetFlightSceneActive(out inFlight, out detail) && inFlight
                && GameApi.TryGetLocalAircraft(out aircraft, out error) && GameApi.IsAlive(aircraft);
            if (!valid) return Clear();
            // Short-circuit assignment cannot prove initialization to older C# compilers.
            if (!GameApi.TryGetLocalAircraft(out aircraft, out error)
                || !GameApi.TryGetIsPrimaryLocalPlayer(aircraft, out primary, out error) || !primary
                || !GameApi.TryGetLoadContext(aircraft, out load, out error) || load != GameApi.CraftLoadContextFlight
                || !Reflect.TryGetMember(aircraft, GameApi.AircraftScriptType, "Aircraft", out data, out error) || data == null)
                return Clear();
            bool changed = !ReferenceEquals(aircraft, _aircraft) || !ReferenceEquals(data, _data);
            if (changed)
            {
                Clear(); _aircraft = aircraft; _data = data;
                object name;
                CraftName = Reflect.TryGetMember(data, "Assets.Scripts.Craft.AircraftData", "Name", out name, out error)
                    && name is string ? (string)name : "当前飞机";
                Status = "等待零件加载";
            }
            if (Ready) return changed;
            object parts, initialized;
            if (!Reflect.TryGetMember(aircraft, GameApi.AircraftScriptType, "_initialized", out initialized, out error)
                || !(initialized is bool) || !(bool)initialized
                || !Reflect.TryGetMember(aircraft, GameApi.AircraftScriptType, "Parts", out parts, out error)
                || !(parts is IList) || ((IList)parts).Count == 0) return changed;
            List<string> names = new List<string>();
            foreach (object part in (IEnumerable)parts)
            {
                object name; string payload;
                if (!Reflect.TryGetMember(part, "Assets.Scripts.Craft.Parts.PartData", "Name", out name, out error))
                { Status = "零件名称读取失败，尚未接入作品参数"; return changed; }
                if (name is string && FlightTuning.HasPrefix((string)name, out payload)) names.Add((string)name);
            }
            _baseline = _settings.ReadConfiguredTuning();
            if (!FlightTuning.Validate(_baseline, out error))
            { Status = "插件配置无效：" + error; return changed; }
            _source = FlightTuning.Copy(_baseline);
            if (names.Count > 1)
            {
                Status = "发现多个 MF 零件，全部作品参数拒绝加载；使用插件配置";
                _log.Warn(Status);
            }
            else if (names.Count == 1)
            {
                Dictionary<string, float> parsed; string warning;
                if (FlightTuning.Parse(names[0], _baseline, out parsed, out error, out warning))
                {
                    _source = parsed; HasCraftParameters = true;
                    Status = "已加载飞机专属参数";
                    if (warning != null) { _log.Warn(warning); Status += "（有未知键被忽略）"; }
                }
                else { Status = "作品参数未加载：" + error + "；使用插件配置"; _log.Warn(Status); }
            }
            else Status = "飞机无 MF 参数，使用插件配置";
            _settings.SetFlightTuning(HasCraftParameters ? _source : null);
            Ready = true; _log.Info("作品参数检查：" + CraftName + "；" + Status);
            return changed;
        }
        public bool Clear()
        {
            bool changed = _aircraft != null || Ready || Applied;
            _aircraft = _data = null; _baseline = _source = _applied = null;
            Ready = Applied = HasCraftParameters = false;
            _settings.SetFlightTuning(null); CraftName = null; Status = "等待飞行场景中的飞机";
            return changed;
        }
    }
}
