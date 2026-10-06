using System;
using System.Reflection;
using HarmonyLib;

namespace SimplePlanes2MouseFlight
{
    // The inactive game AI otherwise clears all ordinary overrides every Update.
    // Only suppress that monitor for the controls currently owned by our instructor.
    internal static class InstructorOverridePatch
    {
        private const string AiType = "Assets.Scripts.Flight.AI.AiControlledAircraftScript";
        private static Harmony _harmony;
        private static object _controls;
        private static PluginLog _log;
        private static bool _reported;
        public static bool IsInstalled { get; private set; }

        public static void Install(PluginLog log)
        {
            _log = log;
            try
            {
                Type type = Reflect.FindType(AiType);
                MethodInfo target = type == null ? null : type.GetMethod("MonitorInputOverrides",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (target == null) throw new MissingMethodException(AiType, "MonitorInputOverrides");
                _harmony = new Harmony(PluginInfo.Guid + ".instructoroverrides");
                _harmony.Patch(target, new HarmonyMethod(typeof(InstructorOverridePatch).GetMethod(
                    "MonitorPrefix", BindingFlags.NonPublic | BindingFlags.Static)), null, null);
                IsInstalled = true;
                log.Info("飞控通道保护已接入：接管期间避免游戏未启用的 AI 清除插件操纵通道。");
            }
            catch (Exception ex)
            {
                Uninstall();
                log.Warn("飞控通道保护接入失败：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        public static void Bind(object controls)
        {
            _controls = controls;
            _reported = false;
        }
        public static void Unbind(object controls)
        {
            if (ReferenceEquals(_controls, controls)) _controls = null;
        }

        public static void Uninstall()
        {
            _controls = null;
            IsInstalled = false;
            try { if (_harmony != null) _harmony.UnpatchSelf(); }
            catch (Exception) { }
            _harmony = null;
            _log = null;
        }

        private static bool MonitorPrefix(object __instance)
        {
            if (_controls == null) return true;
            object enabled;
            object aircraft;
            object controls;
            string error;
            if (!Reflect.TryGetMember(__instance, AiType, "InputOverrideEnabled", out enabled, out error)
                || !(enabled is bool) || (bool)enabled
                || !Reflect.TryGetMember(__instance, AiType, "AiAircraftScript", out aircraft, out error)
                || !GameApi.TryGetAircraftControls(aircraft, out controls, out error)) return true;
            if (!ReferenceEquals(_controls, controls)) return true;
            if (!_reported)
            {
                _reported = true;
                if (_log != null) _log.Info("已阻止游戏未启用的 AI 清除当前飞机的飞控通道。");
            }
            return false;
        }
    }
}
