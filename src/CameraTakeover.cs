using System.Collections.Generic;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // M1：接管游戏的第三人称镜头。
    //
    // 做法（方案 A，见 local/P1_WORKLOG.md）：不去事后改写相机姿态，而是创建一个属于我们的瞄准物体，
    // 把游戏追尾镜头"跟随的那个物体"换成它。游戏自己的相机更新会把相机摆到我们的姿态上，因此
    // 游戏自带的航炮准星等依赖相机的 HUD 全部保持一致。
    //
    // 曾经的错误做法：在 RenderPipelineManager.beginCameraRendering（晚于所有 LateUpdate）里直接写相机。
    // 那样航炮准星（由 PredictorControllerScript 在游戏自己的 Update 阶段投影得到）永远对不上画面。
    internal sealed class CameraTakeover
    {
        private bool _freeLook;

        public bool SetFreeLook(bool on)
        {
            if (_freeLook == on) return true;
            string error;
            if (on)
            {
                Camera camera;
                object aircraft;
                Vector3 position;
                Quaternion rotation;
                string source;
                if (!GameApi.TryGetMainCamera(out camera, out error) || camera == null
                    || !GameApi.TryGetLocalAircraft(out aircraft, out error)
                    || !GameApi.TryGetAircraftPose(aircraft, out position, out rotation, out source, out error))
                    return FailFreeLook(error);
                _orbitRig.SetPose(position, Quaternion.identity);
                if (_orbitRig.Controller == null
                    && !_orbitRig.TryCreateOrbit(FreeLookName, CurrentDistance(), out error))
                    return FailFreeLook(error);
                GameApi.TrySetCameraName(_orbitRig.Controller, FreeLookName, out error);
                if (!GameApi.TrySeedOrbitCamera(_orbitRig.Controller, camera.transform.position, position, out error))
                    return FailFreeLook(error);
                GameApi.TryEndCameraDrag(_rig.Controller, out error);
                CameraInputPatch.Bind(_orbitRig.Controller);
                CameraInputPatch.FreeLook = true;
                if (!GameApi.TrySwitchToCamera(_orbitRig.Controller, out error)) return FailFreeLook(error);
                _freeLook = true;
                _log.Info("自由视角已切换到原生环绕镜头，中心跟随飞机位置，朝向独立于飞控目标。");
            }
            else
            {
                _freeLook = false;
                GameApi.TryEndCameraDrag(_orbitRig.Controller, out error);
                if (!GameApi.TryTransferCameraZoom(_orbitRig.Controller, _rig.Controller, out error)) return FailFreeLook(error);
                CameraInputPatch.Bind(_rig.Controller);
                _rig.ZeroLookOffset(out error);
                GameApi.TrySetCameraName(_rig.Controller, CameraName, out error);
                if (_active && !GameApi.TrySwitchToCamera(_rig.Controller, out error)) return FailFreeLook(error);
            }
            return true;
        }

        private bool FailFreeLook(string error)
        {
            _freeLook = false;
            CameraInputPatch.FreeLook = false;
            _log.Warn("自由视角镜头切换失败，交还控制：" + error);
            Deactivate("自由视角镜头切换失败");
            return false;
        }
        private readonly PluginSettings _settings;
        private readonly PluginLog _log;
        private readonly System.Func<string> _cameraName, _freeLookName;
        private string _lastName, _lastFreeLookName;
        private string CameraName { get { return _cameraName == null ? _settings.CameraName : _cameraName(); } }
        private string FreeLookName { get { return _freeLookName == null ? CameraName + " Free Look" : _freeLookName(); } }
        private readonly CameraAimRig _rig = new CameraAimRig();
        private readonly CameraAimRig _orbitRig = new CameraAimRig();
        private readonly Dictionary<int, bool> _seenCameras = new Dictionary<int, bool>();

        private bool _active;
        private readonly AimDirection _aim = new AimDirection();
        private object _previousController;
        private float _creationDistance = 30f;
        private float _lastBank;
        private float _lastHeightOffset;
        private float _lastLateralOffset;
        private Vector3 _levelRight = Vector3.right, _previousNose;
        private Vector2 _turnShift;
        private bool _hasPreviousNose;
        private float _nextViewEnforceLogTime;
        private string _lastError;
        private Camera _camera;
        private float _nextDiagnosticTime;
        private float _nextFailureLogTime;
        private int _diagnosticCount;

        public CameraTakeover(PluginSettings settings, PluginLog log, System.Func<string> cameraName = null, System.Func<string> freeLookName = null)
        {
            _settings = settings;
            _log = log;
            _cameraName = cameraName; _freeLookName = freeLookName;
        }
        public void RefreshNames()
        {
            string name=CameraName,free=FreeLookName,error;
            if(name!=_lastName && _rig.Controller!=null && GameApi.TrySetCameraName(_rig.Controller,name,out error)) _lastName=name;
            if(free!=_lastFreeLookName && _orbitRig.Controller!=null && GameApi.TrySetCameraName(_orbitRig.Controller,free,out error)) _lastFreeLookName=free;
        }
        public void EndPanelDrag()
        { string error; if(_freeLook && FlightInputPatch.IsPanelPointerCaptured) GameApi.TryEndCameraDrag(_orbitRig.Controller,out error); }

        public bool IsActive
        {
            get { return _active; }
        }

        // M2：瞄准方向。镜头跟随它，HUD 也读它。
        public AimDirection Aim
        {
            get { return _aim; }
        }


        // 诊断键：把瞄准方向朝偏航方向转一个固定角度（屏幕空间限制在下一帧生效）。
        // 鼠标轴万一在 SP2 里不可用，靠它仍能验证"瞄准方向 → 镜头跟随"这条链路。
        public void StepYaw()
        {
            if (_freeLook) return;
            if (!_active)
            {
                _log.Warn("瞄准方向偏航按键被忽略：当前没有接管镜头。");
                return;
            }

            _aim.RotateYaw(_settings.CameraYawStepDegrees);
            _log.Info("瞄准方向偏航 " + _settings.CameraYawStepDegrees.ToString("0")
                + " 度，当前相对机头 " + _aim.AngleFromNose.ToString("0.0") + " 度"
                + "（屏幕空间限制会在下一帧生效；游戏镜头会带阻尼跟过来）。");
        }

        public bool Toggle()
        {
            if (_active)
            {
                Deactivate("用户按键交还");
                return false;
            }

            return Activate();
        }

        public bool Activate()
        {
            string error;
            Camera camera;
            object aircraft;

            if (!GameApi.TryGetMainCamera(out camera, out error))
            {
                _lastError = error;
                _log.Warn("接管镜头失败：" + error + "。镜头保持由游戏控制。");
                return false;
            }

            if (!GameApi.TryGetLocalAircraft(out aircraft, out error))
            {
                _lastError = "取不到本机载具";
                _log.Warn("接管镜头失败：" + _lastError + "。");
                return false;
            }

            Vector3 position;
            Quaternion rotation;
            string poseSource;
            if (!GameApi.TryGetAircraftPose(aircraft, out position, out rotation, out poseSource, out error))
            {
                _lastError = error;
                _log.Warn("接管镜头失败：" + error + "。");
                return false;
            }

            _active = true;
            _camera = camera;
            _seenCameras.Clear();
            _aim.Reset(rotation * Vector3.forward);
            _hasPreviousNose = false; _turnShift = Vector2.zero; _levelRight = Vector3.right;
            _diagnosticCount = 0;
            _nextDiagnosticTime = 0f;
            _lastError = null;

            // 先摆一次姿态，再挂钩，避免挂钩后第一帧物体还停在原点。
            _rig.SetPose(position + Vector3.up * (CurrentDistance() * _settings.HeightOffsetRatio), LevelAimRotation(rotation * Vector3.forward));

            if (!_settings.CameraApplyEnabled)
            {
                _log.Info("已开始接管镜头（测试）：模式=只诊断不挂钩，只把算出来的姿态写进日志，"
                    + "游戏镜头保持原样。按 " + _settings.CameraTestKey + " 结束。");
                return true;
            }

            // 记下玩家启动时所处的镜头，交还时切回去（作品自带的相机零件视角也只是一个普通控制器，
            // 因此不需要任何特殊处理）。
            object previous;
            if (GameApi.TryGetCameraController(GameApi.CameraManagerCurrentControllerMember, out previous, out error))
            {
                _previousController = previous;
            }
            else
            {
                _previousController = null;
            }

            // 创建属于我们自己的专属镜头并注册进游戏的镜头列表。
            float distance;
            if (!GameApi.TryGetSharedCameraDistance(out distance, out error) || distance <= 1f)
            {
                distance = _settings.CameraDistance;
            }

            // 我们自己镜头的名字会顶掉游戏记下的"上次视角名"，先记下原值，切完再写回去。
            string viewPrefsKey, viewPrefsValue, viewPrefsError;
            bool hasViewPrefs = GameApi.TryReadRememberedView(out viewPrefsKey, out viewPrefsValue, out viewPrefsError);

            _creationDistance = distance;

            if (!_rig.TryCreate(CameraName, distance, _settings.FollowDampScale, out error))
            {
                _active = false;
                _camera = null;
                _lastError = error;
                _log.Warn("接管镜头失败：" + error + "。镜头保持由游戏控制。");
                return false;
            }

            GameApi.TrySetCameraName(_rig.Controller, CameraName, out error);
            // 通过游戏自己的切换流程切到我们的镜头。
            if (!GameApi.TrySwitchToCamera(_rig.Controller, out error))
            {
                _rig.ReleaseHook();
                _active = false;
                _camera = null;
                _lastError = error;
                _log.Warn("接管镜头失败：切换到专属镜头失败：" + error + "。");
                return false;
            }

            // 把游戏记下的视角名写回原值，免得玩家下次进游戏时游戏按我们的名字找不到视角。
            if (hasViewPrefs && !string.Equals(viewPrefsValue, CameraName, System.StringComparison.Ordinal))
            {
                string writeError;
                if (!GameApi.TryWriteRememberedView(viewPrefsKey, viewPrefsValue, out writeError))
                {
                    _log.Warn("写回游戏记下的视角名失败：" + writeError + "。不影响本次接管。");
                }
            }

            _log.Info("已接管镜头（测试）：已创建并注册我们自己的专属镜头并切了过去"
                + (_previousController == null ? "（原视角读不到）" : "；原视角已记下，交还会切回")
                + "；镜头类型=" + _rig.ControllerType
                + "，跟随目标字段=" + _rig.TrackedMember
                + "，保持水平=" + (_rig.StayUprightApplied ? "已打开" : "该镜头没有此属性")
                + "，跟随阻尼=" + (_rig.HasBaseDamp ? (_rig.BaseRotationDamp * _settings.FollowDampScale).ToString("0.00")
                    + "（游戏原本 " + _rig.BaseRotationDamp.ToString("0.00") + " × " + _settings.FollowDampScale.ToString("0.00") + "）" : "读不到")
                + "；载具姿态来源 " + poseSource
                + "。按 " + _settings.CameraTestKey + " 交还，按 " + _settings.CameraYawStepKey
                + " 每次把瞄准方向偏航 " + _settings.CameraYawStepDegrees.ToString("0") + " 度。");
            return true;
        }

        public void Deactivate(string reason)
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _hasPreviousNose = false; _turnShift = Vector2.zero;
            SetFreeLook(false);
            _camera = null;
            _rig.ReleaseHook();
            _orbitRig.ReleaseHook();

            string error;
            bool restored = false;

            if (_previousController != null)
            {
                bool registered;
                if (GameApi.IsCameraControllerRegistered(_previousController, out registered, out error) && registered)
                {
                    restored = GameApi.TrySwitchToCamera(_previousController, out error);
                }
            }

            if (!restored)
            {
                restored = GameApi.TrySwitchToDefaultCamera(out error);
            }

            _previousController = null;

            _log.Info("已交还镜头（原因：" + reason + "），已"
                + (restored ? "切回你启动时所在的视角" : "无法切回原视角，已退回默认视角")
                + "，追尾镜头恢复跟随原来的目标。");
        }

        public void Shutdown()
        {
            Deactivate("插件卸载");
            _rig.Destroy();
            _orbitRig.Destroy();
        }

        // 每帧在 Update 阶段调用：把我们的瞄准物体摆到当前姿态上，让游戏自己的相机更新去跟随。
        public void UpdateAimPose()
        {
            if (!_active)
            {
                return;
            }

            object aircraft;
            string error;

            if (!GameApi.TryGetLocalAircraft(out aircraft, out error))
            {
                ReportTransientFailure(error);
                return;
            }

            Vector3 aircraftPosition;
            Quaternion aircraftRotation;
            string poseSource;
            if (!GameApi.TryGetAircraftPose(aircraft, out aircraftPosition, out aircraftRotation, out poseSource, out error))
            {
                ReportTransientFailure(error);
                return;
            }

            // 机头方向每帧变；瞄准方向由鼠标转动（屏幕空间限制在插件里做），这里只做可选回中。
            Vector3 noseDirection = aircraftRotation * Vector3.forward;
            _aim.SetNose(noseDirection);
            if (!_freeLook) _aim.Recenter(Time.unscaledDeltaTime, _settings.RecenterSeconds);

            // The native chase camera also uses target rotation for its position. Keep roll
            // out of this transform, not just out of the final camera's LookAt up vector.
            Vector3 aimDir = _aim.Direction;
            Quaternion aimRotation = LevelAimRotation(aimDir);

            // 游戏算出来的相机位置是"我们的物体位置 - 相机前向 × 距离"，所以我们把物体抬高、横移，
            // 等价于把相机抬高、横移，飞机在画面里的位置随之改变。
            float distance = CurrentDistance();
            Vector3 rigPosition = ComputeRigPosition(
                aircraftPosition,
                aircraftRotation,
                aimRotation,
                distance);

            _rig.SetPose(rigPosition, aimRotation);
            if (_freeLook) _orbitRig.SetPose(aircraftPosition, Quaternion.identity);

            if (!_settings.CameraApplyEnabled)
            {
                LogDiagnosticIfDue(aircraftPosition, aimRotation, poseSource);
                return;
            }

            _rig.EnsureFollowDamp(_settings.FollowDampScale);

            // 换作品、重载场景等情况游戏可能清空镜头列表，这时重新注册。
            bool reRegistered;
            CameraAimRig activeRig = _freeLook ? _orbitRig : _rig;
            if (!activeRig.EnsureRegistered(out reRegistered, out error))
            {
                ReportTransientFailure(error);
                return;
            }

            if (reRegistered && _log.IsNormal)
            {
                _log.Info("游戏重置了镜头列表，已重新注册专属镜头。");
            }

            // 锁住视角：玩家或游戏切走时切回我们的专属镜头（本阶段只做第三人称追尾）。
            object current;
            string currentError;
            if (GameApi.TryGetCameraController(GameApi.CameraManagerCurrentControllerMember, out current, out currentError)
                && !ReferenceEquals(current, activeRig.Controller))
            {
                string switchError;
                if (GameApi.TrySwitchToCamera(activeRig.Controller, out switchError)
                    && Time.unscaledTime >= _nextViewEnforceLogTime)
                {
                    _nextViewEnforceLogTime = Time.unscaledTime + 5f;
                    _log.Info("接管期间视角被切走，已切回专属镜头。");
                }
            }

            // 清零玩家的鼠标视角偏移：否则玩家推鼠标会在我们的瞄准方向上再叠加一个偏移。
            string offsetError;
            if (!_freeLook && !_rig.ZeroLookOffset(out offsetError))
            {
                ReportTransientFailure(offsetError);
            }

            LogDiagnosticIfDue(aircraftPosition, aimRotation, poseSource);
        }

        // 游戏当前使用的相机距离（随载具大小自适应，接管期间还在缓慢变化），取不到就用创建镜头时的值。
        private float CurrentDistance()
        {
            string error;
            float live;

            if (GameApi.TryGetSharedCameraDistance(out live, out error) && live > 1f)
            {
                return live;
            }

            return _creationDistance;
        }

        private Quaternion LevelAimRotation(Vector3 forward)
        {
            float x,z;
            CameraViewMath.HorizontalRight(forward.x,forward.z,_levelRight.x,_levelRight.z,out x,out z);
            Vector3 right = new Vector3(x,0,z);
            right = (right-forward*Vector3.Dot(right,forward)).normalized;
            _levelRight = right;
            return Quaternion.LookRotation(forward,Vector3.Cross(forward,right).normalized);
        }

        // Smooth the aircraft's real change in nose direction in the level view frame.
        // A stationary bank angle, including a pure roll, cannot translate the camera.
        private Vector3 ComputeRigPosition(
            Vector3 aircraftPosition,
            Quaternion aircraftRotation,
            Quaternion aimRotation,
            float distance)
        {
            _lastHeightOffset = distance * _settings.HeightOffsetRatio;
            _lastLateralOffset = 0f;
            _lastBank = Vector3.Dot(aircraftRotation * Vector3.right, Vector3.up);

            Vector3 position = aircraftPosition + Vector3.up * _lastHeightOffset;

            Vector3 nose = aircraftRotation * Vector3.forward;
            float dt = Time.unscaledDeltaTime;
            Vector3 rate = _hasPreviousNose && dt > .0001f && dt < .2f ? (nose-_previousNose)/dt : Vector3.zero;
            _previousNose=nose; _hasPreviousNose=true;
            Vector3 cameraRight=aimRotation*Vector3.right, cameraUp=aimRotation*Vector3.up;
            bool paused; string error;
            bool moving = !_freeLook && GameApi.TryGetPaused(out paused,out error) && !paused;
            _turnShift.x=CameraViewMath.SmoothTurn(_turnShift.x,moving?Vector3.Dot(rate,cameraRight):0,dt);
            _turnShift.y=CameraViewMath.SmoothTurn(_turnShift.y,moving?Vector3.Dot(rate,cameraUp):0,dt);
            Vector2 shift=Vector2.ClampMagnitude(_turnShift,1)*distance*_settings.TurnShiftRatio;
            _lastLateralOffset=shift.x;
            return position+cameraRight*shift.x+cameraUp*shift.y;
        }

        // 渲染阶段只观察，绝不改写相机。用来核对游戏是否真的跟着我们的物体走。
        public void ObserveCamera(Camera camera)
        {
            if (!_active || camera == null)
            {
                return;
            }

            if (_camera == null)
            {
                string resolveError;
                if (!GameApi.TryGetMainCamera(out _camera, out resolveError))
                {
                    return;
                }
            }

            if (!ReferenceEquals(camera, _camera))
            {
                ReportOtherCamera(camera);
            }
        }

        private void LogDiagnosticIfDue(Vector3 aircraftPosition, Quaternion aimRotation, string poseSource)
        {
            if (_diagnosticCount >= 3 && Time.unscaledTime < _nextDiagnosticTime)
            {
                return;
            }

            _nextDiagnosticTime = Time.unscaledTime + 1f;
            _diagnosticCount++;

            string cameraText = "主相机=读不到";
            Camera camera = _camera;
            if (camera == null)
            {
                string resolveError;
                GameApi.TryGetMainCamera(out camera, out resolveError);
            }

            if (camera != null)
            {
                Vector3 aimForward = aimRotation * Vector3.forward;
                float angle = Vector3.Angle(camera.transform.forward, aimForward);
                cameraText = "主相机(" + camera.name + ") 位置=" + Format(camera.transform.position)
                    + " 朝向=" + Format(camera.transform.eulerAngles)
                    + " 父级=" + DescribeParentChain(camera.transform)
                    + "；相机前向与瞄准方向相差 " + angle.ToString("0.0") + " 度";
            }

            _log.Info("镜头诊断" + _diagnosticCount + "："
                + "瞄准物体 位置=" + Format(aircraftPosition)
                + " 朝向=" + Format(aimRotation.eulerAngles)
                + "（载具姿态来源 " + poseSource + "，"
                + (_settings.CameraApplyEnabled ? "已挂钩" : "未挂钩") + "；"
                + "抬高 " + _lastHeightOffset.ToString("0.0") + " 米，"
                + "横移 " + _lastLateralOffset.ToString("0.0") + " 米，"
                + "机身滚转角 " + _lastBank.ToString("0.00") + "；"
                + "瞄准相对机头 " + _aim.AngleFromNose.ToString("0.0") + " 度，"
                + (_aim.ScreenLimitClamped ? "已被屏幕边缘卡住，" : "")
                + "鼠标增量 " + _aim.LastMouseMagnitude.ToString("0.000") + "）；"
                + cameraText);
        }

        private void ReportTransientFailure(string error)
        {
            _lastError = error;

            // 这类失败可能每帧都出现，限流避免刷屏。
            if (Time.unscaledTime < _nextFailureLogTime)
            {
                return;
            }

            _nextFailureLogTime = Time.unscaledTime + 2f;
            _log.Warn("接管镜头期间出错：" + error + "。");
        }

        private static string DescribeParentChain(Transform transform)
        {
            string text = "";
            Transform current = transform.parent;
            int depth = 0;

            while (current != null && depth < 3)
            {
                text = text.Length == 0 ? current.name : text + " < " + current.name;
                current = current.parent;
                depth++;
            }

            return text.Length == 0 ? "无" : text;
        }

        // 渲染阶段对每个相机都会触发。把非主相机登记一次，用来核对场景里到底有几台相机。
        private void ReportOtherCamera(Camera camera)
        {
            int id = camera.GetInstanceID();
            if (_seenCameras.ContainsKey(id) || _seenCameras.Count >= 8)
            {
                return;
            }

            _seenCameras[id] = true;
            _log.Info("镜头诊断：渲染中出现的非主相机「" + camera.name + "」"
                + "位置=" + Format(camera.transform.position)
                + " 启用=" + camera.enabled
                + " 遮罩=" + camera.cullingMask
                + " 最终目标纹理=" + (camera.targetTexture == null ? "屏幕" : camera.targetTexture.name));
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("0.0") + ", " + value.y.ToString("0.0") + ", " + value.z.ToString("0.0") + ")";
        }

        public string Describe()
        {
            if (!_active)
            {
                return "接管=关（镜头由游戏控制）";
            }

            return "接管=开（" + (_settings.CameraApplyEnabled ? "已挂钩" : "只诊断")
                + "，瞄准方向相对机头 " + _aim.AngleFromNose.ToString("0.0") + " 度）";
        }
    }
}
