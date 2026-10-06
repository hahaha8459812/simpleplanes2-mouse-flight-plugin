using BepInEx;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimplePlanes2MouseFlight
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public sealed class MouseFlightPlugin : BaseUnityPlugin
    {
        private PluginLog _log;
        private PluginSettings _settings;
        private TestHotkeys _hotkeys;
        private FlightContextService _service;
        private CameraTakeover _camera;
        private MouseReader _mouse;
        private AimHud _hud;
        private Instructor _instructor;
        private Camera _frameCamera;
        private Quaternion _frameRotation;
        private Vector3 _frameNose;
        private Vector3 _frameUp;
        private Vector3 _frameRight;
        private bool _frameResolved;
        private CraftTuning _craftTuning;
        private FlightTuningPanel _panel;
        private FlightRecorder _recorder;
        private FlightPanelSettings _panelSettings;

        private void Awake()
        {
            _settings = new PluginSettings(Config);
            _panelSettings = new FlightPanelSettings(Config);
            _log = new PluginLog(Logger);
            _log.SetLevelText(_settings.LogLevel);
            _hotkeys = new TestHotkeys(_settings);
            _service = new FlightContextService(_settings, _log);
            _camera = new CameraTakeover(_settings, _log,
                delegate { return _panelSettings.English ? "Mouse Flight" : "鼠标飞控"; },
                delegate { return _panelSettings.English ? "Free Look" : "自由视角"; });
            _mouse = new MouseReader(_settings, _log);
            CameraInputPatch.Install(_log, _mouse.Tick);
            _hud = new AimHud(_settings, _log);
            _instructor = new Instructor(_settings, _log);
            InstructorOverridePatch.Install(_log);
            _recorder = new FlightRecorder(_settings, _log);
            _craftTuning = new CraftTuning(_settings, _log);
            _panel = new FlightTuningPanel(_settings, _craftTuning, ReleaseForPanel, ResumeFromPanel, _recorder.RecordTuningChanged,
                delegate { return _camera.IsActive; }, _panelSettings);
            StartCoroutine(RecordPhysicsSteps());

            _log.Info("教官参数（生效值）：" + _settings.DescribeEffectiveConfig());

            _log.Info("===== " + PluginInfo.Name + " " + PluginInfo.Version + " 加载（P1-M2：鼠标瞄准与准星）=====");
            if (!string.IsNullOrEmpty(_settings.ConfigMigrationNote))
            {
                _log.Info(_settings.ConfigMigrationNote);
            }

            _log.Info("配置：Enabled=" + _settings.Enabled
                + ", LogLevel=" + _settings.LogLevel
                + ", HeartbeatSeconds=" + _settings.HeartbeatSeconds
                + ", CameraTestKey=" + _settings.CameraTestKey
                + ", FreeLookInput=GameInputs.NextView"
                + ", CameraYawStepKey=" + _settings.CameraYawStepKey
                + ", CameraYawStepDegrees=" + _settings.CameraYawStepDegrees
                + ", CameraName=" + _settings.CameraName
                + ", CameraDistance=" + _settings.CameraDistance
                + ", UseGameCameraDistance=" + _settings.UseGameCameraDistance
                + ", CameraApplyEnabled=" + _settings.CameraApplyEnabled
                + ", CameraMaxShift=" + _settings.CameraMaxShift
                + ", HeightOffsetRatio=" + _settings.HeightOffsetRatio
                + ", TurnShiftRatio=" + _settings.TurnShiftRatio
                + ", ForceHudVisible=" + _settings.ForceHudVisible
                + ", FollowDampScale=" + _settings.FollowDampScale
                + ", SensitivityDegreesPerUnit=" + _settings.SensitivityDegreesPerUnit
                + ", Deadzone=" + _settings.MouseDeadzone
                + ", ScreenEdgeMarginRatio=" + _settings.ScreenEdgeMarginRatio
                + ", RecenterSeconds=" + _settings.RecenterSeconds
                + ", UseMousePositionDelta=" + _settings.UseMousePositionDelta
                + ", ShowCrosshair=" + _settings.ShowCrosshair
                + ", ShowNoseMarker=" + _settings.ShowNoseMarker
                + ", ShowLinkLine=" + _settings.ShowLinkLine
                + ", HideWithGameUi=" + _settings.HideHudWithGameUi
                + ", CrosshairRadiusRatio=" + _settings.CrosshairRadiusRatio
                + ", NoseMarkerSizeRatio=" + _settings.NoseMarkerSizeRatio
                + ", DashSpacingRatio=" + _settings.DashSpacingRatio
                + ", InstructorEnabled=" + _settings.InstructorEnabled
                + ", KdRoll=" + _settings.KdRoll
                + ", RollSign=" + _settings.RollSign
                + ", PitchSign=" + _settings.PitchSign
                + ", TuningPanelKey=" + _settings.OverlayKey
                + ", RecordKey=" + _settings.RecordKey
);
            _log.Info("M2 测试方式：飞行中按 " + _settings.CameraTestKey + " 接管，接管后移动鼠标瞄准："
                + "飞控准星（大环）跟着鼠标走，机头指向（小十字）跟着飞机，两者之间的虚线与屏幕中心相连；"
                + "按 " + _settings.CameraYawStepKey + " 可把瞄准方向偏航 "
                + _settings.CameraYawStepDegrees.ToString("0") + " 度（诊断用），再按 "
                + _settings.CameraTestKey + " 交还。接管只在飞行场景、有本机载具且未暂停时生效。");
            _log.Info("独立日志文件：" + (_log.FilePath == null ? "未创建" : _log.FilePath));

            if (_settings.ForceHudVisible)
            {
                HudVisibilityPatch.Install(_log);
            }
            else
            {
                _log.Info("按配置不强制显示准星与锁定环，沿用游戏原本的显隐规则。");
            }

            // 只用来看游戏最终把相机摆到了哪里（诊断），不做任何改写。
            // 游戏用 URP，Camera.onPreCull 不会触发；beginCameraRendering 晚于所有 LateUpdate。
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private bool _freeLookHeld;
        private bool _returningFromFreeLook;
        private readonly FreeLookControlState _freeLookControl = new FreeLookControlState();
        private float _nextInstructorAttempt;
        private bool _flightInputsAttempted;
        private void Update()
        {
            _hotkeys.Update();
            _service.Update();
            if (_craftTuning.Update())
            {
                _panel.Cancel();
                ReleaseForPanel();
            }
            _panel.Update(_service.ConditionsSatisfied && FlightInputPatch.IsInstalled);
            _camera.EndPanelDrag();

            if (!_panel.BlockHotkeys && !_panel.Expanded && !_instructor.IsActive && _hotkeys.RollPulsePressed)
            {
                _service.TriggerPulse(ControlOverrideBinder.ChannelRoll, "滚转");
            }

            if (!_panel.BlockHotkeys && !_panel.Expanded && !_instructor.IsActive && _hotkeys.PitchPulsePressed)
            {
                _service.TriggerPulse(ControlOverrideBinder.ChannelPitch, "俯仰");
            }

            if (!_panel.BlockHotkeys && !_panel.Expanded && !_instructor.IsActive && _hotkeys.YawPulsePressed)
            {
                _service.TriggerPulse(ControlOverrideBinder.ChannelYaw, "偏航");
            }

            if (_hotkeys.CameraTogglePressed && !_panel.Editing && !_panel.BlockHotkeys)
            {
                string reason;
                if (_service.ConditionsSatisfied && FlightInputPatch.IsInstalled && _craftTuning.Ready)
                {
                    _camera.Toggle();
                }
                else if (!_camera.IsActive)
                {
                    reason = !FlightInputPatch.IsInstalled ? "飞行输入补丁未成功安装"
                        : (!_craftTuning.Ready ? _craftTuning.Status : _service.ConditionsReason);
                    _log.Warn("接管镜头按键被忽略：" + reason + "。");
                }
                else
                {
                    _camera.Deactivate("条件不再满足");
                }
            }

            if (_hotkeys.CameraYawPressed && !_hotkeys.FreeLookHeld && !_panel.BlockHotkeys)
            {
                _camera.StepYaw();
            }

            // Wait until the game's input objects and flight context are initialized.
            if (!_flightInputsAttempted && _service.ConditionsSatisfied)
            {
                _flightInputsAttempted = true;
                FlightInputPatch.Install(_log, delegate { return _camera.IsActive; });
            }

            if (_camera.IsActive && !_service.ConditionsSatisfied)
            {
                _camera.Deactivate(_service.ConditionsReason);
            }

            // 接管状态变化时开关鼠标（锁定并隐藏光标 / 恢复）。
            if (_camera.IsActive && !_mouse.IsActive)
            {
                _mouse.Begin();
                FlightInputPatch.BeginControl();
                _hud.ResetSession();
                TryStartInstructor();
            }
            else if (!_camera.IsActive && _mouse.IsActive)
            {
                _mouse.End();
                FlightInputPatch.EndControl();
                _instructor.Deactivate("交还镜头");
                _freeLookHeld = false;
                _returningFromFreeLook = false;
                _freeLookControl.Release();
            }

            _frameResolved = false;

            if (_camera.IsActive && _service.ConditionsSatisfied)
            {
                bool freeLook = _hotkeys.FreeLookHeld && Application.isFocused;
                bool resetAimOnRelease = false;
                if (freeLook != _freeLookHeld)
                {
                    if (!freeLook) resetAimOnRelease = _freeLookControl.Release();
                    _freeLookHeld = freeLook;
                    _returningFromFreeLook = !freeLook;
                    if (!_camera.SetFreeLook(freeLook))
                    {
                        FlightInputPatch.EndControl();
                        _mouse.End();
                        _instructor.Deactivate("自由视角镜头切换失败");
                        _freeLookHeld = _returningFromFreeLook = false;
                        _freeLookControl.Release();
                        return;
                    }
                    _mouse.SetFreeLook(freeLook);
                }

                _mouse.Tick();

                Camera aimCamera;
                object aircraft;
                Vector3 aircraftPosition;
                Quaternion aircraftRotation;
                string frameError;
                string poseSource;

                if (GameApi.TryGetMainCamera(out aimCamera, out frameError) && aimCamera != null
                    && GameApi.TryGetLocalAircraft(out aircraft, out frameError)
                    && GameApi.TryGetAircraftPose(aircraft, out aircraftPosition, out aircraftRotation, out poseSource, out frameError))
                {
                    // 本帧的相机与载具姿态只解析一次：教官与准星 HUD 都用这一份。
                    _frameCamera = aimCamera;
                    _frameRotation = aircraftRotation;
                    _frameNose = aircraftRotation * Vector3.forward;
                    _frameUp = aircraftRotation * Vector3.up;
                    _frameRight = aircraftRotation * Vector3.right;
                    _frameResolved = true;

                    if (resetAimOnRelease)
                    {
                        _camera.Aim.Reset(_frameNose);
                        _log.Info("自由视角结束：鼠标飞控目标已固定到当前机头指向。");
                    }
                    if (_freeLookControl.PlayerOnly) _camera.Aim.Reset(_frameNose);

                    Quaternion cameraRotation = aimCamera.transform.rotation;

                    // The camera can look away while the target stays fixed. Do not clamp the
                    // target against that view, including the camera's return after Alt release.
                    if (!freeLook && _returningFromFreeLook
                        && Vector3.Angle(cameraRotation * Vector3.forward, _camera.Aim.Direction) < 5f)
                    {
                        _returningFromFreeLook = false;
                    }
                    if (!freeLook && !_returningFromFreeLook) ApplyScreenLimit(aimCamera);

                    float mouseX, mouseY;
                    if (_mouse.TryRead(out mouseX, out mouseY))
                    {
                        _camera.Aim.ApplyMouse(
                            mouseX,
                            mouseY,
                            cameraRotation,
                            _settings.SensitivityDegreesPerUnit,
                            _settings.MouseDeadzone);
                    }

                    // 鼠标没动也要夹：相机与飞机都在动，限制是相对当前画面算的。
                    if (!freeLook && !_returningFromFreeLook) _camera.Aim.ClampToScreen(cameraRotation);

                    UpdateInstructor(aircraft, Time.deltaTime);
                }
                else
                {
                    _instructor.Deactivate("当前镜头或载具姿态不可读");
                    _camera.Deactivate("当前镜头或载具姿态不可读");
                    _freeLookControl.Release();
                }
            }

            _camera.UpdateAimPose();
            FlightInputPatch.Tick();
            HudVisibilityPatch.ForceVisible = _settings.ForceHudVisible && _camera.IsActive;

            // 手动飞行记录器与被接管无关：正常飞行时也能用。
            _recorder.Update();
            _panel.Sample(_camera.IsActive, _camera.Aim.Direction);
            _camera.RefreshNames();
        }

        private System.Collections.IEnumerator RecordPhysicsSteps()
        {
            WaitForFixedUpdate wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                _recorder.SamplePhysics(_instructor, _camera.IsActive, _camera.Aim.Direction, _freeLookHeld);
            }
        }

        // 把"屏幕矩形"换算成瞄准方向允许的像素偏移。
        // 透视相机下，屏幕上偏离中心的像素数 = (本地.x/本地.z) × (屏幕高度/2 ÷ tan(竖直视场/2))，
        // 与准星绘制用的是同一个换算，保证"夹住的边界"就是"画到的边界"。
        private void ApplyScreenLimit(Camera camera)
        {
            float halfFovRad = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float pixelsPerUnit = (Screen.height * 0.5f) / Mathf.Tan(halfFovRad);
            float margin = Screen.height * _settings.ScreenEdgeMarginRatio;

            float limitX = Mathf.Max(1f, Screen.width * 0.5f - margin);
            float limitY = Mathf.Max(1f, Screen.height * 0.5f - margin);

            _camera.Aim.SetScreenLimit(pixelsPerUnit, limitX, limitY);
        }

        // 教官要的通道输入对象（覆盖注册时需要），与测试脉冲共用同一套读取。
        private void TryStartInstructor()
        {
            _nextInstructorAttempt = Time.unscaledTime + 1f;
            if (!_settings.InstructorEnabled)
            {
                _log.Info("教官按配置关闭：接管后只有镜头与准星，飞机仍由玩家操纵。");
                return;
            }

            if (_service.HasPulseOverride)
            {
                _log.Warn("教官未启动：测试脉冲正占着操纵通道，先等它到时间或交还。");
                return;
            }

            string error;
            object aircraft;
            object controls;
            object rollInput;
            object pitchInput;
            object yawInput;

            if (!GameApi.TryGetLocalAircraft(out aircraft, out error)
                || !GameApi.TryGetAircraftControls(aircraft, out controls, out error)
                || !_service.TryGetChannelInput(ControlOverrideBinder.ChannelRoll, out rollInput, out error)
                || !_service.TryGetChannelInput(ControlOverrideBinder.ChannelPitch, out pitchInput, out error))
            {
                _log.Warn("教官未能启动：" + error + "。飞机仍由玩家操纵。");
                return;
            }

            if (!_service.TryGetChannelInput(ControlOverrideBinder.ChannelYaw, out yawInput, out error))
            {
                _log.Warn("取偏航通道输入失败：" + error + "。方向舵本轮不接管，滚转与俯仰照常。");
                yawInput = null;
            }

            if (!_instructor.TryActivate(controls, rollInput, pitchInput, yawInput, out error))
            {
                _log.Warn("教官未能接管操纵通道：" + error + "。飞机仍由玩家操纵。");
            }
        }

        private void UpdateInstructor(object aircraft, float deltaTime)
        {
            object controls;
            string error;
            if (!GameApi.TryGetAircraftControls(aircraft, out controls, out error))
            {
                _instructor.Deactivate("读不到当前载具操纵对象");
                return;
            }

            if (_instructor.IsActive && !_instructor.IsBoundTo(controls))
            {
                _instructor.Deactivate("当前载具操纵对象变化");
                _freeLookControl.Release();
                _camera.Aim.Reset(_frameNose);
                _nextInstructorAttempt = 0f;
            }
            bool hasOverrides;
            if (_instructor.IsActive
                && GameApi.TryGetHasInputOverrides(controls, out hasOverrides, out error)
                && !hasOverrides)
            {
                _instructor.Deactivate("游戏清除了飞控通道");
                _nextInstructorAttempt = 0f;
            }
            if (!_instructor.IsActive && _settings.InstructorEnabled
                && Time.unscaledTime >= _nextInstructorAttempt) TryStartInstructor();

            if (!_instructor.IsActive)
            {
                return;
            }

            _instructor.SetPlayerOnly(_freeLookControl.PlayerOnly);

            Vector3 velocity;
            Vector3 angularVelocity;
            if (!GameApi.TryGetAircraftMotion(aircraft, out velocity, out angularVelocity, out error))
            {
                _instructor.Deactivate("读不到载具速度与角速度");
                _log.Warn("教官已停止：读不到载具速度与角速度（" + error + "）。");
                return;
            }

            _instructor.Update(
                deltaTime,
                _camera.Aim.Direction,
                _frameRotation,
                _frameNose,
                _frameRight,
                _frameUp,
                velocity,
                angularVelocity);
        }

        // 渲染阶段只用来观察：确认游戏确实按我们的瞄准物体摆了相机。这里不改写任何相机。
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_camera != null)
            {
                _camera.ObserveCamera(camera);
            }
            // Render callback observes the game's final camera pose, after all LateUpdate calls.
            bool playerManeuver;
            if (_camera != null && _camera.IsActive && _frameResolved && camera == _frameCamera
                && _freeLookHeld && Application.isFocused && _service.ConditionsSatisfied
                && !_freeLookControl.PlayerOnly
                && _instructor.TryReadPlayerManeuver(out playerManeuver)
                && _freeLookControl.Observe(true, _camera.Aim.IsOutsideView(
                    camera.transform.rotation, camera.fieldOfView, camera.aspect), playerManeuver))
            {
                _instructor.SetPlayerOnly(true);
                _camera.Aim.Reset(_frameNose);
                _camera.UpdateAimPose();
                _log.Info("自由视角准星在屏外且玩家输入舵量：进入纯玩家三轴操纵，直到松开自由视角键。");
            }
        }

        private void OnGUI()
        {
            DrawAimHud();
            if (!_panel.Expanded) _recorder.DrawStatus();
            _panel.Draw();
        }

        private void ReleaseForPanel()
        {
            _instructor.Deactivate("调参或载具变化");
            _service.CancelPulse();
            _camera.Deactivate("调参或载具变化");
            FlightInputPatch.EndControl();
            _mouse.End();
            _freeLookHeld = _returningFromFreeLook = false;
            _freeLookControl.Release();
            _frameResolved = false;
            _nextInstructorAttempt = 0f;
        }
        private bool ResumeFromPanel()
        {
            if (_camera.IsActive) return true;
            bool paused, allowed; string error;
            if (!_craftTuning.Ready || !_service.ConditionsSatisfied || !FlightInputPatch.IsInstalled
                || !Application.isFocused || !GameApi.TryGetPaused(out paused, out error) || paused
                || !GameApi.TryGetAllowKeyboardInputs(out allowed, out error) || !allowed) return false;
            _camera.Toggle();
            if (!_camera.IsActive) return false;
            _mouse.Begin(); FlightInputPatch.BeginControl(); _hud.ResetSession();
            TryStartInstructor();
            if (_settings.InstructorEnabled && !_instructor.IsActive)
            { ReleaseForPanel(); return false; }
            return true;
        }

        // 只在飞行场景、接管期间绘制；游戏按 F9 隐藏界面时按配置一起隐藏。
        private void DrawAimHud()
        {
            // OnGUI 每帧会被调用多次（Layout、Repaint 等），只在 Repaint 时绘制：
            // 下面的查询要读游戏对象，不能在其他事件里白做几遍。
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_hud == null || !_camera.IsActive)
            {
                return;
            }

            if (!_service.ConditionsSatisfied)
            {
                return;
            }

            if (!_frameResolved || _frameCamera == null)
            {
                return;
            }

            if (_settings.HideHudWithGameUi)
            {
                bool hidden;
                string uiError;
                if (GameApi.TryGetFlightUiHidden(out hidden, out uiError) && hidden)
                {
                    return;
                }
            }

            _hud.Draw(
                _frameCamera,
                _camera.Aim.Direction,
                _frameNose,
                _frameUp);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            HudVisibilityPatch.Uninstall();

            if (_recorder != null)
            {
                _recorder.Stop("插件卸载");
            }

            if (_instructor != null)
            {
                _instructor.Shutdown();
            }

            if (_mouse != null)
            {
                _mouse.End();
            }

            if (_hud != null)
            {
                _hud.Dispose();
            }

            if (_panel != null)
            {
                _panel.Dispose();
            }

            if (_camera != null)
            {
                _camera.Shutdown();
            }

            if (_service != null)
            {
                _service.Shutdown();
            }

            CameraInputPatch.Uninstall();
            FlightInputPatch.Uninstall();
            InstructorOverridePatch.Uninstall();

            if (_log != null)
            {
                _log.Info("插件卸载。");
                _log.Close();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || _camera == null) return;
            if (_panel != null) _panel.Cancel();
            if (_recorder != null) _recorder.Stop("游戏窗口失去焦点");
            _camera.Deactivate("游戏窗口失去焦点");
            FlightInputPatch.EndControl();
            _mouse.End();
            _instructor.Deactivate("游戏窗口失去焦点");
            _freeLookHeld = _returningFromFreeLook = false;
            _freeLookControl.Release();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _freeLookHeld = false;
            _returningFromFreeLook = false;
        }
    }
}
