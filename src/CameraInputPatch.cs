using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Only the plugin's controller is gated. Drag rotation and wheel zoom remain game code.
    internal static class CameraInputPatch
    {
        private static Harmony _harmony;
        private static object _controller;
        private static Action _updateCursor;
        public static bool IsInstalled { get; private set; }
        public static string InstallError { get; private set; }
        public static bool FreeLook;

        public static void Install(PluginLog log, Action updateCursor)
        {
            InstallError = null;
            _updateCursor = updateCursor;
            try
            {
                Type type = Reflect.FindType(GameApi.InteractiveCameraControllerType);
                if (type == null) throw new MissingMemberException(GameApi.InteractiveCameraControllerType);
                _harmony = new Harmony(PluginInfo.Guid + ".camerainput");
                Patch(type.GetMethod("HandleInput", BindingFlags.Public | BindingFlags.Instance), "HandleInputPrefix", false);
                Patch(type.GetMethod("Rotate", new Type[] { typeof(Vector2), typeof(bool) }), "RotatePrefix", false);
                Patch(type.GetMethod("UpdateCursor", BindingFlags.Public | BindingFlags.Instance), "UpdateCursorPrefix", false);
                Patch(type.GetProperty("MouseLook").GetGetMethod(), "MouseLookPostfix", true);
                MethodInfo update = type.GetMethod("Update", new Type[] { typeof(int) });
                if (update == null) throw new MissingMethodException("InteractiveCameraController.Update(int)");
                _harmony.Patch(update, null, null, new HarmonyMethod(typeof(CameraInputPatch).GetMethod(
                    "UpdateTranspiler", BindingFlags.Static | BindingFlags.NonPublic)));
                IsInstalled = true;
                log.Info("自由视角输入已接入：专属镜头复用游戏左键拖动与滚轮缩放。");
            }
            catch (Exception ex)
            {
                InstallError = ex.ToString();
                Uninstall();
                log.Warn("自由视角输入接入失败，专属镜头不启动：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void Patch(MethodInfo target, string handler, bool postfix)
        {
            if (target == null) throw new MissingMethodException(handler);
            HarmonyMethod patch = new HarmonyMethod(typeof(CameraInputPatch).GetMethod(handler,
                BindingFlags.Static | BindingFlags.NonPublic));
            _harmony.Patch(target, postfix ? null : patch, postfix ? patch : null, null);
        }

        public static void Bind(object controller)
        {
            _controller = controller;
            FreeLook = false;
        }

        public static void Unbind(object controller)
        {
            if (!ReferenceEquals(_controller, controller)) return;
            _controller = null;
            FreeLook = false;
        }

        public static void Uninstall()
        {
            _controller = null;
            FreeLook = false;
            IsInstalled = false;
            _updateCursor = null;
            try
            {
                if (_harmony != null) _harmony.UnpatchSelf();
            }
            catch (Exception)
            {
                // Ownership is already cleared, so any remaining hook is a native pass-through.
            }
            _harmony = null;
        }

        private static bool Owns(object controller)
        {
            return _controller != null && ReferenceEquals(_controller, controller);
        }

        private static bool AllowCameraInput(bool pointerInside, object controller)
        {
            if (FlightInputPatch.PanelOpen || FlightInputPatch.IsPanelPointerCaptured) return false;
            // Locked cursor has no UI hover position. Keep native UI/pause gates and zoom code.
            return pointerInside || (Owns(controller) && !FreeLook && Application.isFocused);
        }

        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> result = new List<CodeInstruction>();
            int matches = 0;
            MethodInfo gate = typeof(CameraInputPatch).GetMethod("AllowCameraInput", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (CodeInstruction instruction in instructions)
            {
                result.Add(instruction);
                MethodInfo method = instruction.operand as MethodInfo;
                if (method != null && method.Name == "get_IsPointerInsideGameView"
                    && method.DeclaringType.FullName == GameApi.FlightSceneFlightUIType)
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    result.Add(new CodeInstruction(OpCodes.Call, gate));
                    matches++;
                }
            }
            if (matches != 1) throw new MissingMethodException("Camera Update pointer gate: " + matches);
            return result;
        }

        private static bool HandleInputPrefix(object __instance, object __0)
        {
            if (FlightInputPatch.PanelOpen || FlightInputPatch.IsPanelPointerCaptured) return false;
            if (!Owns(__instance)) return true;
            if (!FreeLook) return false;
            object button;
            string error;
            // Native Primary input includes begin, drag and end; do not leave its touch state stuck.
            return __0 != null && Reflect.TryGetMember(__0, __0.GetType().FullName,
                "InputButton", out button, out error) && Convert.ToInt32(button) == 0;
        }

        private static bool RotatePrefix(object __instance)
        {
            if (FlightInputPatch.PanelOpen || FlightInputPatch.IsPanelPointerCaptured) return false;
            return !Owns(__instance) || FreeLook;
        }

        private static void MouseLookPostfix(object __instance, ref bool __result)
        {
            // Disable continuous unbuttoned look; HandleInput still supports native Primary dragging.
            if (Owns(__instance)) __result = false;
        }

        private static bool UpdateCursorPrefix(object __instance)
        {
            if (FlightInputPatch.PanelOpen)
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return false; }
            if (!Owns(__instance)) return true;
            if (_updateCursor != null) _updateCursor();
            return false;
        }
    }
}
