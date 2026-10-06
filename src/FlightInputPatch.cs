using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal static class FlightInputPatch
    {
        private static Harmony _harmony;
        private static string _installStage;
        private static Func<bool> _ownsCamera;
        private static object _inputs, _nextView, _fireGuns, _fireWeapons, _toggleJoystick;
        private static object _aircraft;
        private static readonly MouseWeaponState MouseWeapons = new MouseWeaponState();
        public static bool IsInstalled { get; private set; }
        public static bool PanelOpen;
        public static bool PanelPointerCaptured;
        public static Func<bool> PanelPointerCapture;
        public static bool IsPanelPointerCaptured
        { get { return PanelPointerCaptured || (PanelPointerCapture != null && PanelPointerCapture()); } }
        private static bool Owns { get { return IsInstalled && _ownsCamera != null && _ownsCamera(); } }

        public static void Install(PluginLog log, Func<bool> ownsCamera)
        {
            _ownsCamera = ownsCamera;
            try
            {
                Type input = Reflect.FindType(GameApi.GameInputImplementationType);
                Type ui = Reflect.FindType(GameApi.FlightUiScriptType);
                Type camera = Reflect.FindType(GameApi.CameraManagerScriptType);
                Type wrapper = Reflect.FindType("Assets.Scripts.Input.InputWrapper");
                if (input == null || ui == null || camera == null || wrapper == null) throw new MissingMemberException("Flight input types");
                _harmony = new Harmony(PluginInfo.Guid + ".flightinput");
                Patch(input.GetMethod("GetButton", Type.EmptyTypes), "ButtonPostfix", true);
                Patch(input.GetMethod("GetButtonIfEnabled", Type.EmptyTypes), "PanelButtonPostfix", true);
                Patch(input.GetMethod("GetButtonDown", Type.EmptyTypes), "ButtonDownPostfix", true);
                // IfEnabled reads Rewired directly rather than calling GetButtonDown.
                Patch(input.GetMethod("GetButtonDownIfEnabled", Type.EmptyTypes), "ButtonDownPostfix", true);
                PropertyInfo joystick = ui.GetProperty("MouseAsJoystick");
                if (joystick == null) throw new MissingMemberException("MouseAsJoystick");
                Patch(joystick.GetGetMethod(), "JoystickPostfix", true);
                Patch(joystick.GetSetMethod(), "JoystickSetPrefix", false);
                Patch(wrapper.GetMethod("GetMouseAsJoystickAxis", new Type[] { typeof(bool) }), "JoystickAxisPostfix", true);
                MethodInfo update = camera.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, Type.EmptyTypes, null);
                if (update == null) throw new MissingMethodException("CameraManagerScript.Update");
                _installStage = "CameraManagerScript.Update / CameraUpdateTranspiler";
                _harmony.Patch(update, null, null, new HarmonyMethod(Handler("CameraUpdateTranspiler")));
                IsInstalled = true;
                log.Info("飞行输入已接入：下一视角按住自由视角；普通瞄准左键机炮、右键副武器；原镜头切换和鼠标摇杆临时屏蔽。");
            }
            catch (Exception ex)
            {
                string stage = _installStage;
                Uninstall();
                log.Warn("飞行输入接入失败，步骤=" + stage + "，禁止镜头接管：" + ex.ToString());
            }
        }

        private static MethodInfo Handler(string name)
        {
            return typeof(FlightInputPatch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }
        private static void Patch(MethodInfo method, string handler, bool postfix)
        {
            _installStage = (method == null ? "missing target" : method.DeclaringType.FullName + "." + method.Name) + " / " + handler;
            if (method == null) throw new MissingMethodException(handler);
            if (method.DeclaringType.IsInterface || method.IsAbstract || method.GetMethodBody() == null)
                throw new InvalidOperationException("Patch target has no implementation: " + _installStage);
            HarmonyMethod patch = new HarmonyMethod(Handler(handler));
            _harmony.Patch(method, postfix ? null : patch, postfix ? patch : null, null);
        }

        private static bool ResolveInputs()
        {
            object inputs;
            string error;
            if (!GameApi.TryGetGameInputs(out inputs, out error)) { ClearInputs(); return false; }
            if (ReferenceEquals(inputs, _inputs)) return true;
            object next, guns, weapons, joystick;
            if (!GameApi.TryGetGameInput(inputs, "NextView", out next, out error)
                || !GameApi.TryGetGameInput(inputs, "FireGuns", out guns, out error)
                || !GameApi.TryGetGameInput(inputs, "FireWeapons", out weapons, out error)
                || !GameApi.TryGetGameInput(inputs, "ToggleMouseJoystick", out joystick, out error))
            { ClearInputs(); return false; }
            MouseWeapons.Reset();
            _inputs = inputs; _nextView = next; _fireGuns = guns; _fireWeapons = weapons; _toggleJoystick = joystick;
            return true;
        }

        public static bool FreeLookHeld
        {
            get
            {
                if (!Owns || !Application.isFocused || !ResolveInputs()) return false;
                object result;
                string error;
                bool allow, paused;
                if (!GameApi.TryGetAllowKeyboardInputs(out allow, out error) || !allow
                    || !GameApi.TryGetPaused(out paused, out error) || paused) return false;
                return Reflect.TryCall(_nextView, GameApi.GameInputType, "GetButtonIfEnabled", 0, null, out result, out error)
                    && result is bool && (bool)result;
            }
        }

        // Read current gates in the input callback; scene polling can lag behind pause or a dialog.
        private static bool CanAddWeapons()
        {
            if (!Owns || !Application.isFocused || CameraInputPatch.FreeLook || FreeLookHeld) return false;
            string error;
            bool paused, allow, primary;
            int load;
            object aircraft, game, ui, interacting;
            return GameApi.TryGetPaused(out paused, out error) && !paused
                && GameApi.TryGetAllowKeyboardInputs(out allow, out error) && allow
                && GameApi.TryGetLocalAircraft(out aircraft, out error)
                && ReferenceEquals(aircraft, _aircraft)
                && GameApi.TryGetIsPrimaryLocalPlayer(aircraft, out primary, out error) && primary
                && GameApi.TryGetLoadContext(aircraft, out load, out error) && load == GameApi.CraftLoadContextFlight
                && Reflect.TryGetStatic(GameApi.GameType, "Instance", out game, out error) && game != null
                && Reflect.TryGetMember(game, GameApi.GameType, "UIInfo", out ui, out error) && ui != null
                && Reflect.TryGetMember(ui, "Assets.Scripts.GuiNew.UIInfo", "IsInteracting", out interacting, out error)
                && interacting is bool && !(bool)interacting;
        }

        public static void Tick()
        {
            bool enabled = ResolveInputs() && CanAddWeapons();
            MouseWeapons.Update(enabled, Input.GetMouseButton(0), Input.GetMouseButton(1));
        }

        public static void BeginControl()
        {
            string error;
            GameApi.TryGetLocalAircraft(out _aircraft, out error);
            MouseWeapons.Reset();
        }
        public static void EndControl()
        {
            _aircraft = null;
            MouseWeapons.Reset();
        }

        private static void ButtonPostfix(object __instance, ref bool __result)
        {
            if (PanelOpen || IsPanelPointerCaptured) { PanelButtonPostfix(__instance, ref __result); return; }
            if (!Owns || !ResolveInputs()) return;
            bool guns = ReferenceEquals(__instance, _fireGuns);
            bool weapons = ReferenceEquals(__instance, _fireWeapons);
            if (!guns && !weapons) return;
            Tick();
            object enabled;
            string error;
            if (Reflect.TryGetMember(__instance, GameApi.GameInputType, "Enabled", out enabled, out error)
                && enabled is bool && (bool)enabled)
                __result = __result || (guns ? MouseWeapons.Guns : MouseWeapons.Weapons);
        }
        private static void ButtonDownPostfix(object __instance, ref bool __result)
        {
            if (PanelOpen || IsPanelPointerCaptured) PanelButtonPostfix(__instance, ref __result);
            if ((Owns || PanelOpen || IsPanelPointerCaptured) && ResolveInputs() && ReferenceEquals(__instance, _toggleJoystick)) __result = false;
        }
        private static void PanelButtonPostfix(object __instance, ref bool __result)
        {
            if ((PanelOpen || IsPanelPointerCaptured) && ResolveInputs() && (ReferenceEquals(__instance, _fireGuns) || ReferenceEquals(__instance, _fireWeapons)))
                __result = false;
        }
        private static void JoystickPostfix(ref bool __result) { if (Owns || PanelOpen || IsPanelPointerCaptured) __result = false; }
        private static bool JoystickSetPrefix() { return !Owns && !PanelOpen && !IsPanelPointerCaptured; }
        private static void JoystickAxisPostfix(ref Vector2 __result) { if (Owns || PanelOpen || IsPanelPointerCaptured) __result = Vector2.zero; }
        private static bool AllowViewSwitch(bool allowed) { return allowed && !Owns && !PanelOpen; }

        private static IEnumerable<CodeInstruction> CameraUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> result = new List<CodeInstruction>();
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                result.Add(instruction);
                MethodInfo method = instruction.operand as MethodInfo;
                if (method != null && method.Name == "get_AllowKeyboardInputs"
                    && method.DeclaringType.FullName == GameApi.UserInterfaceType)
                {
                    result.Add(new CodeInstruction(OpCodes.Call, Handler("AllowViewSwitch")));
                    matches++;
                }
            }
            if (matches != 1) throw new MissingMethodException("Camera switch input gate: " + matches);
            return result;
        }

        private static void ClearInputs()
        {
            _inputs = _nextView = _fireGuns = _fireWeapons = _toggleJoystick = null;
            MouseWeapons.Reset();
        }
        public static void Uninstall()
        {
            IsInstalled = false;
            PanelOpen = false;
            PanelPointerCaptured = false;
            PanelPointerCapture = null;
            _ownsCamera = null;
            ClearInputs();
            EndControl();
            try { if (_harmony != null) _harmony.UnpatchSelf(); }
            catch (Exception) { }
            _harmony = null;
        }
    }
}
