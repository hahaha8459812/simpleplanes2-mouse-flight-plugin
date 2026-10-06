using System;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal enum FlightState
    {
        Disabled = 0,
        WaitingForAircraft = 1,
        Ready = 2,
        Active = 3
    }

    // 场景与本机载具判定 + 接管状态机。
    //
    // 安全性的来源：一次只注册**一个**通道的覆盖，另外两个通道游戏照常读玩家的键鼠，
    // 所以测试脉冲期间飞机始终可控。理由见 ControlOverrideBinder 顶部注释。
    internal sealed class FlightContextService
    {
        private const float PollInterval = 0.25f;
        private const float PulseDiagInterval = 0.5f;
        private const int PulseDiagMax = 4;

        private readonly PluginSettings _settings;
        private readonly PluginLog _log;
        private readonly ControlOverrideBinder _binder;

        private float _nextPollTime;
        private float _nextHeartbeatTime;

        private bool _enabledInitialized;
        private bool _enabled;

        private bool _inFlightScene;
        private string _sceneDetail = "尚未检测";
        private bool _sceneSignalWarningLogged;

        private object _aircraft;
        private object _controls;
        private string _aircraftDetail = "尚未检测";

        private bool _gatesReadable;
        private bool _isPrimaryLocalPlayer;
        private int _loadContext = -1;
        private bool _paused;
        private bool _allowKeyboardInputs;

        private int _pulseChannel;
        private string _pulseChannelName;
        private float _pulseValue;
        private float _pulseEndTime;
        private float _nextDiagTime;
        private int _diagCount;

        private FlightState _state = FlightState.Disabled;
        private string _stateDetail = "尚未检测";
        private string _lastError;
        private bool _bindingsReported;
        private float _nextBindingsAttemptTime;
        private int _bindingsAttempts;
        private bool _conditionsSatisfied;
        private string _conditionsReason = "尚未检测";

        public FlightContextService(PluginSettings settings, PluginLog log)
        {
            _settings = settings;
            _log = log;
            _binder = new ControlOverrideBinder();
        }




        public int LoadContext
        {
            get { return _loadContext; }
        }

        public bool IsPrimaryLocalPlayer
        {
            get { return _isPrimaryLocalPlayer; }
        }

        public bool Paused
        {
            get { return _paused; }
        }

        public bool AllowKeyboardInputs
        {
            get { return _allowKeyboardInputs; }
        }



        // 接管镜头等长期行为要跟着同一套条件走：条件不满足就必须立刻让位。
        // 测试脉冲是否正占着某个通道。教官启动前要确认，否则两条路径会同时写同一个通道。
        public bool HasPulseOverride
        {
            get { return _binder.IsBound; }
        }

        public bool ConditionsSatisfied
        {
            get { return _conditionsSatisfied; }
        }

        public string ConditionsReason
        {
            get { return _conditionsReason; }
        }

        public void Update()
        {
            EnsureEnabledInitialized();

            if (Time.unscaledTime >= _nextPollTime)
            {
                _nextPollTime = Time.unscaledTime + PollInterval;
                PollScene();
                PollAircraft();
                ReportKeyboardBindingsOnce();
            }

            LogHeartbeatIfDue();

            if (!_enabled)
            {
                StopPulse();
                ReleaseOverrides("总开关关闭");
                SetState(FlightState.Disabled, "插件总开关关闭");
                return;
            }

            string reason;
            if (!ConditionsMet(out reason))
            {
                StopPulse();
                ReleaseOverrides(reason);
                SetState(FlightState.WaitingForAircraft, reason);
                return;
            }

            if (_pulseChannel == 0)
            {
                ReleaseOverrides("没有测试脉冲");
                SetState(FlightState.Ready, "条件满足，未接管（飞机完全由你操作）");
                return;
            }

            if (Time.unscaledTime >= _pulseEndTime)
            {
                string axes = DescribeAxes();
                string channelName = _pulseChannelName;
                StopPulse();
                ReleaseOverrides("测试脉冲到时");
                _log.Info("测试脉冲结束（" + channelName + "）"
                    + (axes.Length == 0 ? "" : "；结束前游戏里的通道值 " + axes) + "。");
                SetState(FlightState.Ready, "条件满足，未接管（飞机完全由你操作）");
                return;
            }

            string bindError;
            if (!EnsureBoundForPulse(out bindError))
            {
                StopPulse();
                ReleaseOverrides("注册覆盖失败");
                SetState(FlightState.WaitingForAircraft, "注册覆盖失败：" + bindError);
                return;
            }

            SetState(FlightState.Active, "测试脉冲进行中：" + PulseDescription());

            if (_diagCount < PulseDiagMax && Time.unscaledTime >= _nextDiagTime)
            {
                _diagCount++;
                _nextDiagTime = Time.unscaledTime + PulseDiagInterval;
                _log.Info("脉冲诊断" + _diagCount + "：" + DescribeInputComposition()
                    + "；游戏内通道值 " + DescribeAxes() + "。");
            }
        }

        public void Shutdown()
        {
            StopPulse();
            ReleaseOverrides("插件卸载");
        }

        public void CancelPulse() { StopPulse(); }

        // 按一下做一次短脉冲：只把该通道固定为设定值，另外两个通道完全由游戏原生处理。
        public void TriggerPulse(int channel, string channelName)
        {
            EnsureEnabledInitialized();

            if (!_enabled)
            {
                _log.Warn("测试脉冲（" + channelName + "）被忽略：General/Enabled 为 false。");
                return;
            }

            string reason;
            if (!ConditionsMet(out reason))
            {
                _log.Warn("测试脉冲（" + channelName + "）被忽略：" + reason + "。");
                return;
            }

            if (_controls == null)
            {
                _log.Warn("测试脉冲（" + channelName + "）被忽略：还没取到本机载具的操纵对象。");
                return;
            }

            object input;
            string error;

            if (!TryGetChannelInput(channel, out input, out error))
            {
                _log.Warn("测试脉冲（" + channelName + "）被忽略：取输入对象失败：" + error + "。");
                return;
            }

            if (_binder.IsBound)
            {
                ReleaseOverrides("重新开始测试脉冲");
            }

            if (!_binder.TryBind(_controls, channel, input, _settings.PulseMagnitude, out error))
            {
                _log.Warn("测试脉冲（" + channelName + "）被忽略：注册输入覆盖失败：" + error + "。");
                return;
            }

            _pulseChannel = channel;
            _pulseChannelName = channelName;
            _pulseValue = _settings.PulseMagnitude;
            _pulseEndTime = Time.unscaledTime + _settings.PulseSeconds;
            _nextDiagTime = Time.unscaledTime + PulseDiagInterval;
            _diagCount = 0;

            _log.Info("测试脉冲开始：" + channelName + " 固定为 " + _pulseValue.ToString("0.00")
                + "，持续 " + _settings.PulseSeconds.ToString("0.0") + " 秒；"
                + "只接管这一个通道，另外两个通道游戏照常读你的键鼠，到点自动交还。");

            SetState(FlightState.Active, "测试脉冲进行中：" + PulseDescription());
        }

        private void EnsureEnabledInitialized()
        {
            if (_enabledInitialized)
            {
                return;
            }

            _enabledInitialized = true;
            _enabled = _settings.Enabled;
        }

        private bool ConditionsMet(out string reason)
        {
            bool satisfied = EvaluateConditions(out reason);
            _conditionsSatisfied = satisfied;
            _conditionsReason = reason;
            return satisfied;
        }

        private bool EvaluateConditions(out string reason)
        {
            if (!_inFlightScene)
            {
                reason = "不在飞行场景（" + _sceneDetail + "）";
                return false;
            }

            if (!GameApi.IsAlive(_aircraft))
            {
                reason = "等待本机载具";
                return false;
            }

            ReadGates();

            if (!_gatesReadable)
            {
                reason = "读取门控失败（" + _lastError + "）";
                return false;
            }

            if (!_isPrimaryLocalPlayer || _loadContext != GameApi.CraftLoadContextFlight)
            {
                reason = "载具上下文不满足（IsPrimaryLocalPlayer=" + _isPrimaryLocalPlayer
                    + ", LoadContext=" + _loadContext + "）";
                return false;
            }

            if (_paused || !_allowKeyboardInputs)
            {
                reason = "暂停或键盘输入被 UI 接管（Paused=" + _paused
                    + ", AllowKeyboardInputs=" + _allowKeyboardInputs + "）";
                return false;
            }

            reason = "条件满足";
            return true;
        }

        private bool EnsureBoundForPulse(out string error)
        {
            if (_binder.IsBound
                && _binder.Channel == _pulseChannel
                && ReferenceEquals(_binder.BoundControls, _controls))
            {
                error = null;
                return true;
            }

            ReleaseOverrides("载具或操纵对象变化");

            object input;
            if (!TryGetChannelInput(_pulseChannel, out input, out error))
            {
                return false;
            }

            return _binder.TryBind(_controls, _pulseChannel, input, _pulseValue, out error);
        }

        // 教官要用同一套通道输入对象（覆盖注册时需要）。
        public bool TryGetChannelInput(int channel, out object input, out string error)
        {
            object gameInputs;

            input = null;
            error = null;

            if (!GameApi.TryGetGameInputs(out gameInputs, out error))
            {
                return false;
            }

            string member;
            if (channel == ControlOverrideBinder.ChannelPitch)
            {
                member = GameApi.PitchMember;
            }
            else if (channel == ControlOverrideBinder.ChannelRoll)
            {
                member = GameApi.RollMember;
            }
            else
            {
                member = GameApi.YawMember;
            }

            return GameApi.TryGetGameInput(gameInputs, member, out input, out error);
        }

        private void ReleaseOverrides(string reason)
        {
            if (!_binder.IsBound)
            {
                return;
            }

            int channel = _binder.Channel;
            string name = ControlOverrideBinder.ChannelName(channel);

            if (_binder.Unbind())
            {
                _log.Info("已交还" + name + "通道（原因：" + reason + "）。");
            }
            else
            {
                _log.Warn("交还" + name + "通道失败（原因：" + reason
                    + "）：游戏侧的覆盖没有被摘除，该通道会一直由插件驱动。");
            }
        }

        private void StopPulse()
        {
            _pulseChannel = 0;
            _pulseChannelName = null;
            _diagCount = PulseDiagMax;
        }

        private string PulseDescription()
        {
            float remaining = _pulseEndTime - Time.unscaledTime;
            if (remaining < 0f)
            {
                remaining = 0f;
            }

            return _pulseChannelName + " 固定为 " + _pulseValue.ToString("0.00")
                + "，还剩 " + remaining.ToString("0.0") + " 秒；另外两个通道由游戏原生处理";
        }

        // 每通道的原始输入轴（键盘/摇杆），用于诊断"玩家的输入到底能不能读到"。
        private string DescribeKeyboardAxes()
        {
            object gameInputs;
            string error;

            if (!GameApi.TryGetGameInputs(out gameInputs, out error))
            {
                return "读不到（" + error + "）";
            }

            object pitchInput;
            object rollInput;
            object yawInput;
            if (!GameApi.TryGetGameInput(gameInputs, GameApi.PitchMember, out pitchInput, out error)
                || !GameApi.TryGetGameInput(gameInputs, GameApi.RollMember, out rollInput, out error)
                || !GameApi.TryGetGameInput(gameInputs, GameApi.YawMember, out yawInput, out error))
            {
                return "读不到（" + error + "）";
            }

            float pitch;
            float roll;
            float yaw;
            if (!GameApi.TryReadInputAxis(pitchInput, out pitch, out error)
                || !GameApi.TryReadInputAxis(rollInput, out roll, out error)
                || !GameApi.TryReadInputAxis(yawInput, out yaw, out error))
            {
                return "读不到（" + error + "）";
            }

            return "P=" + pitch.ToString("0.000")
                + " R=" + roll.ToString("0.000")
                + " Y=" + yaw.ToString("0.000");
        }

        private string DescribeInputComposition()
        {
            string mouseText = "鼠标摇杆=读不到";
            float mouseX;
            float mouseY;
            string error;

            if (_controls != null && GameApi.TryGetMouseAxis(_controls, out mouseX, out mouseY, out error))
            {
                mouseText = "鼠标摇杆=(" + mouseX.ToString("0.00") + ", " + mouseY.ToString("0.00") + ")";
            }

            bool mouseJoystick;
            string mouseJoystickText;
            if (GameApi.TryGetMouseAsJoystickState(out mouseJoystick, out error))
            {
                mouseJoystickText = mouseJoystick ? "鼠标当摇杆=开" : "鼠标当摇杆=关";
            }
            else
            {
                mouseJoystickText = "鼠标当摇杆=读不到";
            }

            return "键盘轴 " + DescribeKeyboardAxes()
                + "　" + mouseText
                + "　" + mouseJoystickText
                + "　委托内读到的本通道输入=" + _binder.LastInputAxis.ToString("0.000");
        }

        private string DescribeAxes()
        {
            if (_controls == null)
            {
                return "";
            }

            float pitch;
            float roll;
            float yaw;
            string error;

            if (!GameApi.TryGetControlsFloat(_controls, GameApi.PitchMember, out pitch, out error)
                || !GameApi.TryGetControlsFloat(_controls, GameApi.RollMember, out roll, out error)
                || !GameApi.TryGetControlsFloat(_controls, GameApi.YawMember, out yaw, out error))
            {
                return "";
            }

            return "P=" + pitch.ToString("0.000")
                + " R=" + roll.ToString("0.000")
                + " Y=" + yaw.ToString("0.000");
        }

        private void ReportKeyboardBindingsOnce()
        {
            if (_bindingsReported || !_settings.LogKeyboardBindings || !_log.IsNormal)
            {
                return;
            }

            // 游戏对象可能还没准备好，隔一段时间重试，并限制总次数，避免一直做反射。
            if (Time.unscaledTime < _nextBindingsAttemptTime || _bindingsAttempts >= 20)
            {
                return;
            }

            _nextBindingsAttemptTime = Time.unscaledTime + 2f;
            _bindingsAttempts++;

            string[] lines;
            string error;

            if (!GameApi.TryGetKeyboardBindings(out lines, out error))
            {
                return;
            }

            _bindingsReported = true;

            _log.Info("游戏自带键盘按键表（" + lines.Length + " 项）：");
            for (int i = 0; i < lines.Length; i++)
            {
                _log.Info("    " + lines[i]);
            }

            _log.Info("插件按键冲突检查：");
            ReportKeyConflict(lines, _settings.PulseRollKey, "滚转脉冲");
            ReportKeyConflict(lines, _settings.PulsePitchKey, "俯仰脉冲");
            ReportKeyConflict(lines, _settings.PulseYawKey, "偏航脉冲");
            ReportKeyConflict(lines, _settings.OverlayKey, "飞控面板展开/应用");
        }

        private void ReportKeyConflict(string[] lines, string keyName, string purpose)
        {
            KeyCode key = KeyUtil.Parse(keyName, KeyCode.None);
            if (key == KeyCode.None)
            {
                if (keyName == null || keyName.Trim().Length == 0 || string.Equals(keyName.Trim(), "None", StringComparison.OrdinalIgnoreCase))
                {
                    _log.Info("    " + purpose + "键未绑定，跳过冲突检查。");
                    return;
                }

                _log.Warn("    " + purpose + "键 \"" + keyName + "\" 不是有效的 KeyCode 名字。");
                return;
            }

            string name = key.ToString();
            System.Collections.Generic.List<string> hits = new System.Collections.Generic.List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hits.Add(lines[i]);
                }
            }

            if (hits.Count == 0)
            {
                _log.Info("    " + purpose + "键 " + name + "：游戏按键表里没有这个键。");
                return;
            }

            _log.Warn("    " + purpose + "键 " + name + " 与游戏自身按键重复：" + string.Join("；", hits.ToArray()));
        }

        private void PollScene()
        {
            bool active;
            string detail;

            if (!GameApi.TryGetFlightSceneActive(out active, out detail))
            {
                _inFlightScene = false;
                _sceneDetail = detail;

                if (!_sceneSignalWarningLogged && _log.IsNormal)
                {
                    _sceneSignalWarningLogged = true;
                    _log.Warn("读不到飞行场景信号（Game.Instance.SceneManager.InFlightScene 与 GameState.IsInLevel 都失败）；"
                        + "插件保持待机。游戏更新可能改过这些成员。");
                }

                return;
            }

            if (active != _inFlightScene)
            {
                _log.Info("飞行场景：" + (_inFlightScene ? "离开" : "进入") + "（" + detail + "）。");
            }

            _inFlightScene = active;
            _sceneDetail = detail;
        }

        private void PollAircraft()
        {
            object aircraft;
            string error;

            if (!_inFlightScene)
            {
                if (_aircraft != null)
                {
                    _aircraft = null;
                    _controls = null;
                    _aircraftDetail = "离开飞行场景";
                    _log.Info("本机载具已清空（离开飞行场景）。");
                }

                return;
            }

            if (!GameApi.TryGetLocalAircraft(out aircraft, out error))
            {
                if (_aircraft != null)
                {
                    _aircraft = null;
                    _controls = null;
                    // 游戏用 FlightUIScript.OnPlayerExitedAircraft 在这里隐藏整套飞行 UI
                    // （仪表盘、航向、底部按钮），所以这条日志要写明后果，免得被当成插件故障。
                    _log.Info("本机载具已消失（损毁或已离开载具）。"
                        + "游戏自带的飞行 UI（航向、空速、底部按钮）在没有本机载具时会被游戏自己隐藏，与本插件无关。");
                }

                _aircraftDetail = error;
                return;
            }

            if (!ReferenceEquals(_aircraft, aircraft))
            {
                if (_binder.IsBound)
                {
                    ReleaseOverrides("本机载具变化");
                }

                _aircraft = aircraft;
                _log.Info("本机载具已就绪：" + DescribeAircraft(aircraft) + "。");
            }

            object controls;
            if (!GameApi.TryGetAircraftControls(aircraft, out controls, out error))
            {
                _controls = null;
                _aircraftDetail = error;
                return;
            }

            _controls = controls;
            _aircraftDetail = "就绪";
        }

        private void ReadGates()
        {
            bool value;
            int number;
            string error;

            _gatesReadable = true;

            if (GameApi.TryGetLoadContext(_aircraft, out number, out error))
            {
                _loadContext = number;
            }
            else
            {
                _loadContext = -1;
                _lastError = error;
                _gatesReadable = false;
            }

            if (GameApi.TryGetIsPrimaryLocalPlayer(_aircraft, out value, out error))
            {
                _isPrimaryLocalPlayer = value;
            }
            else
            {
                _isPrimaryLocalPlayer = false;
                _lastError = error;
                _gatesReadable = false;
            }

            if (GameApi.TryGetPaused(out value, out error))
            {
                _paused = value;
            }
            else
            {
                _paused = true;
                _lastError = error;
                _gatesReadable = false;
            }

            if (GameApi.TryGetAllowKeyboardInputs(out value, out error))
            {
                _allowKeyboardInputs = value;
            }
            else
            {
                _allowKeyboardInputs = false;
                _lastError = error;
                _gatesReadable = false;
            }
        }

        private void LogHeartbeatIfDue()
        {
            if (_state == FlightState.Disabled && !_inFlightScene)
            {
                return;
            }

            if (Time.unscaledTime < _nextHeartbeatTime)
            {
                return;
            }

            _nextHeartbeatTime = Time.unscaledTime + _settings.HeartbeatSeconds;
            _log.Info(BuildStatusLine("心跳"));
        }

        private string BuildStatusLine(string tag)
        {
            string aircraftText = GameApi.IsAlive(_aircraft) ? DescribeAircraft(_aircraft) : "无";
            string boundText = _binder.IsBound ? "已注册" : "未注册";
            string overrideText = "未知";

            if (_controls != null)
            {
                bool hasOverrides;
                string error;
                if (GameApi.TryGetHasInputOverrides(_controls, out hasOverrides, out error))
                {
                    overrideText = hasOverrides ? "true" : "false";
                }
                else
                {
                    overrideText = "读不到";
                }
            }

            string axisText = DescribeAxes();
            if (axisText.Length == 0)
            {
                axisText = "读不到";
            }

            return tag + "：状态=" + StateText(_state)
                + " 飞行=" + _inFlightScene
                + " 载具=" + aircraftText
                + " LoadContext=" + _loadContext
                + " Primary=" + _isPrimaryLocalPlayer
                + " Paused=" + _paused
                + " AllowInput=" + _allowKeyboardInputs
                + " 覆盖=" + boundText
                + " HasOverrides=" + overrideText
                + " 通道值[" + axisText + "]"
                + (_pulseChannel == 0
                    ? " 脉冲=无"
                    : " 脉冲=" + _pulseChannelName + "(" + _pulseValue.ToString("0.00") + ")");
        }

        private void SetState(FlightState state, string detail)
        {
            if (state != _state)
            {
                _log.Info("接管状态：" + StateText(_state) + " → " + StateText(state) + "（" + detail + "）");
                _state = state;
            }
            else if (_log.IsVerbose && detail != _stateDetail)
            {
                _log.Verbose("接管状态详情：" + detail);
            }

            _stateDetail = detail;
        }

        public static string StateText(FlightState state)
        {
            if (state == FlightState.Active)
            {
                return "Active";
            }

            if (state == FlightState.Ready)
            {
                return "Ready";
            }

            return state == FlightState.Disabled ? "Disabled" : "WaitingForAircraft";
        }

        private static string DescribeAircraft(object aircraft)
        {
            UnityEngine.Object unityObject = aircraft as UnityEngine.Object;
            if (unityObject == null)
            {
                return "未知";
            }

            return "ID #" + unityObject.GetInstanceID();
        }

    }
}
