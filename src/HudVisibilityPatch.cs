using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 让游戏自带的航炮准星与导弹锁定环一直显示。
    //
    // 为什么它们会消失（IL 转储 Game.dll）：游戏在 `TargetingScript.Update` 里向**当前镜头控制器**询问两件事：
    //   显示中央准星 = 有锁定目标 && 目标允许显示航炮准星 && 当前镜头控制器.AllowGunReticle(目标)
    //   显示锁定环   = 当前镜头控制器.AllowMissileLocking(目标) && 锁定角 > 0
    // 而追尾镜头的这两个方法实际是同一套判断：
    //   AllowMissileLocking(目标) = Vector3.Dot(相机.forward, 目标.forward) > 0.9
    //   AllowGunReticle(目标)     = 该镜头没有相机零件 || 零件允许显示 ? AllowMissileLocking(目标) : false
    // 也就是"相机朝向与目标朝向夹角超过约 25.8 度就隐藏"。飞控接管后相机朝向由瞄准方向决定，
    // 很容易超出这个锥角，于是准星与锁定环被游戏主动隐藏——这正是实测中按 `=` 偏航后两者一起消失的原因。
    //
    // 这两个方法只被 `TargetingScript` 用来决定 HUD 显隐（调用点只有三处：TargetingScript 两处、
    // 以及两个镜头控制器内部互相调用），不影响导弹能否锁定等玩法，所以接管期间强制返回真只改变显示。
    internal static class HudVisibilityPatch
    {
        private static Harmony _harmony;
        private static bool _installed;

        // 由插件每帧写入：飞控接管期间为真，那时才强制显示。
        public static bool ForceVisible;


        private static readonly string[] ControllerTypes =
        {
            "Assets.Scripts.Flight.Cameras.ChaseCameraController",
            "Assets.Scripts.Flight.Cameras.OrbitCameraController"
        };

        public static bool Install(PluginLog log)
        {
            if (_installed)
            {
                return true;
            }

            try
            {
                MethodInfo postfix = typeof(HudVisibilityPatch).GetMethod(
                    "AllowMissileLockingPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                _harmony = new Harmony("com.codex.simpleplanes2.mouseflight.hudvisible");

                int patched = 0;
                for (int i = 0; i < ControllerTypes.Length; i++)
                {
                    Type type = Reflect.FindType(ControllerTypes[i]);
                    if (type == null)
                    {
                        continue;
                    }

                    MethodInfo target = type.GetMethod(
                        "AllowMissileLocking",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(Transform) },
                        null);

                    if (target == null)
                    {
                        continue;
                    }

                    _harmony.Patch(target, null, new HarmonyMethod(postfix), null);
                    patched++;
                }

                _installed = patched > 0;

                if (_installed)
                {
                    log.Info("已挂上准星显示补丁：接管飞控期间强制显示航炮准星与导弹锁定环（共 "
                        + patched + " 个镜头类型）。");
                }
                else
                {
                    log.Warn("准星显示补丁没挂上：没找到目标方法，航炮准星与锁定环会沿用游戏原本的显隐规则。");
                }

                return _installed;
            }
            catch (Exception ex)
            {
                log.Warn("准星显示补丁安装失败：" + ex.GetType().Name + ": " + ex.Message
                    + "。航炮准星与锁定环会沿用游戏原本的显隐规则，其余功能不受影响。");
                _installed = false;
                return false;
            }
        }

        public static void Uninstall()
        {
            ForceVisible = false;

            if (_harmony == null)
            {
                return;
            }

            try
            {
                _harmony.UnpatchSelf();
            }
            catch (Exception)
            {
                // 卸载阶段的异常不影响退出流程。
            }

            _harmony = null;
            _installed = false;
        }

        private static void AllowMissileLockingPostfix(ref bool __result)
        {
            if (ForceVisible)
            {
                __result = true;
            }
        }
    }
}
