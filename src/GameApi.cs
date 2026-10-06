using System;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 游戏类型名与成员名的唯一出处。游戏更新改名时只会在这里失配，
    // 表现为功能不工作加一条日志警告，不会让插件加载失败。
    internal static class GameApi
    {
        public const string GameType = "Assets.Scripts.Game";
        public const string GameStateType = "Assets.Scripts.GameState";
        public const string SceneManagerType = "Assets.Scripts.Scenes.SceneManager";
        public const string PauseManagerType = "Assets.Scripts.Flight.PauseManager";
        public const string FlightSceneScriptType = "Assets.Scripts.Flight.FlightSceneScript";
        public const string FlightSceneFlightUIType = "Assets.Scripts.Flight.UI.FlightUIScript";
        public const string FlightScenePlayerType = "Assets.Scripts.Flight.FlightScenePlayer";
        public const string AircraftScriptType = "Assets.Scripts.Craft.AircraftScript";
        public const string AircraftControlsType = "Assets.Scripts.Craft.AircraftControls";
        public const string GameInputsType = "Assets.Scripts.Input.GameInputs";
        public const string GameInputType = "Assets.Scripts.Input.IGameInput";
        public const string GameInputImplementationType = "Assets.Scripts.Input.GameInput";
        public const string UserInterfaceType = "Assets.Scripts.UI.UserInterface";
        public const string CameraManagerScriptType = "Assets.Scripts.Flight.Cameras.CameraManagerScript";
        public const string ChaseCameraControllerType = "Assets.Scripts.Flight.Cameras.ChaseCameraController";
        public const string OrbitCameraControllerType = "Assets.Scripts.Flight.Cameras.OrbitCameraController";
        public const string InteractiveCameraControllerType = "Assets.Scripts.Flight.Cameras.InteractiveCameraController";

        public const string InstanceMember = "Instance";
        public const string GameSceneManagerMember = "SceneManager";
        public const string GameUserInterfaceMember = "UserInterface";
        public const string UserInterfaceAllowKeyboardInputsMember = "AllowKeyboardInputs";
        public const string SceneManagerInFlightSceneMember = "InFlightScene";
        public const string GameStateIsInLevelMember = "IsInLevel";
        public const string GameStateIsInDesignerMember = "IsInDesigner";
        public const string PauseManagerPausedMember = "Paused";
        public const string FlightSceneLocalPlayerMember = "LocalPlayer";
        public const string FlightSceneFlightUIMember = "FlightUI";
        public const string FlightUIMouseAsJoystickMember = "MouseAsJoystick";
        public const string FlightScenePlayerAircraftMember = "Aircraft";
        public const string AircraftControlsMember = "Controls";
        public const string AircraftLoadContextMember = "LoadContext";
        public const string AircraftIsPrimaryLocalPlayerMember = "IsPrimaryLocalPlayer";
        public const string AircraftMouseAxisMember = "MouseAxis";
        public const string AircraftPositionMember = "Position";
        public const string AircraftRotationMember = "Rotation";
        public const string AircraftOrientedCenterOfMassMember = "OrientedCenterOfMassRigidBodies";
        public const string AircraftMainCockpitMember = "MainCockpit";
        public const string SetInputOverrideMethod = "SetInputOverride";
        public const string HasInputOverridesMember = "HasInputOverrides";
        public const string PitchMember = "Pitch";
        public const string RollMember = "Roll";
        public const string YawMember = "Yaw";
        public const string GameInputIdMember = "Id";
        public const string GameInputGetAxisMethod = "GetAxis";
        public const string GameInputPrimaryBindingMethod = "GetKeyboardPrimaryBindingText";
        public const string GameInputSecondaryBindingMethod = "GetKeyboardSecondaryBindingText";
        public const string CameraManagerMainCameraMember = "MainCamera";
        public const string CameraManagerSharedCameraDistanceMember = "SharedCameraDistance";
        public const string CameraManagerCurrentControllerMember = "_currentCameraController";
        public const string CameraManagerCameraControllersMember = "_cameraControllers";
        public const string CameraManagerSwitchToCameraMethod = "SwitchToCamera";
        public const string CameraManagerSwitchToDefaultMethod = "SwitchToDefaultCamera";
        public const string CameraManagerAddCameraMethod = "AddCamera";
        public const string CameraManagerUnregisterCameraMethod = "UnregisterCustomCameraVantage";
        public const string CameraControllerNameMember = "Name";
        public const string FlightUiScriptType = "Assets.Scripts.Flight.UI.FlightUIScript";
        public const string GameInputsNextViewMember = "NextView";
        public const string ChaseCameraStayUprightMember = "StayUpright";
        public const string ChaseCameraRotationDampMember = "_rotationDamp";
        public const string AircraftVelocityMember = "Velocity";
        public const string AircraftAngularVelocityMember = "AngularVelocity";
        public const string AircraftAirSpeedMember = "AirSpeed";
        public const string InteractiveCameraLookOffsetMember = "_deltaRotation";

        // CraftLoadContext 枚举：Default=0、Menu=1、Designer=2、Flight=4、Studio=8。
        public const int CraftLoadContextFlight = 4;

        // 游戏主相机。接管镜头的做法是：在 RenderPipelineManager.beginCameraRendering
        // （URP 下每渲染一个相机前触发，晚于所有 LateUpdate）里改写它的位置与朝向。
        // 之所以要"排在游戏后面写"：游戏每帧在 CameraManagerScript.LateUpdate → UpdateCamera()
        // → 活动控制器里写相机，而 CameraController 继承 System.Object、不是 MonoBehaviour，
        // 没法靠禁用组件阻止它。
        public static bool TryGetMainCamera(out Camera camera, out string error)
        {
            object manager;

            camera = null;

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            object value;
            if (!Reflect.TryGetMember(manager, CameraManagerScriptType, CameraManagerMainCameraMember, out value, out error))
            {
                return false;
            }

            camera = value as Camera;
            if (camera == null)
            {
                error = "CameraManagerScript.MainCamera is not a Camera";
                return false;
            }

            error = null;
            return true;
        }

        // 本机载具的真实姿态。
        //
        // 注意：不能用 AircraftScript 自身所在的物体（AircraftScript.transform）——那是个不动的管理对象，
        // 实测它的位置全程不变、朝向恒为 +Z，用它算出来的镜头会被扔到几千米之外（第一次实机测试黑屏的原因）。
        // AircraftScript 的成员语义（IL 转储）：
        //   Position  = MainCockpit.transform.position（座舱零件的位置，当前世界空间）
        //   Rotation  = OrientedCenterOfMassRigidBodies.eulerAngles（返回的是欧拉角 Vector3，不是四元数）
        //   GlobalPosition = Position 再经 ConvertFloatingOriginToAbsolutePosition 换算成绝对坐标
        // 因此优先取 OrientedCenterOfMassRigidBodies 这个 Transform 的世界位置与朝向；
        // 取不到时退回 Position + Rotation（欧拉角）。
        public static bool TryGetAircraftPose(
            object aircraft,
            out Vector3 position,
            out Quaternion rotation,
            out string source,
            out string error)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            source = null;

            object rig;
            if (Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftOrientedCenterOfMassMember, out rig, out error))
            {
                Transform rigTransform = rig as Transform;
                if (rigTransform != null)
                {
                    position = rigTransform.position;
                    rotation = rigTransform.rotation;
                    source = "OrientedCenterOfMassRigidBodies";
                    error = null;
                    return true;
                }
            }

            object positionValue;
            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftPositionMember, out positionValue, out error)
                || !(positionValue is Vector3))
            {
                error = "取载具位置失败：" + error;
                return false;
            }

            object rotationValue;
            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftRotationMember, out rotationValue, out error)
                || !(rotationValue is Vector3))
            {
                error = "取载具朝向失败：" + error;
                return false;
            }

            position = (Vector3)positionValue;
            rotation = Quaternion.Euler((Vector3)rotationValue);
            source = "Position+Rotation(欧拉角)";
            error = null;
            return true;
        }


        // 游戏按 F9 隐藏界面时 FlightUIScript.UIHidden 为真（静态属性）。
        public static bool TryGetFlightUiHidden(out bool hidden, out string error)
        {
            object value;

            hidden = false;

            if (!Reflect.TryGetStatic(FlightUiScriptType, "UIHidden", out value, out error))
            {
                return false;
            }

            hidden = Equals(value, true);
            error = null;
            return true;
        }

        public static bool TryGetCameraManager(out object manager, out string error)
        {
            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            error = null;
            return true;
        }

        // 读取 CameraManagerScript 上某个控制器字段（_chaseCamera / _defaultController / _currentCameraController）。
        public static bool TryGetCameraController(string memberName, out object controller, out string error)
        {
            object manager;

            controller = null;

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(manager, CameraManagerScriptType, memberName, out controller, out error))
            {
                return false;
            }

            if (controller == null)
            {
                error = "CameraManagerScript." + memberName + " 为空";
                return false;
            }

            error = null;
            return true;
        }

        // 切换到指定镜头。游戏自己的切换流程会一路更新它自己的簿记与 HUD，所以比我们手动摆相机正确。
        public static bool TrySwitchToCamera(object controller, out string error)
        {
            object manager;
            object result;

            if (controller == null)
            {
                error = "目标镜头为空";
                return false;
            }

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            return Reflect.TryCall(
                manager,
                CameraManagerScriptType,
                CameraManagerSwitchToCameraMethod,
                1,
                new object[] { controller },
                out result,
                out error);
        }

        public static bool TrySwitchToDefaultCamera(out string error)
        {
            object manager;
            object result;

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            return Reflect.TryCall(
                manager,
                CameraManagerScriptType,
                CameraManagerSwitchToDefaultMethod,
                0,
                null,
                out result,
                out error);
        }

        // 交还时要用：确认当初记下的镜头现在仍然在游戏的镜头列表里（作品换了、零件没了就可能失效）。
        public static bool IsCameraControllerRegistered(object controller, out bool registered, out string error)
        {
            object manager;
            object listObject;

            registered = false;

            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(manager, CameraManagerScriptType, CameraManagerCameraControllersMember, out listObject, out error))
            {
                return false;
            }

            System.Collections.IList list = listObject as System.Collections.IList;
            if (list == null)
            {
                error = "CameraManagerScript._cameraControllers 不是列表";
                return false;
            }

            registered = list.Contains(controller);
            error = null;
            return true;
        }

        // 自己再要一个镜头对象：用游戏自己的追尾镜头类型构造一个**属于我们**的实例。
        // 这样游戏原有的追尾镜头完全不被触碰，我们的镜头还能作为一个独立条目注册进游戏，
        // 通过游戏的切换流程切过去（详见 P1 文档）。
        // centerOnRigidBody 传 false：让它用"跟随目标 + 朝向 × 偏移"算位置，不依赖零件上的刚体，
        // 这样零件被销毁也不会出错。
        public static bool TryConstructOwnChaseCamera(
            object manager,
            object cockpitPart,
            float distance,
            out object controller,
            out string error)
        {
            controller = null;

            if (manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            object created;
            if (!Reflect.TryConstruct(
                    ChaseCameraControllerType,
                    new object[] { manager, false, distance, cockpitPart },
                    out created,
                    out error))
            {
                return false;
            }

            controller = created;
            error = null;
            return true;
        }

        public static bool TryConstructOwnOrbitCamera(object manager, Func<Transform> target,
            out object controller, out string error)
        {
            // Use the unambiguous native provider constructor; no camera-vantage or cockpit overload.
            return Reflect.TryConstruct(OrbitCameraControllerType,
                new object[] { manager, false, target, null }, out controller, out error);
        }

        public static bool TrySeedOrbitCamera(object controller, Vector3 cameraPosition, Vector3 pivot, out string error)
        {
            Vector3 direction = pivot - cameraPosition;
            float distance = Mathf.Max(1f, direction.magnitude);
            Vector2 angles = OrbitViewSeed.Angles(direction);
            object manager;
            if (!TryGetCameraManager(out manager, out error)) return false;
            return Reflect.TrySetMember(controller, OrbitCameraControllerType, "_currentRotation", angles, out error)
                && Reflect.TrySetMember(controller, InteractiveCameraControllerType, "_deltaRotation", angles, out error)
                && Reflect.TrySetMember(controller, InteractiveCameraControllerType, "_targetDistance", distance, out error)
                && Reflect.TrySetMember(manager, CameraManagerScriptType, CameraManagerSharedCameraDistanceMember, distance, out error)
                && Reflect.TrySetMember(manager, CameraManagerScriptType, "SharedCameraRotation",
                    Quaternion.Euler(angles.x, angles.y, 0f), out error);
        }

        public static bool TryTransferCameraZoom(object source, object target, out string error)
        {
            object distance;
            return Reflect.TryGetMember(source, InteractiveCameraControllerType, "_targetDistance", out distance, out error)
                && Reflect.TrySetMember(target, InteractiveCameraControllerType, "_targetDistance", distance, out error);
        }

        // 把镜头注册进游戏的镜头列表（AddCamera 的实现就是 _cameraControllers.Add）。
        public static bool TryAddCamera(object controller, out string error)
        {
            object manager;
            object result;

            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            return Reflect.TryCall(
                manager,
                CameraManagerScriptType,
                CameraManagerAddCameraMethod,
                1,
                new object[] { controller },
                out result,
                out error);
        }

        // 从游戏镜头列表里摘掉（游戏自己的注销接口就是移除它持有的那几份引用）。
        public static bool TryUnregisterCamera(object controller, out string error)
        {
            object manager;
            object result;

            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            return Reflect.TryCall(
                manager,
                CameraManagerScriptType,
                CameraManagerUnregisterCameraMethod,
                1,
                new object[] { controller },
                out result,
                out error);
        }

        // 镜头跟随瞄准方向的收敛速度由 ChaseCameraController._rotationDamp 决定
        // （Update 里是 Slerp(当前, 目标, _rotationDamp * dt)）。我们的镜头是我们自己构造的实例，
        // 所以可以直接调它的这个值。
        public static bool TryGetRotationDamp(object controller, out float value, out string error)
        {
            object raw;

            value = 0f;

            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            if (!Reflect.TryGetMember(
                    controller,
                    ChaseCameraControllerType,
                    ChaseCameraRotationDampMember,
                    out raw,
                    out error))
            {
                return false;
            }

            if (raw == null)
            {
                error = "阻尼值为空";
                return false;
            }

            value = System.Convert.ToSingle(raw);
            error = null;
            return true;
        }

        public static bool TrySetRotationDamp(object controller, float value, out string error)
        {
            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            return Reflect.TrySetMember(
                controller,
                ChaseCameraControllerType,
                ChaseCameraRotationDampMember,
                value,
                out error);
        }

        // 给镜头改名。CameraController.Name 是可写属性（背后是自动字段），所以直接赋值即可，
        // 切换视角时游戏弹出的那条提示、以及它记进设置里的视角名都会跟着变。
        public static bool TrySetCameraName(object controller, string name, out string error)
        {
            if (controller == null)
            {
                error = "镜头为空";
                return false;
            }

            return Reflect.TrySetMember(
                controller,
                controller.GetType().FullName,
                CameraControllerNameMember,
                name,
                out error);
        }

        // 游戏会把它当前使用的视角名写进 PlayerPrefs（键为 GameInputs.Instance.NextView.Id），
        // 下次进飞行场景时按这个名字恢复上次的视角。我们切到自己的镜头时会把它覆盖掉，
        // 所以切换前后各读一次，切完再写回去，避免影响玩家原来的记忆值。
        public static bool TryReadRememberedView(out string key, out string value, out string error)
        {
            key = null;
            value = null;

            object inputs;
            if (!Reflect.TryGetStatic(GameInputsType, InstanceMember, out inputs, out error) || inputs == null)
            {
                error = "GameInputs.Instance is null";
                return false;
            }

            object nextView;
            if (!Reflect.TryGetMember(inputs, GameInputsType, GameInputsNextViewMember, out nextView, out error)
                || nextView == null)
            {
                error = "GameInputs.NextView 为空";
                return false;
            }

            object id;
            if (!Reflect.TryGetMember(nextView, nextView.GetType().FullName, GameInputIdMember, out id, out error))
            {
                return false;
            }

            key = id as string;
            if (string.IsNullOrEmpty(key))
            {
                error = "取不到视角设置的键名";
                return false;
            }

            value = PlayerPrefs.GetString(key);
            error = null;
            return true;
        }

        public static bool TryWriteRememberedView(string key, string value, out string error)
        {
            if (string.IsNullOrEmpty(key))
            {
                error = "键名为空";
                return false;
            }

            PlayerPrefs.SetString(key, value == null ? string.Empty : value);
            error = null;
            return true;
        }

        // 载具的速度与角速度（世界系，都是 Vector3 属性）。
        public static bool TryGetAircraftMotion(
            object aircraft,
            out Vector3 velocity,
            out Vector3 angularVelocity,
            out string error)
        {
            object raw;

            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;

            if (aircraft == null)
            {
                error = "载具为空";
                return false;
            }

            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftVelocityMember, out raw, out error))
            {
                return false;
            }

            if (!(raw is Vector3) || !FlightRecorder.Finite((Vector3)raw))
            { error = "载具速度类型错误或包含无效数值"; return false; }
            velocity = (Vector3)raw;

            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftAngularVelocityMember, out raw, out error))
            {
                return false;
            }

            if (!(raw is Vector3) || !FlightRecorder.Finite((Vector3)raw))
            { error = "载具角速度类型错误或包含无效数值"; return false; }
            angularVelocity = (Vector3)raw;
            error = null;
            return true;
        }

        public static bool TryGetAircraftAirSpeed(object aircraft, out float airSpeed, out string error)
        {
            object raw;

            airSpeed = 0f;

            if (aircraft == null)
            {
                error = "载具为空";
                return false;
            }

            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftAirSpeedMember, out raw, out error))
            {
                return false;
            }

            if (raw == null)
            {
                error = "空速值为空";
                return false;
            }

            airSpeed = System.Convert.ToSingle(raw);
            error = null;
            return true;
        }

        private static Vector3 ToVector3(object raw)
        {
            return raw is Vector3 ? (Vector3)raw : Vector3.zero;
        }

        public static bool TryGetAircraftCockpitPart(object aircraft, out object part, out string error)
        {
            part = null;

            if (aircraft == null)
            {
                error = "载具为空";
                return false;
            }

            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftMainCockpitMember, out part, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        // 把镜头"跟随的那个物体"换成我们提供的物体。
        // 不同控制器用的字段名不同（IL 转储）：
        //   追尾 ChaseCameraController: _transformToTrack
        //   环绕 OrbitCameraController: _targetTransform
        // 座舱用 _targetPart（本阶段不支持）。所以按运行时真实类型逐个试。
        private static readonly string[] TrackedTransformCandidates =
        {
            "_transformToTrack",
            "_targetTransform"
        };

        public static bool TryHookCameraTarget(
            object controller,
            Func<Transform> provider,
            out object original,
            out string memberUsed,
            out string controllerType,
            out string error)
        {
            original = null;
            memberUsed = null;
            controllerType = null;
            error = null;

            if (controller == null)
            {
                error = "相机控制器为空";
                return false;
            }

            System.Type type = controller.GetType();
            string typeName = type.FullName;
            controllerType = typeName;

            for (int i = 0; i < TrackedTransformCandidates.Length; i++)
            {
                string candidate = TrackedTransformCandidates[i];
                object current;
                string readError;

                if (!Reflect.TryGetMember(controller, typeName, candidate, out current, out readError))
                {
                    continue;
                }

                string writeError;
                if (!Reflect.TrySetMember(controller, typeName, candidate, provider, out writeError))
                {
                    error = "替换 " + candidate + " 失败：" + writeError;
                    return false;
                }

                original = current;
                memberUsed = candidate;
                return true;
            }

            error = "当前视角不支持接管：控制器 " + typeName
                + " 上找不到跟随目标的字段（目前支持追尾与环绕两种第三人称镜头）";
            return false;
        }

        public static bool TryRestoreCameraTarget(object controller, string memberName, object original, out string error)
        {
            if (controller == null || memberName == null)
            {
                error = "没有需要还原的跟随目标";
                return false;
            }

            return Reflect.TrySetMember(controller, controller.GetType().FullName, memberName, original, out error);
        }


        // 让追尾镜头保持水平、不随机身滚转——这正是战雷那种手感，而且本来就是游戏自带的功能。
        // 控制器没有这个属性时返回 false，且不算错误（环绕镜头就没有）。
        public static bool TryForceStayUpright(object controller, out object original, out string error)
        {
            original = null;
            error = null;

            if (controller == null)
            {
                return false;
            }

            string typeName = controller.GetType().FullName;
            object current;
            string readError;

            if (!Reflect.TryGetMember(controller, typeName, ChaseCameraStayUprightMember, out current, out readError))
            {
                return false;
            }

            if (Equals(current, true))
            {
                original = current;
                return true;
            }

            if (!Reflect.TrySetMember(controller, typeName, ChaseCameraStayUprightMember, true, out error))
            {
                return false;
            }

            original = current;
            return true;
        }

        public static bool TryRestoreStayUpright(object controller, object original, out string error)
        {
            if (controller == null || original == null)
            {
                error = "没有需要还原的保持水平设置";
                return false;
            }

            return Reflect.TrySetMember(
                controller,
                controller.GetType().FullName,
                ChaseCameraStayUprightMember,
                original,
                out error);
        }

        // 玩家的鼠标视角偏移。我们接管镜头时把它清零，让相机完全听我们的；
        // 否则玩家推鼠标会在我们的方向上再叠加一个偏移，画面永远追不上瞄准方向。
        public static bool TryZeroCameraLookOffset(object controller, out string error)
        {
            return Reflect.TrySetMember(
                controller,
                InteractiveCameraControllerType,
                InteractiveCameraLookOffsetMember,
                Vector2.zero,
                out error);
        }

        public static bool TryEndCameraDrag(object controller, out string error)
        {
            return Reflect.TrySetMember(controller, InteractiveCameraControllerType,
                "_touching", false, out error);
        }


        // 游戏当前使用的相机距离，用来让接管后的取景与游戏本身一致。
        public static bool TryGetSharedCameraDistance(out float distance, out string error)
        {
            object manager;
            object raw;

            distance = 0f;

            if (!Reflect.TryGetStatic(CameraManagerScriptType, InstanceMember, out manager, out error)
                || manager == null)
            {
                error = "CameraManagerScript.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(manager, CameraManagerScriptType, CameraManagerSharedCameraDistanceMember, out raw, out error))
            {
                return false;
            }

            if (!(raw is float))
            {
                error = "CameraManagerScript.SharedCameraDistance is not a float";
                return false;
            }

            distance = (float)raw;
            error = null;
            return true;
        }

        public static bool IsAlive(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return unityObject != null;
        }

        // 飞行场景判定（IL 转储 Game.dll，SceneManager::OnSceneLoading）：
        //   SceneManager.InFlightScene 只在场景名为 "Terrain" 时置真；
        //   GameState.IsInLevel 写的是 InDesignerScene || InFlightScene，也就是设计器里也为真，
        //   所以它不能单独当飞行信号，必须与 IsInDesigner 一起读。
        // SceneManager 在本版本里没有静态 Instance，实例只能经 Game.Instance.SceneManager 取
        // （IL 转储 Game.dll，Game::get_SceneManager）。
        public static bool TryGetFlightSceneActive(out bool active, out string detail)
        {
            bool inFlightScene;

            if (TryGetSceneManagerInFlightScene(out inFlightScene))
            {
                active = inFlightScene;
                detail = "SceneManager.InFlightScene=" + inFlightScene;
                return true;
            }

            bool isInLevel;
            bool isInDesigner;
            if (!TryGetGameStateSceneFlags(out isInLevel, out isInDesigner))
            {
                active = false;
                detail = "no flight-scene signal";
                return false;
            }

            active = isInLevel && !isInDesigner;
            detail = "GameState.IsInLevel=" + isInLevel + ", IsInDesigner=" + isInDesigner;
            return true;
        }

        private static bool TryGetSceneManagerInFlightScene(out bool value)
        {
            object game;
            object sceneManager;
            object raw;
            string error;

            value = false;

            if (!Reflect.TryGetStatic(GameType, InstanceMember, out game, out error) || game == null)
            {
                return false;
            }

            if (!Reflect.TryGetMember(game, GameType, GameSceneManagerMember, out sceneManager, out error)
                || sceneManager == null)
            {
                return false;
            }

            if (!Reflect.TryGetMember(sceneManager, SceneManagerType, SceneManagerInFlightSceneMember, out raw, out error)
                || !(raw is bool))
            {
                return false;
            }

            value = (bool)raw;
            return true;
        }

        private static bool TryGetGameStateSceneFlags(out bool isInLevel, out bool isInDesigner)
        {
            object gameState;
            object rawLevel;
            object rawDesigner;
            string error;

            isInLevel = false;
            isInDesigner = false;

            if (!Reflect.TryGetStatic(GameStateType, InstanceMember, out gameState, out error) || gameState == null)
            {
                return false;
            }

            if (!Reflect.TryGetMember(gameState, GameStateType, GameStateIsInLevelMember, out rawLevel, out error)
                || !(rawLevel is bool))
            {
                return false;
            }

            if (!Reflect.TryGetMember(gameState, GameStateType, GameStateIsInDesignerMember, out rawDesigner, out error)
                || !(rawDesigner is bool))
            {
                return false;
            }

            isInLevel = (bool)rawLevel;
            isInDesigner = (bool)rawDesigner;
            return true;
        }

        public static bool TryGetLocalAircraft(out object aircraft, out string error)
        {
            object scene;
            object player;

            aircraft = null;

            if (!Reflect.TryGetStatic(FlightSceneScriptType, InstanceMember, out scene, out error))
            {
                return false;
            }

            if (scene == null)
            {
                error = "FlightSceneScript.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(scene, FlightSceneScriptType, FlightSceneLocalPlayerMember, out player, out error))
            {
                return false;
            }

            if (player == null)
            {
                error = "FlightSceneScript.LocalPlayer is null";
                return false;
            }

            if (!Reflect.TryGetMember(player, FlightScenePlayerType, FlightScenePlayerAircraftMember, out aircraft, out error))
            {
                return false;
            }

            if (!IsAlive(aircraft))
            {
                error = "LocalPlayer.Aircraft is null or destroyed";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryGetAircraftControls(object aircraft, out object controls, out string error)
        {
            controls = null;
            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftControlsMember, out controls, out error))
            {
                return false;
            }

            if (controls == null)
            {
                error = "AircraftScript.Controls is null";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryGetLoadContext(object aircraft, out int loadContext, out string error)
        {
            object value;

            loadContext = -1;
            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftLoadContextMember, out value, out error))
            {
                return false;
            }

            if (value == null)
            {
                error = "AircraftScript.LoadContext is null";
                return false;
            }

            try
            {
                loadContext = Convert.ToInt32(value);
            }
            catch (Exception ex)
            {
                error = "AircraftScript.LoadContext is not an integer enum: " + ex.GetType().Name;
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryGetIsPrimaryLocalPlayer(object aircraft, out bool isPrimaryLocalPlayer, out string error)
        {
            object value;

            isPrimaryLocalPlayer = false;
            if (!Reflect.TryGetMember(aircraft, AircraftScriptType, AircraftIsPrimaryLocalPlayerMember, out value, out error))
            {
                return false;
            }

            if (!(value is bool))
            {
                error = "AircraftScript.IsPrimaryLocalPlayer is not a bool";
                return false;
            }

            isPrimaryLocalPlayer = (bool)value;
            error = null;
            return true;
        }

        public static bool TryGetPaused(out bool paused, out string error)
        {
            object value;

            paused = false;
            if (!Reflect.TryGetStatic(PauseManagerType, PauseManagerPausedMember, out value, out error))
            {
                return false;
            }

            if (!(value is bool))
            {
                error = "PauseManager.Paused is not a bool";
                return false;
            }

            paused = (bool)value;
            error = null;
            return true;
        }

        public static bool TryGetAllowKeyboardInputs(out bool allow, out string error)
        {
            object game;
            object userInterface;
            object value;

            allow = false;

            if (!Reflect.TryGetStatic(GameType, InstanceMember, out game, out error))
            {
                return false;
            }

            if (game == null)
            {
                error = "Game.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(game, GameType, GameUserInterfaceMember, out userInterface, out error))
            {
                return false;
            }

            if (userInterface == null)
            {
                error = "Game.UserInterface is null";
                return false;
            }

            if (!Reflect.TryGetMember(userInterface, UserInterfaceType, UserInterfaceAllowKeyboardInputsMember, out value, out error))
            {
                return false;
            }

            if (!(value is bool))
            {
                error = "UserInterface.AllowKeyboardInputs is not a bool";
                return false;
            }

            allow = (bool)value;
            error = null;
            return true;
        }

        public static bool TryGetGameInputs(out object gameInputs, out string error)
        {
            gameInputs = null;
            if (!Reflect.TryGetStatic(GameInputsType, InstanceMember, out gameInputs, out error))
            {
                return false;
            }

            if (gameInputs == null)
            {
                error = "GameInputs.Instance is null";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryGetGameInput(object gameInputs, string memberName, out object input, out string error)
        {
            input = null;
            if (!Reflect.TryGetMember(gameInputs, GameInputsType, memberName, out input, out error))
            {
                return false;
            }

            if (input == null)
            {
                error = "GameInputs." + memberName + " is null";
                return false;
            }

            error = null;
            return true;
        }


        // 读取玩家对这个通道的原始输入。
        // 必须在注册覆盖之前确认它可用：覆盖一旦注册，游戏就不再自己读输入，
        // 我们的委托若返回空值，该通道会保持上一个值（不是退回正常输入）。
        // 依据：IL 转储 AircraftControls::GetControlInput 与 Update 里 Roll/Pitch 分支。
        public static bool TryReadPlayerInputAxis(object input, out float value, out string error)
        {
            object enabled;
            value = 0f;
            if (!Reflect.TryGetMember(input, GameInputType, "Enabled", out enabled, out error)) return false;
            if (!(enabled is bool)) { error = "IGameInput.Enabled is not a bool"; return false; }
            if (!(bool)enabled) { error = null; return true; }
            return TryReadInputAxis(input, out value, out error);
        }

        public static bool TryReadInputAxis(object input, out float value, out string error)
        {
            object result;

            value = 0f;
            if (!Reflect.TryCall(input, GameInputType, GameInputGetAxisMethod, 0, null, out result, out error))
            {
                return false;
            }

            if (!(result is float))
            {
                error = "IGameInput.GetAxis() did not return a float";
                return false;
            }

            float raw = (float)result;
            if (float.IsNaN(raw) || float.IsInfinity(raw))
            {
                error = "IGameInput.GetAxis() returned " + raw;
                return false;
            }

            value = raw < -1f ? -1f : (raw > 1f ? 1f : raw);
            error = null;
            return true;
        }

        // 把游戏自带的键盘按键表读成文本，用于确认插件按键有没有和游戏冲突。
        public static bool TryGetKeyboardBindings(out string[] lines, out string error)
        {
            lines = null;
            error = null;

            object gameInputs;
            if (!TryGetGameInputs(out gameInputs, out error))
            {
                return false;
            }

            Type inputType = Reflect.FindType(GameInputType);
            if (inputType == null)
            {
                error = "type not found: " + GameInputType;
                return false;
            }

            Type type = gameInputs.GetType();
            System.Collections.Generic.List<string> result = new System.Collections.Generic.List<string>();
            System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance;

            System.Reflection.PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                object value = null;
                try
                {
                    if (!properties[i].CanRead || properties[i].GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    value = properties[i].GetValue(gameInputs, null);
                }
                catch (Exception)
                {
                    value = null;
                }

                AppendBinding(result, properties[i].Name, value, inputType);
            }

            System.Reflection.FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                object value = null;
                try
                {
                    value = fields[i].GetValue(gameInputs);
                }
                catch (Exception)
                {
                    value = null;
                }

                AppendBinding(result, fields[i].Name, value, inputType);
            }

            if (result.Count == 0)
            {
                error = "GameInputs 里没有找到任何键盘输入项";
                return false;
            }

            lines = result.ToArray();
            return true;
        }

        private static void AppendBinding(
            System.Collections.Generic.List<string> result,
            string memberName,
            object value,
            Type inputType)
        {
            if (value == null || !inputType.IsInstanceOfType(value))
            {
                return;
            }

            string primary = ReadInputText(value, GameInputPrimaryBindingMethod);
            string secondary = ReadInputText(value, GameInputSecondaryBindingMethod);

            if (primary.Length == 0 && secondary.Length == 0)
            {
                return;
            }

            result.Add(memberName + " = " + primary
                + (secondary.Length == 0 ? "" : " / " + secondary));
        }

        private static string ReadInputText(object input, string methodName)
        {
            object result;
            string error;

            if (!Reflect.TryCall(input, GameInputType, methodName, 0, null, out result, out error))
            {
                return "";
            }

            string text = result as string;
            return text == null ? "" : text.Trim();
        }

        public static bool TryGetControlsFloat(object controls, string memberName, out float value, out string error)
        {
            object raw;

            value = 0f;
            if (!Reflect.TryGetMember(controls, AircraftControlsType, memberName, out raw, out error))
            {
                return false;
            }

            if (!(raw is float))
            {
                error = "AircraftControls." + memberName + " is not a float";
                return false;
            }

            value = (float)raw;
            error = null;
            return true;
        }

        public static bool TryGetHasInputOverrides(object controls, out bool hasOverrides, out string error)
        {
            object raw;

            hasOverrides = false;
            if (!Reflect.TryGetMember(controls, AircraftControlsType, HasInputOverridesMember, out raw, out error))
            {
                return false;
            }

            if (!(raw is bool))
            {
                error = "AircraftControls.HasInputOverrides is not a bool";
                return false;
            }

            hasOverrides = (bool)raw;
            error = null;
            return true;
        }

        // 游戏自己算出的"鼠标当摇杆"增量（AircraftControls.MouseAxis）。
        // 覆盖生效时游戏不会把它叠加到俯仰/滚转上，所以把这个值记进日志，
        // 就能直接看出"鼠标摇杆被短路"是否真的发生。
        public static bool TryGetMouseAxis(object controls, out float x, out float y, out string error)
        {
            object raw;

            x = 0f;
            y = 0f;
            if (!Reflect.TryGetMember(controls, AircraftControlsType, AircraftMouseAxisMember, out raw, out error))
            {
                return false;
            }

            if (!(raw is Vector2))
            {
                error = "AircraftControls.MouseAxis is not a Vector2";
                return false;
            }

            Vector2 value = (Vector2)raw;
            if (float.IsNaN(value.x) || float.IsNaN(value.y)
                || float.IsInfinity(value.x) || float.IsInfinity(value.y))
            {
                error = "AircraftControls.MouseAxis is not finite";
                return false;
            }

            x = value.x;
            y = value.y;
            error = null;
            return true;
        }

        // 鼠标当摇杆当前是否处于开启状态（右键切换的运行时状态，不是设置里那个开关）。
        // 开启时玩家的俯仰/滚转来自鼠标光标位置，值存在 AircraftControls.MouseAxis 里。
        public static bool TryGetMouseAsJoystickState(out bool isOn, out string error)
        {
            object scene;
            object flightUI;
            object value;

            isOn = false;

            if (!Reflect.TryGetStatic(FlightSceneScriptType, InstanceMember, out scene, out error)
                || scene == null)
            {
                error = "FlightSceneScript.Instance is null";
                return false;
            }

            if (!Reflect.TryGetMember(scene, FlightSceneScriptType, FlightSceneFlightUIMember, out flightUI, out error)
                || flightUI == null)
            {
                error = "FlightSceneScript.FlightUI is null";
                return false;
            }

            if (!Reflect.TryGetMember(flightUI, FlightSceneFlightUIType, FlightUIMouseAsJoystickMember, out value, out error))
            {
                return false;
            }

            if (!(value is bool))
            {
                error = "FlightUIScript.MouseAsJoystick is not a bool";
                return false;
            }

            isOn = (bool)value;
            error = null;
            return true;
        }
    }
}
