using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SimplePlanes2MouseFlight
{
    // One schema for craft input, session editing and export. Values never write ConfigEntry.
    internal sealed class TuningField
    {
        public readonly string Name, ShortName, Label, Group;
        public readonly float Min, Max, Step;
        public readonly bool Toggle, Sign;
        public TuningField(string name, string shortName, string label, string group,
            float min, float max, float step, bool toggle, bool sign)
        { Name = name; ShortName = shortName; Label = label; Group = group;
          Min = min; Max = max; Step = step; Toggle = toggle; Sign = sign; }
    }

    internal static class FlightTuning
    {
        private static TuningField F(string n, string s, string l, string g, float min, float max, float step)
        { return new TuningField(n, s, l, g, min, max, step, false, false); }
        public static readonly TuningField[] Fields = {
            F("MaxPitchRate", "MPR", "俯仰转速上限 (°/s, 0=关闭)", "俯仰", 0, 150, 1),
            F("PitchArrivalRadius", "PAR", "俯仰收尾半径 (°)", "俯仰", .5f, 5, .1f),
            F("PitchBrakingLead", "PBL", "俯仰制动提前量 (s)", "俯仰", 0, .4f, .01f),
            F("PitchDemandAcceleration", "PDA", "俯仰目标转速变化上限 (°/s²)", "俯仰", 0, 1000, 5),
            F("PitchRateTrackingGain", "PTG", "俯仰转速跟踪增益", "俯仰", .1f, 2, .01f),
            F("PitchRateIntegralGain", "PIG", "俯仰积分增益", "俯仰", 0, 1, .01f),
            F("PitchAccelerationDamping", "PAD", "俯仰加速度阻尼", "俯仰", 0, .2f, .001f),
            F("PitchIntegralReleaseGain", "PIR", "俯仰残余积分释放增益", "俯仰", 0, 5, .05f),
            new TuningField("PitchFastResponseProtection", "PFP", "快速响应保护", "俯仰", 0, 1, 1, true, false),
            F("PitchBalanceGain", "PBG", "俯仰平衡补偿学习增益", "俯仰", 0, .1f, .001f),
            F("PitchBalanceLimit", "PBC", "俯仰平衡补偿上限", "俯仰", 0, .4f, .01f),
            F("PitchBalanceSlew", "PBS", "俯仰平衡补偿变化上限", "俯仰", .001f, .1f, .001f),
            F("WingsLevelGain", "WLG", "机翼回平力度", "滚转", .5f, 3, .01f),
            F("MaxLevelRollRate", "MLR", "回平转速上限 (°/s, 0=关闭)", "滚转", 0, 200, 1),
            F("MaxRollRate", "MRR", "朝目标滚转上限 (°/s, 0=关闭)", "滚转", 0, 400, 1),
            F("KdRoll", "KDR", "滚转阻尼", "滚转", 0, .3f, .01f),
            F("RollDemandAcceleration", "RDA", "滚转目标转速变化上限 (°/s²)", "滚转", 0, 2000, 5),
            F("BankBlendStart", "BBS", "回平/朝目标过渡起点 (°)", "滚转", .2f, 90, .1f),
            F("BankBlendEnd", "BBE", "回平/朝目标过渡终点 (°)", "滚转", .5f, 120, .1f),
            F("VerticalFadeStart", "VFS", "竖直目标渐隐起点 (°)", "滚转", 2, 45, 1),
            F("VerticalFadeEnd", "VFE", "竖直目标渐隐终点 (°)", "滚转", 5, 90, 1),
            F("BlendSmoothing", "BS", "滚转模式平滑时间 (s)", "滚转", 0, .5f, .01f),
            F("RollCaptureAngle", "RCA", "回平进入角 (°)", "滚转", .2f, 2, .1f),
            F("RollCaptureExitAngle", "RCE", "回平退出角 (°)", "滚转", .7f, 10, .1f),
            F("RollCaptureRate", "RCR", "回平允许转速 (°/s)", "滚转", 1, 20, .5f),
            F("RollCaptureDwell", "RCD", "回平确认时间 (s)", "滚转", 0, .5f, .01f),
            F("MaxYawRate", "MYR", "偏航转速上限 (°/s, 0=关闭)", "偏航", 0, 60, 1),
            F("KdYaw", "KDY", "偏航阻尼", "偏航", 0, .5f, .01f),
            F("YawInTurnShare", "YTS", "大转弯方向舵保留份额", "偏航", 0, 1, .01f),
            F("YawCoordStrength", "YCS", "机体系速度横向角变化协调增益", "偏航", 0, 1.5f, .01f),
            F("YawAimGain", "YAG", "偏航瞄准增益", "偏航", .5f, 2, .01f),
            F("YawFineGain", "YFG", "偏航精细瞄准增益", "偏航", .5f, 2.5f, .01f),
            F("YawBrakingLead", "YBL", "偏航制动提前量 (s)", "偏航", 0, .4f, .01f),
            F("YawDemandGain", "YDG", "偏航杆量增益", "偏航", .2f, 1.5f, .01f),
            F("AttitudeResponse", "AR", "滚转/偏航响应时间 (s)", "通用", .05f, 2, .01f),
            F("RateSmoothing", "RS", "转速平滑时间 (s)", "通用", 0, .3f, .01f),
            F("OutputSlew", "OS", "三轴杆量变化上限 (/s)", "通用", .5f, 30, .1f),
            new TuningField("RollSign", "RSG", "滚转方向", "通用", -1, 1, 2, false, true),
            new TuningField("PitchSign", "PSG", "俯仰方向", "通用", -1, 1, 2, false, true),
            new TuningField("YawSign", "YSG", "偏航方向", "通用", -1, 1, 2, false, true)
        };

        public static Dictionary<string, float> Copy(IDictionary<string, float> values)
        { return new Dictionary<string, float>(values, StringComparer.Ordinal); }
        public static Dictionary<string, float> Read(PluginSettings settings)
        {
            Dictionary<string, float> values = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (TuningField f in Fields)
            {
                PropertyInfo p = typeof(PluginSettings).GetProperty(f.Name);
                object v = p.GetValue(settings, null);
                values.Add(f.Name, v is bool ? ((bool)v ? 1f : 0f) : Convert.ToSingle(v, CultureInfo.InvariantCulture));
            }
            return values;
        }
        public static TuningField Find(string key)
        {
            foreach (TuningField f in Fields)
                if (string.Equals(key, f.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, f.ShortName, StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }
        public static bool Validate(IDictionary<string, float> values, out string error)
        {
            foreach (TuningField f in Fields)
            {
                float v;
                if (!values.TryGetValue(f.Name, out v) || float.IsNaN(v) || float.IsInfinity(v)
                    || v < f.Min || v > f.Max || (f.Toggle && v != 0 && v != 1) || (f.Sign && v != -1 && v != 1))
                { error = f.Label + "：取值无效，允许 " + f.Min + " 至 " + f.Max; return false; }
            }
            if (values["BankBlendEnd"] < values["BankBlendStart"] + 1f)
            { error = "滚转过渡终点须至少比起点大 1°"; return false; }
            if (values["VerticalFadeEnd"] < values["VerticalFadeStart"] + 1f)
            { error = "竖直渐隐终点须至少比起点大 1°"; return false; }
            if (values["RollCaptureExitAngle"] < values["RollCaptureAngle"] + .5f)
            { error = "回平退出角须至少比进入角大 0.5°"; return false; }
            error = null; return true;
        }
        public static bool HasPrefix(string name, out string payload)
        {
            payload = null;
            if (name == null) return false;
            for (int i = 0; i + 2 <= name.Length; i++)
            {
                if (i > 0 && !char.IsWhiteSpace(name[i - 1])) continue;
                if (name.Substring(i, 2) != "MF") continue;
                int end = i + 2;
                if (end != name.Length && name[end] != ';') continue;
                payload = name.Substring(end); return true;
            }
            return false;
        }
        public static bool Parse(string name, IDictionary<string, float> baseline,
            out Dictionary<string, float> result, out string error, out string warning)
        {
            result = null; error = warning = null;
            string payload;
            if (!HasPrefix(name, out payload)) { error = "没有 MF 参数前缀"; return false; }
            Dictionary<string, float> merged = Copy(baseline);
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> unknown = new List<string>();
            foreach (string raw in payload.Split(';'))
            {
                string item = raw.Trim();
                if (item.Length == 0) continue;
                int equals = item.IndexOf('=');
                if (equals <= 0 || equals != item.LastIndexOf('=')) { error = "参数格式错误：" + item; return false; }
                TuningField f = Find(item.Substring(0, equals).Trim());
                if (f == null) { unknown.Add(item.Substring(0, equals).Trim()); continue; }
                if (!seen.Add(f.Name)) { error = "参数重复：" + f.Name; return false; }
                string text = item.Substring(equals + 1).Trim();
                float value; bool flag;
                if (f.Toggle && bool.TryParse(text, out flag)) value = flag ? 1 : 0;
                else if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                { error = "数值格式错误：" + f.Name; return false; }
                merged[f.Name] = value;
            }
            if (!Validate(merged, out error)) return false;
            warning = unknown.Count == 0 ? null : "忽略未知参数：" + string.Join(", ", unknown.ToArray());
            result = merged; return true;
        }
        public static string Export(IDictionary<string, float> values, IDictionary<string, float> baseline, bool differencesOnly)
        {
            StringBuilder b = new StringBuilder("MF");
            foreach (TuningField f in Fields)
            {
                float v = values[f.Name];
                if (differencesOnly && v == baseline[f.Name]) continue;
                b.Append(';').Append(f.ShortName).Append('=').Append(v.ToString("R", CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }
    }
}
