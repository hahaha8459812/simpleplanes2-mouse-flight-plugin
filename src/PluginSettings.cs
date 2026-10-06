using BepInEx.Configuration;

namespace SimplePlanes2MouseFlight
{
    // 配置项的唯一定义处。修改后需要重启游戏；游戏内按键不受此限制。
    internal sealed class PluginSettings
    {
        // 配置迁移：BepInEx 只写不覆盖，旧版本留下的键与旧值会一直留在配置文件里，
        // 造成"改了源码默认值但游戏里没变化"。改动教官参数后把版本号加一，
        // 启动时整理废弃键并迁移本版修改的参数；41 -> 42 保留其他调参。
        private const int CurrentConfigVersion = 46;

        private static readonly string[] ObsoleteInstructorKeys =
        {
            "BrakingShare", "KdPitch", "RollPullCoordination", "PullGateFadeStart", "PullGateFadeEnd",
            "RollPitchCoordination", "AssistTurnRateGain", "FineAngle", "AlignAngle", "AzimuthDeadzone",
            "BankSlewRate", "BankRollOutRate", "AimConeDegrees", "ShapeExponent",
            "PitchTrimGain", "PitchTrimLimit", "PitchTrimLeak", "YawAlignStrength", "CoordPullGain", "CoordPullCap", "CoordPullReleaseAngle",
            "KpRoll", "MaxRateDamp", "GuardRollDegrees", "GuardPitchDegrees", "GuardSeconds",
            "RollRateScale", "PitchRateScale", "MaxBankAngle", "MaxBank"
        };

        private readonly ConfigEntry<bool> _enabled;
        private readonly ConfigEntry<bool> _panelHotkeyEnabled, _flightHotkeyEnabled;
        private readonly ConfigEntry<string> _logLevel;
        private readonly ConfigEntry<float> _heartbeatSeconds;
        private readonly ConfigEntry<float> _pulseSeconds;
        private readonly ConfigEntry<float> _pulseMagnitude;
        private readonly ConfigEntry<string> _pulseRollKey;
        private readonly ConfigEntry<string> _pulsePitchKey;
        private readonly ConfigEntry<string> _pulseYawKey;
        private readonly ConfigEntry<bool> _logKeyboardBindings;
        private readonly ConfigEntry<bool> _overlayEnabled;
        private readonly ConfigEntry<string> _overlayKey;
        private readonly ConfigEntry<string> _cameraTestKey;
        private readonly ConfigEntry<string> _freeLookKey;
        private readonly ConfigEntry<string> _cameraYawStepKey;
        private readonly ConfigEntry<float> _cameraYawStepDegrees;
        private readonly ConfigEntry<float> _cameraDistance;
        private readonly ConfigEntry<float> _heightOffsetRatio;
        private readonly ConfigEntry<float> _turnShiftRatio;
        private readonly ConfigEntry<bool> _cameraApplyEnabled;
        private readonly ConfigEntry<bool> _useGameCameraDistance;
        private readonly ConfigEntry<bool> _forceHudVisible;
        private readonly ConfigEntry<float> _followDampScale;
        private readonly ConfigEntry<bool> _instructorEnabled;
        private readonly ConfigEntry<float> _bankBlendStart;
        private readonly ConfigEntry<float> _bankBlendEnd;
        private readonly ConfigEntry<float> _verticalFadeStart;
        private readonly ConfigEntry<float> _verticalFadeEnd;
        private readonly ConfigEntry<float> _attitudeResponse;
        private readonly ConfigEntry<float> _maxRollRate;
        private readonly ConfigEntry<float> _maxLevelRollRate;
        private readonly ConfigEntry<float> _maxPitchRate;
        private readonly ConfigEntry<float> _yawSign;
        private readonly ConfigEntry<float> _maxYawRate;
        private readonly ConfigEntry<float> _kdYaw;
        private readonly ConfigEntry<float> _yawInTurnShare;
        private readonly ConfigEntry<float> _yawCoordStrength;
        private readonly ConfigEntry<float> _blendSmoothing;
        private readonly ConfigEntry<float> _wingsLevelGain;
        private readonly ConfigEntry<float> _outputSlew;
        private readonly ConfigEntry<float> _rateSmoothing;
        private readonly ConfigEntry<float> _kdRoll;
        private readonly ConfigEntry<float> _pitchBalanceGain, _pitchBalanceLimit, _pitchBalanceSlew;
        private readonly ConfigEntry<float> _yawAimGain, _yawFineGain;
        private readonly ConfigEntry<float> _pitchArrivalRadius, _pitchBrakingLead, _pitchRateTrackingGain, _pitchRateIntegralGain, _pitchAccelerationDamping;
        private readonly ConfigEntry<float> _pitchIntegralReleaseGain, _pitchDemandAcceleration, _rollDemandAcceleration;
        private readonly ConfigEntry<float> _yawBrakingLead, _yawDemandGain;
        private readonly ConfigEntry<float> _rollCaptureAngle, _rollCaptureExitAngle, _rollCaptureRate, _rollCaptureDwell;
        private readonly ConfigEntry<bool> _pitchFastResponseProtection;
        private readonly ConfigEntry<string> _recordKey;
        private readonly ConfigEntry<string> _recordMarkerKey;
        private readonly ConfigEntry<float> _recordIntervalSeconds;
        private readonly ConfigEntry<int> _rollSign;
        private readonly ConfigEntry<int> _pitchSign;
        private readonly ConfigEntry<string> _cameraName;
        private readonly ConfigEntry<float> _sensitivityDegreesPerUnit;
        private readonly ConfigEntry<float> _mouseDeadzone;
        private readonly ConfigEntry<float> _screenEdgeMarginRatio;
        private readonly ConfigEntry<float> _recenterSeconds;
        private readonly ConfigEntry<bool> _useMousePositionDelta;
        private readonly ConfigEntry<bool> _showCrosshair;
        private readonly ConfigEntry<bool> _showNoseMarker;
        private readonly ConfigEntry<bool> _showLinkLine;
        private readonly ConfigEntry<bool> _hideHudWithGameUi;
        private readonly ConfigEntry<float> _crosshairRadiusRatio;
        private readonly ConfigEntry<float> _noseMarkerSizeRatio;
        private readonly ConfigEntry<float> _dashSpacingRatio;
        private readonly ConfigEntry<float> _cameraMaxShift;

        public PluginSettings(ConfigFile config)
        {
            _enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "插件总开关。关闭后测试脉冲按键不再生效，插件什么都不做。\nMaster switch; when off the test pulse keys do nothing.");
            _logLevel = config.Bind(
                "General",
                "LogLevel",
                "Normal",
                "日志级别：Off 完全不输出、Normal 输出加载与状态变化、Verbose 追加诊断细节。\nLog level: Off, Normal or Verbose.");
            _heartbeatSeconds = config.Bind(
                "General",
                "HeartbeatSeconds",
                3f,
                "心跳日志间隔（秒），范围 1 到 60。周期记录状态与三个操纵通道的当前值。\nHeartbeat interval in seconds, 1 to 60.");

            _pulseSeconds = config.Bind(
                "Test",
                "PulseSeconds",
                2f,
                "测试脉冲的持续秒数，范围 0.5 到 10。到时间会自动交还操作。\nTest pulse duration in seconds, 0.5 to 10.");
            _pulseMagnitude = config.Bind(
                "Test",
                "PulseMagnitude",
                0.4f,
                "测试脉冲把该通道固定到的数值，范围 0.1 到 1。\nChannel value used during a test pulse, 0.1 to 1.");
            _pulseRollKey = config.Bind(
                "Test",
                "PulseRollKey",
                "None",
                "游戏内按键：按一下让滚转通道做一次测试脉冲，到时间自动交还。\nIn-game key for a roll test pulse.");
            _pulsePitchKey = config.Bind(
                "Test",
                "PulsePitchKey",
                "None",
                "游戏内按键：按一下让俯仰通道做一次测试脉冲。\nIn-game key for a pitch test pulse.");
            _pulseYawKey = config.Bind(
                "Test",
                "PulseYawKey",
                "None",
                "游戏内按键：按一下让偏航通道做一次测试脉冲。\nIn-game key for a yaw test pulse.");
            _logKeyboardBindings = config.Bind(
                "Test",
                "LogKeyboardBindings",
                true,
                "启动后把游戏自带的键盘按键表写进日志，用于确认插件用的按键没有和游戏冲突。\nLog the game's own keyboard bindings so key conflicts are visible.");
            _overlayEnabled = config.Bind(
                "Overlay",
                "OverlayEnabled",
                true,
                "旧配置，调试浮层已移除。飞行场景显示可折叠飞控参数面板。\nLegacy setting, unused. Flight tuning panel replaces the debug overlay.");
            _overlayKey = config.Bind(
                "Overlay",
                "OverlayKey",
                "KeypadMultiply",
                "游戏内按键：展开或折叠飞控参数面板。折叠不保存、不接管。沿用旧配置键名。\nExpand/collapse flight tuning panel without saving or engaging. Legacy key name retained.");
            _panelHotkeyEnabled = config.Bind("Hotkeys", "PanelEnabled", true, "启用面板热键，禁用时保留绑定。Enable panel hotkey; binding retained when disabled.");
            _flightHotkeyEnabled = config.Bind("Hotkeys", "FlightEnabled", true, "启用鼠标飞控热键，禁用时保留绑定。Enable mouse-flight hotkey; binding retained when disabled.");

            _freeLookKey = config.Bind("Camera", "FreeLookKey", "LeftAlt",
                "旧配置，已不使用。自由视角现读取游戏自定义绑定的下一视角输入，按住进入、松开返回。\nLegacy setting, unused. Free look now holds the game's configured NextView input.");
            _cameraTestKey = config.Bind(
                "Camera",
                "CameraTestKey",
                "Minus",
                "游戏内按键：接管或交还镜头（M1 阶段的技术验证用，不是最终的使用方式）。\nIn-game key to take over or hand back the camera (M1 technical test only).");
            _cameraYawStepKey = config.Bind(
                "Camera",
                "CameraYawStepKey",
                "Equals",
                "游戏内按键：接管镜头期间按一次，把镜头方向绕竖直轴偏航一个固定角度，用来确认插件确实能转动镜头。\nIn-game key to nudge the taken-over camera's yaw.");
            _cameraYawStepDegrees = config.Bind(
                "Camera",
                "CameraYawStepDegrees",
                45f,
                "上面那个按键每次偏航的角度，范围 5 到 90。\nDegrees added per press, 5 to 90.");
            _cameraDistance = config.Bind(
                "Camera",
                "CameraDistance",
                30f,
                "接管镜头后镜头与载具的距离；读得到游戏自己的相机距离时优先用游戏的，这个只是兜底。范围 5 到 200。\nFallback camera distance, 5 to 200.");
            _heightOffsetRatio = config.Bind(
                "Camera",
                "HeightOffsetRatio",
                0.25f,
                "镜头抬高量 = 该值 × 相机距离。抬高后飞机落在屏幕下半部分；填 0 就是镜头与飞机等高。范围 0 到 1。\nCamera height offset as a fraction of the camera distance; puts the aircraft in the lower half of the screen. 0 to 1.");
            _turnShiftRatio = config.Bind(
                "Camera",
                "TurnShiftRatio",
                0.12f,
                "转向感：按飞机实际俯仰和偏航方向平滑偏移镜头，飞机向画面反侧移动；最大量以相机距离为基准。填 0 关闭，范围 0 到 0.6。\nSmooth camera offset toward actual aircraft turns, including pitch. Maximum as a fraction of camera distance; 0 disables, 0 to 0.6.");
            _cameraApplyEnabled = config.Bind(
                "Camera",
                "CameraApplyEnabled",
                true,
                "是否真的改写相机姿态。false 时只把算出来的姿态写进日志、不动相机，用于排查。\nWhen false the takeover only logs the computed pose and does not move the camera.");
            _useGameCameraDistance = config.Bind(
                "Camera",
                "UseGameCameraDistance",
                true,
                "是否使用游戏自己的相机距离。游戏的距离会随载具大小自适应，通常比固定值可靠。\nUse the game's own (adaptive) camera distance.");
            _cameraMaxShift = config.Bind(
                "Camera",
                "CameraMaxShift",
                1000f,
                "安全网：算出来的镜头位置离游戏当前镜头超过这个距离（米）就放弃接管，避免把画面扔到几千米之外。范围 50 到 100000。\nRefuse to move the camera further than this from where the game put it, in metres.");
            _cameraName = config.Bind(
                "Camera",
                "CameraName",
                "Flight Control View",
                "飞控专属视角显示的名字。切换过去时游戏弹出的提示会用它，游戏记进设置里的视角名也会用它。名字不会经过汉化，写什么就显示什么。\nDisplay name of the flight-control camera. Shown in the view-change message and stored as the remembered view name. Not translated by localisation plugins.");
            _followDampScale = config.Bind(
                "Camera",
                "FollowDampScale",
                1.0f,
                "镜头跟随瞄准方向的快慢：游戏自己的阻尼值是 1.5（时间常数约 0.7 秒），这里乘一个系数。小于 1 更慢更柔，大于 1 更跟手。范围 0.1 到 3。\nMultiplier on the game's camera follow damping (1.5, about 0.7 s time constant). Below 1 is slower and softer, above 1 is snappier. 0.1 to 3.");
            _instructorEnabled = config.Bind(
                "Instructor",
                "Enabled",
                true,
                "教官总开关。关掉后飞机不会去追准星，退化成 M2 的行为（只有镜头与准星）。\nInstructor master switch. Off leaves M2 behaviour (camera and crosshair only).");
            _bankBlendStart = config.Bind("Instructor", "BankBlendStart", 0.3f,
                "机翼回平与朝目标朝目标滚转混合的起始偏移角（度）：偏移小于它基本按机翼回平，大于 BankBlendEnd 完全朝目标朝目标滚转。范围 0.2 到 90。\nOff-nose angle below which the roll is wings-level; fully banked toward the aim above BankBlendEnd. 0.2 to 90.");
            _bankBlendEnd = config.Bind("Instructor", "BankBlendEnd", 2f,
                "完全朝目标朝目标滚转的偏移角（度）。必须大于 BankBlendStart。范围 0.5 到 120，且至少比起始值大 1 度。\nOff-nose angle at which the roll is fully banked toward the aim. 0.5 to 120.");
            _verticalFadeStart = config.Bind("Instructor", "VerticalFadeStart", 10f,
                "目标偏离机体正上/正下多少度以内开始让\"机翼回平\"渐隐（度）：越接近正上/正下，回平方向越没有意义，此时滚转保持不动。范围 2 到 45。\nAngle from straight up/down at which the wings-level term starts fading out. 2 to 45.");
            _verticalFadeEnd = config.Bind("Instructor", "VerticalFadeEnd", 30f,
                "目标偏离正上/正下超过这个角度时\"机翼回平\"完全生效（度），必须大于 VerticalFadeStart。范围 5 到 90。\nAngle from straight up/down beyond which the wings-level term is fully active. 5 to 90.");
            _attitudeResponse = config.Bind("Instructor", "AttitudeResponse", 0.35f,
                "姿态环时间常数（秒）：角度误差每这么多秒转成一份目标角速度。范围 0.05 到 2。\nAttitude loop time constant in seconds. 0.05 to 2.");
            _maxRollRate = config.Bind("Instructor", "MaxRollRate", 120f,
                "朝目标滚转的目标转速上限（度/秒），0 关闭，默认 120。范围 0 到 400。\nBanking target-rate limit, deg/s; 0 disables, default 120, range 0 to 400.");
            _maxLevelRollRate = config.Bind("Instructor", "MaxLevelRollRate", 45f,
                "回平目标转速上限（度/秒），0 关闭，默认 45。范围 0 到 200。\nLevelling target-rate limit, deg/s; 0 disables, default 45, range 0 to 200.");
            _yawSign = config.Bind("Instructor", "YawSign", 1f,
                "偏航通道符号（1 或 -1）。默认 1 来自 2026-10-05 方向舵实测记录（Q 为负、E 为正，机体上方向转速 ±14~15 度/秒）：游戏 Yaw 轴正值 = 机头向右，与滚转轴同向。若方向舵反着动就改成 -1。\nYaw axis sign. Default 1 measured from a rudder recording: positive game Yaw turns the nose right. Use -1 if the rudder moves backwards.");
            _maxYawRate = config.Bind("Instructor", "MaxYawRate", 20f,
                "偏航目标转速上限（度/秒），0 关闭，默认 20。范围 0 到 60。\nYaw target-rate limit, deg/s; 0 disables, default 20, range 0 to 60.");
            _kdYaw = config.Bind("Instructor", "KdYaw", 0.3f,
                "偏航通道阻尼系数，与滚转俯仰同一量纲。范围 0 到 0.5。\nYaw damping gain. 0 to 0.5.");
            _yawInTurnShare = config.Bind("Instructor", "YawInTurnShare", 0.75f,
                "大转弯时方向舵保留的份额：0 = 完全让位给滚转，1 = 一直全力参与。范围 0 到 1。\nRudder share kept while banking hard into a turn. 0 to 1.");
            _yawCoordStrength = config.Bind("Instructor", "YawCoordStrength", 0.6f,
                "方向舵协调项增益，基于机体系速度横向角的变化，范围 0 到 1.5。不是世界航迹转速或直接侧滑反馈。\nCoordination gain on body-relative velocity azimuth derivative, 0 to 1.5.");
            _wingsLevelGain = config.Bind("Instructor", "WingsLevelGain", 1.33f,
                "机翼回平力度倍数：1 表示机身滚转 90 度给满杆，1.33 表示约 68 度就给满杆（力度大三分之一）。范围 0.5 到 3。\nWings-level strength multiplier. 1 means a 90 degree roll gives full stick. 0.5 to 3.");
            _blendSmoothing = config.Bind("Instructor", "BlendSmoothing", 0.15f,
                "滚转两种模式（机翼回平 / 朝目标滚转）切换的平滑时间（秒）：把切换的台阶磨掉，减轻抽搐。0 表示不平滑。范围 0 到 0.5。\nSmoothing applied to the roll mode blend to remove the switching step. 0 to 0.5.");
            _maxPitchRate = config.Bind("Instructor", "MaxPitchRate", 0f,
                "俯仰目标转速上限（度/秒），0 关闭限制，默认关闭。范围 0 到 150。\nPitch target-rate limit, deg/s. 0 disables the limit (default); 0 to 150.");
            _outputSlew = config.Bind("Instructor", "OutputSlew", 6f,
                "三轴输出的变化率上限（杆量/秒）：防抽杆与鼠标抖动。范围 0.5 到 30。\nOutput slew limit in stick units per second. 0.5 to 30.");
            _rateSmoothing = config.Bind("Instructor", "RateSmoothing", 0.06f,
                "角速度低通时间常数（秒）：阻尼项只用低通后的角速度。逐帧差分的角速度有延迟，动压大时会从阻尼翻转成驱动，形成高频抖动。0 表示不低通。范围 0 到 0.3。\nLow-pass time constant on the rate feeding the damping term (anti high-speed PIO). 0 to 0.3.");
            _kdRoll = config.Bind("Instructor", "KdRoll", 0.15f,
                "滚转通道阻尼系数：每 1 度/秒滚转率抵消多少杆量。给的是与机型无关的均衡值：比例系数已按滚转角度上限归一化，这里只决定响应快慢与超调（0.006 约对应 0.3 秒）。范围 0 到 0.3。\nRoll damping gain. A balanced value that is not tuned to one aircraft. 0 to 0.3.");
            _pitchArrivalRadius = config.Bind("Instructor", "PitchArrivalRadius", 2f,
                "俯仰收尾区半径（度），默认 2 表示整个区域约 4 度宽。范围 0.5 到 5。\nNominal angular arrival radius, degrees.");
            _pitchBrakingLead = config.Bind("Instructor", "PitchBrakingLead", 0.15f,
                "根据实测转速预估剩余误差的时间（秒）。快速转动时可能在名义收尾区外提前制动。范围 0 到 0.4。\nMeasured-rate braking prediction horizon, seconds.");
            _pitchRateTrackingGain = config.Bind("Instructor", "PitchRateTrackingGain", 0.5f,
                "实际俯仰转速误差的比例增益，范围 0.1 到 2。\nProportional gain on normalized measured pitch-rate error.");
            _pitchRateIntegralGain = config.Bind("Instructor", "PitchRateIntegralGain", 0.3f,
                "俯仰转速误差的积分增益，保留维持转动所需杆量。范围 0 到 1，带输出跟踪和饱和限制。\nPitch-rate integral gain with output tracking and saturation limits.");
            _pitchAccelerationDamping = config.Bind("Instructor", "PitchAccelerationDamping", 0.05f,
                "俯仰角加速度阻尼系数（秒），范围 0 到 0.2。作用于滤波后的实测转速差分。\nDamping coefficient on filtered angular acceleration, seconds.");
            _pitchIntegralReleaseGain = config.Bind("Instructor", "PitchIntegralReleaseGain", 2f,
                "残留俯仰杆量妨碍明显制动时的撤去增益，范围 0 到 5；不会直接清除稳定飞行所需杆量。\nIntegral unwind gain during substantial braking, 0 to 5.");
            _pitchDemandAcceleration = config.Bind("Instructor", "PitchDemandAcceleration", 120f,
                "俯仰目标转速增加时的变化率，度/秒²；减少或反向要求即时生效。0 关闭，范围 0 到 1000。\nPitch demand buildup rate; deceleration/reversal bypass this profile.");
            _rollDemandAcceleration = config.Bind("Instructor", "RollDemandAcceleration", 600f,
                "滚转目标转速增加时的变化率，度/秒²；减少或反向要求即时生效。0 关闭，范围 0 到 2000。\nRoll demand buildup rate; deceleration/reversal bypass this profile.");
            _rollCaptureAngle = config.Bind("Instructor", "RollCaptureAngle", 0.8f,
                "接近准星并减速后允许进入回平的总偏差（度），范围 0.2 到 2。\nMaximum pointing error for wings-level capture, degrees.");
            _rollCaptureExitAngle = config.Bind("Instructor", "RollCaptureExitAngle", 3f,
                "回平状态退出的总偏差（度），至少比进入角大 0.5 度；避免小过冲反复切换。\nPointing error for leaving wings-level capture.");
            _rollCaptureRate = config.Bind("Instructor", "RollCaptureRate", 6f,
                "允许进入回平的俯仰与偏航合成实测转速，度/秒，范围 1 到 20。不是滚转转速上限。\nMaximum pitch/yaw pointing rate for capture, deg/s.");
            _rollCaptureDwell = config.Bind("Instructor", "RollCaptureDwell", 0.08f,
                "进入回平条件需连续满足的时间，秒，范围 0 到 0.5。\nContinuous capture qualification time, seconds.");
            _pitchFastResponseProtection = config.Bind("Instructor", "PitchFastResponseProtection", true,
                "检测到操纵回读与快速俯仰响应后，本次接管降低转速反馈及加速度阻尼力度。\nReduce feedback sensitivity after evidence of rapid pitch response during this engagement.");
            _pitchBalanceGain = config.Bind("Instructor", "PitchBalanceGain", 0.035f,
                "接近准星并稳定时，俯仰平衡补偿的学习增益（杆量/度/秒）。0 关闭。范围 0 到 0.1。\nConditional pitch balance gain, stick/degree/second; zero disables it.");
            _pitchBalanceLimit = config.Bind("Instructor", "PitchBalanceLimit", 0.25f,
                "俯仰平衡补偿绝对值上限（杆量），范围 0 到 0.4。不是游戏的 Trim 通道。\nMaximum equilibrium stick magnitude; not the native Trim channel.");
            _pitchBalanceSlew = config.Bind("Instructor", "PitchBalanceSlew", 0.025f,
                "俯仰平衡补偿每秒最多变化多少杆量，范围 0.001 到 0.1。\nMaximum balance learning rate, stick units/second.");
            _yawAimGain = config.Bind("Instructor", "YawAimGain", 1.6f,
                "大偏差时方向舵指向修正增益，范围 0.5 到 2。只增强瞄准误差项，协调项另行计算。\nRudder aiming gain outside the fine region.");
            _yawFineGain = config.Bind("Instructor", "YawFineGain", 2f,
                "接近准星时方向舵指向修正增益，范围 0.5 到 2.5；从 0.3 到 6 度平滑过渡到 YawAimGain。\nRudder fine aiming gain, smoothly transitioning from 0.3 to 6 degrees.");
            _yawBrakingLead = config.Bind("Instructor", "YawBrakingLead", 0.05f,
                "方向舵根据实测偏航转速提前制动的时间（秒），范围 0 到 0.4。\nMeasured yaw-rate braking prediction horizon, seconds.");
            _yawDemandGain = config.Bind("Instructor", "YawDemandGain", 0.8f,
                "偏航目标转速换算杆量的系数，范围 0.2 到 1.5；配合更积极的瞄准增益使用。\nStick gain on nominal yaw-rate demand, 0.2 to 1.5.");
            _recordKey = config.Bind(
                "Test",
                "RecordKey",
                "Slash",
                "诊断记录键：按一下开始物理采样记录，再按一下结束。手动飞行和飞控接管均可记录。\nToggle physics-step diagnostic recording, in manual or assisted flight.");
            _recordMarkerKey = config.Bind("Test", "RecordMarkerKey", "Backslash",
                "记录期间按此键标记下一段测试，编号显示在记录状态中。\nMark the next test segment while recording.");
            _recordIntervalSeconds = config.Bind(
                "Test",
                "RecordIntervalSeconds",
                0.02f,
                "记录间隔（秒）。范围 0.02 到 1。\nRecorder sample interval in seconds. 0.02 to 1.");
            _rollSign = config.Bind("Instructor", "RollSign", 1,
                "滚转通道符号（1 或 -1）。默认 1 来自 2026-10-05 手动飞行记录实测：游戏 Roll 轴正值就是右滚。实机若发现朝目标滚转方向反了，改成 -1。\nRoll channel sign, 1 or -1. Flip it if the aircraft banks the wrong way.");
            _pitchSign = config.Bind("Instructor", "PitchSign", -1,
                "俯仰通道符号（1 或 -1）。默认 -1 来自实测：游戏 Pitch 轴正值是低头，而本插件的需求正值表示抬头。\nPitch channel sign, 1 or -1. Flip it if the nose goes the wrong way.");
            _forceHudVisible = config.Bind(
                "Camera",
                "ForceHudVisible",
                true,
                "接管飞控期间是否强制显示游戏自带的航炮准星与导弹锁定环。游戏原本的规则是相机朝向与目标朝向夹角超过约 26 度就隐藏，飞控接管后很容易触发，所以默认强制显示。\nKeep the game's gun reticle and missile lock ring visible while the flight control is active.");
            _sensitivityDegreesPerUnit = config.Bind(
                "Mouse",
                "SensitivityDegreesPerUnit",
                0.67f,
                "鼠标每单位增量转动多少度。单位与像素、屏幕分辨率无关（方案 5.4），换分辨率手感不变；觉得太快或太慢就调这里。范围 0.05 到 20。\nDegrees of aim rotation per mouse unit. Independent of resolution; tune to taste. 0.05 to 20.");
            _mouseDeadzone = config.Bind(
                "Mouse",
                "Deadzone",
                0.05f,
                "鼠标增量的绝对值小于它就当作 0，用来抑制手抖。范围 0 到 0.5。\nMouse delta below this is treated as zero. 0 to 0.5.");
            _screenEdgeMarginRatio = config.Bind(
                "Mouse",
                "ScreenEdgeMarginRatio",
                0f,
                "瞄准方向在屏幕上的活动范围：0 表示飞控准星中心可以一直拉到屏幕边缘（战雷就是这样），大于 0 会在四周留出这么多比例的空隙（例如 0.02 表示留 2% 屏幕高度的边距），让准星圆环整个留在画面里。范围 0 到 0.3。\nHow close the crosshair may get to the screen edge. 0 lets its centre reach the very edge; a larger value keeps a margin so the whole ring stays visible. 0 to 0.3.");
            _recenterSeconds = config.Bind(
                "Mouse",
                "RecenterSeconds",
                0f,
                "大于 0 时，瞄准方向按这个时间常数缓慢转回机头（辅助项，战雷默认没有）。0 表示关闭。范围 0 到 10。\nAim slowly returns to the nose with this time constant when above 0. 0 to 10.");
            _useMousePositionDelta = config.Bind(
                "Mouse",
                "UseMousePositionDelta",
                false,
                "鼠标轴读数若恒为 0（被游戏占用），把这个改成 true 改用光标位置差分。日志里的鼠标读数会写明实际走的哪条路。\nFall back to cursor-position deltas when the mouse axes read zero. See the mouse sample line in the log.");
            _showCrosshair = config.Bind(
                "Hud",
                "ShowCrosshair",
                true,
                "是否绘制飞控准星（大环）。\nDraw the aim crosshair (large ring).");
            _showNoseMarker = config.Bind(
                "Hud",
                "ShowNoseMarker",
                true,
                "是否绘制机头指向标记（小十字，横臂较长，跟着机身滚转）。\nDraw the nose marker (small cross with a longer horizontal arm, rolling with the craft).");
            _showLinkLine = config.Bind(
                "Hud",
                "ShowLinkLine",
                true,
                "是否绘制飞控准星到屏幕中心的虚线。\nDraw the dashed line from the crosshair to the screen centre.");
            _hideHudWithGameUi = config.Bind(
                "Hud",
                "HideWithGameUi",
                true,
                "游戏按 F9 隐藏界面时，本插件的准星是否也一起隐藏。\nAlso hide the plugin HUD when the game hides its own UI (F9).");
            _crosshairRadiusRatio = config.Bind(
                "Hud",
                "CrosshairRadiusRatio",
                0.014f,
                "飞控准星大环的半径 = 该值 × 屏幕高度。范围 0.004 到 0.1。\nCrosshair ring radius as a fraction of screen height. 0.004 to 0.1.");
            _noseMarkerSizeRatio = config.Bind(
                "Hud",
                "NoseMarkerSizeRatio",
                0.010f,
                "机头指向小十字的尺寸 = 该值 × 屏幕高度。范围 0.003 到 0.08。\nNose marker size as a fraction of screen height. 0.003 to 0.08.");
            _dashSpacingRatio = config.Bind(
                "Hud",
                "DashSpacingRatio",
                0.008f,
                "虚线的划+空总长 = 该值 × 屏幕高度。范围 0.002 到 0.05。\nDash plus gap length as a fraction of screen height. 0.002 to 0.05.");
            ConfigMigrationNote = Migrate(config);
        }

        private string Migrate(ConfigFile config)
        {
            int removed = 0;
            for (int i = 0; i < ObsoleteInstructorKeys.Length; i++)
            {
                if (config.Remove(new ConfigDefinition("Instructor", ObsoleteInstructorKeys[i])))
                {
                    removed++;
                }
            }

            ConfigEntry<int> version = config.Bind("General", "ConfigVersion", CurrentConfigVersion,
                "配置版本号，由插件维护；版本变化时整理废弃键并迁移本版修改的参数。不要手动修改。\nConfig schema version, maintained by the plugin. Do not edit.");

            int previousVersion = version.Value;
            bool reset = previousVersion < CurrentConfigVersion;
            if (reset)
            {
                if (previousVersion < 41) ResetInstructorToCurrentDefaults();
                else if (previousVersion < 42)
                {
                    // Stage two changes only these existing tuning values; keep other user settings.
                    _bankBlendStart.Value = 0.3f; _bankBlendEnd.Value = 2f;
                    _kdYaw.Value = 0.3f; _yawInTurnShare.Value = 0.5f;
                }
                // Update the prior shipped yaw defaults while preserving custom values.
                if (previousVersion >= 41 && previousVersion < 43)
                {
                    if (UnityEngine.Mathf.Abs(_yawInTurnShare.Value - 0.5f) < 0.00001f) _yawInTurnShare.Value = 0.75f;
                    if (UnityEngine.Mathf.Abs(_yawAimGain.Value - 1.15f) < 0.00001f) _yawAimGain.Value = 1.6f;
                    if (UnityEngine.Mathf.Abs(_yawFineGain.Value - 1.6f) < 0.00001f) _yawFineGain.Value = 2f;
                }
                if (previousVersion < 45 && _maxPitchRate.Value == 30f) _maxPitchRate.Value = 0f;
                version.Value = CurrentConfigVersion;
            }

            if (removed == 0 && !reset)
            {
                return null;
            }

            try
            {
                config.Save();
            }
            catch (System.Exception ex)
            {
                return "配置已整理，但保存失败：" + ex.GetType().Name + " " + ex.Message
                    + "（本次仍按新参数运行，下次启动会再试）";
            }

            return "配置已整理：删除废弃键 " + removed.ToString() + " 个"
                + (reset ? "，配置版本及本版默认参数已迁移，其他自定义值保留" : "") + "。";
        }

        public bool Enabled
        {
            get { return _enabled.Value; }
        }

        public string ConfigMigrationNote { get; private set; }

        private System.Collections.Generic.Dictionary<string, float> _tuning;
        public void SetFlightTuning(System.Collections.Generic.IDictionary<string, float> values)
        {
            string error;
            if (values != null && !FlightTuning.Validate(values, out error))
                throw new System.ArgumentException(error);
            _tuning = values == null ? null : FlightTuning.Copy(values);
        }
        public System.Collections.Generic.Dictionary<string, float> ReadConfiguredTuning()
        {
            System.Collections.Generic.Dictionary<string, float> saved = _tuning;
            try { _tuning = null; return FlightTuning.Read(this); }
            finally { _tuning = saved; }
        }
        private float TuningNumber(string name, float configured)
        {
            float value;
            return _tuning != null && _tuning.TryGetValue(name, out value) ? value : configured;
        }
        private bool TuningBool(string name, bool configured)
        { return TuningNumber(name, configured ? 1f : 0f) != 0f; }

        // 启动时把"配置里写的原值"与"实际生效值"逐项对比：超范围被钳掉的键会打印警告。
        // 目的：杜绝"改了配置却静默失效"——要么生效，要么在日志里明说没生效。
        public string DescribeEffectiveConfig()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            AppendEffective(builder, "BankBlendStart", _bankBlendStart.Value, BankBlendStart);
            AppendEffective(builder, "BankBlendEnd", _bankBlendEnd.Value, BankBlendEnd);
            AppendEffective(builder, "VerticalFadeStart", _verticalFadeStart.Value, VerticalFadeStart);
            AppendEffective(builder, "VerticalFadeEnd", _verticalFadeEnd.Value, VerticalFadeEnd);
            AppendEffective(builder, "AttitudeResponse", _attitudeResponse.Value, AttitudeResponse);
            AppendEffective(builder, "MaxRollRate", _maxRollRate.Value, MaxRollRate);
            AppendEffective(builder, "MaxLevelRollRate", _maxLevelRollRate.Value, MaxLevelRollRate);
            AppendEffective(builder, "YawSign", _yawSign.Value, YawSign);
            AppendEffective(builder, "MaxYawRate", _maxYawRate.Value, MaxYawRate);
            AppendEffective(builder, "KdYaw", _kdYaw.Value, KdYaw);
            AppendEffective(builder, "YawInTurnShare", _yawInTurnShare.Value, YawInTurnShare);
            AppendEffective(builder, "YawCoordStrength", _yawCoordStrength.Value, YawCoordStrength);
            AppendEffective(builder, "WingsLevelGain", _wingsLevelGain.Value, WingsLevelGain);
            AppendEffective(builder, "BlendSmoothing", _blendSmoothing.Value, BlendSmoothing);
            AppendEffective(builder, "MaxPitchRate", _maxPitchRate.Value, MaxPitchRate);
            AppendEffective(builder, "OutputSlew", _outputSlew.Value, OutputSlew);
            AppendEffective(builder, "RateSmoothing", _rateSmoothing.Value, RateSmoothing);
            AppendEffective(builder, "KdRoll", _kdRoll.Value, KdRoll);
            AppendEffective(builder, "RollSign", (float)_rollSign.Value, RollSign);
            AppendEffective(builder, "PitchSign", (float)_pitchSign.Value, PitchSign);
            AppendEffective(builder, "PitchBalanceGain", _pitchBalanceGain.Value, PitchBalanceGain);
            AppendEffective(builder, "PitchBalanceLimit", _pitchBalanceLimit.Value, PitchBalanceLimit);
            AppendEffective(builder, "PitchBalanceSlew", _pitchBalanceSlew.Value, PitchBalanceSlew);
            AppendEffective(builder, "YawAimGain", _yawAimGain.Value, YawAimGain);
            AppendEffective(builder, "YawFineGain", _yawFineGain.Value, YawFineGain);
            AppendEffective(builder, "PitchArrivalRadius", _pitchArrivalRadius.Value, PitchArrivalRadius);
            AppendEffective(builder, "PitchBrakingLead", _pitchBrakingLead.Value, PitchBrakingLead);
            AppendEffective(builder, "PitchRateTrackingGain", _pitchRateTrackingGain.Value, PitchRateTrackingGain);
            AppendEffective(builder, "PitchAccelerationDamping", _pitchAccelerationDamping.Value, PitchAccelerationDamping);
            AppendEffective(builder, "PitchRateIntegralGain", _pitchRateIntegralGain.Value, PitchRateIntegralGain);
            AppendEffective(builder, "PitchIntegralReleaseGain", _pitchIntegralReleaseGain.Value, PitchIntegralReleaseGain);
            AppendEffective(builder, "PitchDemandAcceleration", _pitchDemandAcceleration.Value, PitchDemandAcceleration);
            AppendEffective(builder, "RollDemandAcceleration", _rollDemandAcceleration.Value, RollDemandAcceleration);
            AppendEffective(builder, "YawBrakingLead", _yawBrakingLead.Value, YawBrakingLead);
            AppendEffective(builder, "YawDemandGain", _yawDemandGain.Value, YawDemandGain);
            AppendEffective(builder, "RollCaptureAngle", _rollCaptureAngle.Value, RollCaptureAngle);
            AppendEffective(builder, "RollCaptureExitAngle", _rollCaptureExitAngle.Value, RollCaptureExitAngle);
            AppendEffective(builder, "RollCaptureRate", _rollCaptureRate.Value, RollCaptureRate);
            AppendEffective(builder, "RollCaptureDwell", _rollCaptureDwell.Value, RollCaptureDwell);
            builder.Append("PitchFastResponseProtection=").Append(PitchFastResponseProtection).Append("  ");
            return builder.ToString();
        }

        private void AppendEffective(System.Text.StringBuilder builder, string key, float raw, float effective)
        {
            builder.Append(key).Append('=').Append(effective.ToString("0.###"));

            if (_tuning == null && System.Math.Abs(raw - effective) > 0.000001f)
            {
                builder.Append("（配置写的是 ").Append(raw.ToString("0.###")).Append("，超出允许范围，已按前一个值使用）");
            }

            builder.Append("  ");
        }

        // Older schemas need the baseline defaults; stage two migrates only the changed values above.
        private void ResetInstructorToCurrentDefaults()
        {
            _instructorEnabled.Value = true;
            _bankBlendStart.Value = 0.3f;
            _bankBlendEnd.Value = 2f;
            _verticalFadeStart.Value = 10f;
            _verticalFadeEnd.Value = 30f;
            _attitudeResponse.Value = 0.35f;
            _maxRollRate.Value = 120f;
            _maxLevelRollRate.Value = 45f;
            _yawSign.Value = 1f;
            _maxYawRate.Value = 20f;
            _kdYaw.Value = 0.3f;
            _yawInTurnShare.Value = 0.75f;
            _yawCoordStrength.Value = 0.6f;
            _wingsLevelGain.Value = 1.33f;
            _blendSmoothing.Value = 0.15f;
            _maxPitchRate.Value = 30f;
            _outputSlew.Value = 6f;
            _rateSmoothing.Value = 0.06f;
            _kdRoll.Value = 0.15f;
            _rollSign.Value = 1;
            _pitchSign.Value = -1;
        }


        public string LogLevel
        {
            get { return _logLevel.Value; }
        }

        public float HeartbeatSeconds
        {
            get
            {
                float value = _heartbeatSeconds.Value;
                if (value < 1f)
                {
                    return 1f;
                }

                return value > 60f ? 60f : value;
            }
        }

        public float PulseSeconds
        {
            get
            {
                float value = _pulseSeconds.Value;
                if (value < 0.5f)
                {
                    return 0.5f;
                }

                return value > 10f ? 10f : value;
            }
        }

        public float PulseMagnitude
        {
            get
            {
                float value = _pulseMagnitude.Value;
                if (value < 0.1f)
                {
                    return 0.1f;
                }

                return value > 1f ? 1f : value;
            }
        }

        public string PulseRollKey
        {
            get { return _pulseRollKey.Value; }
        }

        public string PulsePitchKey
        {
            get { return _pulsePitchKey.Value; }
        }

        public string PulseYawKey
        {
            get { return _pulseYawKey.Value; }
        }

        public bool LogKeyboardBindings
        {
            get { return _logKeyboardBindings.Value; }
        }

        public bool OverlayEnabled
        {
            get { return _overlayEnabled.Value; }
        }

        public string OverlayKey
        {
            get { return _overlayKey.Value; }
            set { _overlayKey.Value = value; }
        }
        public bool PanelHotkeyEnabled { get { return _panelHotkeyEnabled.Value; } set { _panelHotkeyEnabled.Value = value; } }
        public bool FlightHotkeyEnabled { get { return _flightHotkeyEnabled.Value; } set { _flightHotkeyEnabled.Value = value; } }

        public string CameraTestKey
        {
            get { return _cameraTestKey.Value; }
            set { _cameraTestKey.Value = value; }
        }

        public string FreeLookKey
        {
            get { return _freeLookKey.Value; }
        }

        public string CameraYawStepKey
        {
            get { return _cameraYawStepKey.Value; }
        }

        public float CameraYawStepDegrees
        {
            get
            {
                float value = _cameraYawStepDegrees.Value;
                if (value < 5f)
                {
                    return 5f;
                }

                return value > 90f ? 90f : value;
            }
        }

        public float CameraDistance
        {
            get
            {
                float value = _cameraDistance.Value;
                if (value < 5f)
                {
                    return 5f;
                }

                return value > 200f ? 200f : value;
            }
        }

        public bool CameraApplyEnabled
        {
            get { return _cameraApplyEnabled.Value; }
        }

        public bool UseGameCameraDistance
        {
            get { return _useGameCameraDistance.Value; }
        }

        public bool InstructorEnabled
        {
            get { return _instructorEnabled.Value; }
        }




        public float BankBlendStart
        {
            get
            {
                float value = TuningNumber("BankBlendStart", _bankBlendStart.Value);
                if (value < 0.2f)
                {
                    return 0.2f;
                }

                return value > 90f ? 90f : value;
            }
        }

        public float BankBlendEnd
        {
            get
            {
                float value = TuningNumber("BankBlendEnd", _bankBlendEnd.Value);
                if (value < 0.5f)
                {
                    return 0.5f;
                }

                float bankEnd = value > 120f ? 120f : value;
                float bankStart = BankBlendStart;
                return bankEnd < bankStart + 1f ? bankStart + 1f : bankEnd;
            }
        }

        public float VerticalFadeStart
        {
            get
            {
                float value = TuningNumber("VerticalFadeStart", _verticalFadeStart.Value);
                if (value < 2f)
                {
                    return 2f;
                }

                return value > 45f ? 45f : value;
            }
        }

        public float VerticalFadeEnd
        {
            get
            {
                float value = TuningNumber("VerticalFadeEnd", _verticalFadeEnd.Value);
                if (value < 5f)
                {
                    return 5f;
                }

                float fadeEnd = value > 90f ? 90f : value;
                float fadeStart = VerticalFadeStart;
                return fadeEnd < fadeStart + 1f ? fadeStart + 1f : fadeEnd;
            }
        }

        public float AttitudeResponse
        {
            get
            {
                float value = TuningNumber("AttitudeResponse", _attitudeResponse.Value);
                if (value < 0.05f)
                {
                    return 0.05f;
                }

                return value > 2f ? 2f : value;
            }
        }

        public float MaxRollRate
        {
            get
            {
                float value = TuningNumber("MaxRollRate", _maxRollRate.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 400f ? 400f : value;
            }
        }

        public float MaxLevelRollRate
        {
            get
            {
                float value = TuningNumber("MaxLevelRollRate", _maxLevelRollRate.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 200f ? 200f : value;
            }
        }

        public float MaxPitchRate
        {
            get
            {
                float value = TuningNumber("MaxPitchRate", _maxPitchRate.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 150f ? 150f : value;
            }
        }

        public float YawSign
        {
            get
            {
                return TuningNumber("YawSign", _yawSign.Value) < 0f ? -1f : 1f;
            }
        }

        public float MaxYawRate
        {
            get
            {
                float value = TuningNumber("MaxYawRate", _maxYawRate.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 60f ? 60f : value;
            }
        }

        public float YawInTurnShare
        {
            get
            {
                float value = TuningNumber("YawInTurnShare", _yawInTurnShare.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 1f ? 1f : value;
            }
        }


        public float YawCoordStrength
        {
            get
            {
                float value = TuningNumber("YawCoordStrength", _yawCoordStrength.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 1.5f ? 1.5f : value;
            }
        }

        public float WingsLevelGain
        {
            get
            {
                float value = TuningNumber("WingsLevelGain", _wingsLevelGain.Value);
                if (value < 0.5f)
                {
                    return 0.5f;
                }

                return value > 3f ? 3f : value;
            }
        }

        public float BlendSmoothing
        {
            get
            {
                float value = TuningNumber("BlendSmoothing", _blendSmoothing.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.5f ? 0.5f : value;
            }
        }

        public float KdYaw
        {
            get
            {
                float value = TuningNumber("KdYaw", _kdYaw.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.5f ? 0.5f : value;
            }
        }










        public float OutputSlew
        {
            get
            {
                float value = TuningNumber("OutputSlew", _outputSlew.Value);
                if (value < 0.5f)
                {
                    return 0.5f;
                }

                return value > 30f ? 30f : value;
            }
        }

        public float RateSmoothing
        {
            get
            {
                float value = TuningNumber("RateSmoothing", _rateSmoothing.Value);
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.3f ? 0.3f : value;
            }
        }





        public float KdRoll
        {
            get { return ClampGain(TuningNumber("KdRoll", _kdRoll.Value)); }
        }





        public float PitchArrivalRadius { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchArrivalRadius", _pitchArrivalRadius.Value), 0.5f, 5f); } }
        public float PitchBrakingLead { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchBrakingLead", _pitchBrakingLead.Value), 0f, 0.4f); } }
        public float PitchRateIntegralGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchRateIntegralGain", _pitchRateIntegralGain.Value), 0f, 1f); } }
        public float PitchRateTrackingGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchRateTrackingGain", _pitchRateTrackingGain.Value), 0.1f, 2f); } }
        public float PitchAccelerationDamping { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchAccelerationDamping", _pitchAccelerationDamping.Value), 0f, 0.2f); } }
        public float PitchIntegralReleaseGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchIntegralReleaseGain", _pitchIntegralReleaseGain.Value), 0f, 5f); } }
        public float PitchDemandAcceleration { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchDemandAcceleration", _pitchDemandAcceleration.Value), 0f, 1000f); } }
        public float RollDemandAcceleration { get { return UnityEngine.Mathf.Clamp(TuningNumber("RollDemandAcceleration", _rollDemandAcceleration.Value), 0f, 2000f); } }
        public float YawBrakingLead { get { return UnityEngine.Mathf.Clamp(TuningNumber("YawBrakingLead", _yawBrakingLead.Value), 0f, 0.4f); } }
        public float YawDemandGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("YawDemandGain", _yawDemandGain.Value), 0.2f, 1.5f); } }
        public float RollCaptureAngle { get { return UnityEngine.Mathf.Clamp(TuningNumber("RollCaptureAngle", _rollCaptureAngle.Value), 0.2f, 2f); } }
        public float RollCaptureExitAngle { get { return UnityEngine.Mathf.Clamp(TuningNumber("RollCaptureExitAngle", _rollCaptureExitAngle.Value), RollCaptureAngle + 0.5f, 10f); } }
        public float RollCaptureRate { get { return UnityEngine.Mathf.Clamp(TuningNumber("RollCaptureRate", _rollCaptureRate.Value), 1f, 20f); } }
        public float RollCaptureDwell { get { return UnityEngine.Mathf.Clamp(TuningNumber("RollCaptureDwell", _rollCaptureDwell.Value), 0f, 0.5f); } }
        public bool PitchFastResponseProtection { get { return TuningBool("PitchFastResponseProtection", _pitchFastResponseProtection.Value); } }
        public float PitchBalanceGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchBalanceGain", _pitchBalanceGain.Value), 0f, 0.1f); } }
        public float PitchBalanceLimit { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchBalanceLimit", _pitchBalanceLimit.Value), 0f, 0.4f); } }
        public float PitchBalanceSlew { get { return UnityEngine.Mathf.Clamp(TuningNumber("PitchBalanceSlew", _pitchBalanceSlew.Value), 0.001f, 0.1f); } }
        public float YawAimGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("YawAimGain", _yawAimGain.Value), 0.5f, 2f); } }
        public float YawFineGain { get { return UnityEngine.Mathf.Clamp(TuningNumber("YawFineGain", _yawFineGain.Value), 0.5f, 2.5f); } }

        public string RecordKey
        {
            get { return _recordKey.Value; }
        }

        public string RecordMarkerKey { get { return _recordMarkerKey.Value; } }

        public float RecordIntervalSeconds
        {
            get
            {
                float value = _recordIntervalSeconds.Value;
                if (value < 0.02f)
                {
                    return 0.02f;
                }

                return value > 1f ? 1f : value;
            }
        }







        public int RollSign
        {
            get { return TuningNumber("RollSign", _rollSign.Value) < 0 ? -1 : 1; }
        }

        public int PitchSign
        {
            get { return TuningNumber("PitchSign", _pitchSign.Value) < 0 ? -1 : 1; }
        }

        private static float ClampGain(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 0.3f ? 0.3f : value;
        }

        public float FollowDampScale
        {
            get
            {
                float value = _followDampScale.Value;
                if (value < 0.1f)
                {
                    return 0.1f;
                }

                return value > 3f ? 3f : value;
            }
        }

        public string CameraName
        {
            get
            {
                string value = _cameraName.Value;
                if (value == null || value.Trim().Length == 0)
                {
                    return "Flight Control View";
                }

                return value.Trim();
            }
        }

        public bool ForceHudVisible
        {
            get { return _forceHudVisible.Value; }
        }

        public float CameraMaxShift
        {
            get
            {
                float value = _cameraMaxShift.Value;
                if (value < 50f)
                {
                    return 50f;
                }

                return value > 100000f ? 100000f : value;
            }
        }

        public float SensitivityDegreesPerUnit
        {
            get
            {
                float value = _sensitivityDegreesPerUnit.Value;
                if (value < 0.05f)
                {
                    return 0.05f;
                }

                return value > 20f ? 20f : value;
            }
        }

        public float MouseDeadzone
        {
            get
            {
                float value = _mouseDeadzone.Value;
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.5f ? 0.5f : value;
            }
        }

        public float ScreenEdgeMarginRatio
        {
            get
            {
                float value = _screenEdgeMarginRatio.Value;
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.3f ? 0.3f : value;
            }
        }

        public float RecenterSeconds
        {
            get
            {
                float value = _recenterSeconds.Value;
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 10f ? 10f : value;
            }
        }

        public bool UseMousePositionDelta
        {
            get { return _useMousePositionDelta.Value; }
        }

        public bool ShowCrosshair
        {
            get { return _showCrosshair.Value; }
        }

        public bool ShowNoseMarker
        {
            get { return _showNoseMarker.Value; }
        }

        public bool ShowLinkLine
        {
            get { return _showLinkLine.Value; }
        }

        public bool HideHudWithGameUi
        {
            get { return _hideHudWithGameUi.Value; }
        }

        public float CrosshairRadiusRatio
        {
            get
            {
                float value = _crosshairRadiusRatio.Value;
                if (value < 0.004f)
                {
                    return 0.004f;
                }

                return value > 0.1f ? 0.1f : value;
            }
        }

        public float NoseMarkerSizeRatio
        {
            get
            {
                float value = _noseMarkerSizeRatio.Value;
                if (value < 0.003f)
                {
                    return 0.003f;
                }

                return value > 0.08f ? 0.08f : value;
            }
        }

        public float DashSpacingRatio
        {
            get
            {
                float value = _dashSpacingRatio.Value;
                if (value < 0.002f)
                {
                    return 0.002f;
                }

                return value > 0.05f ? 0.05f : value;
            }
        }

        public float HeightOffsetRatio
        {
            get
            {
                float value = _heightOffsetRatio.Value;
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 1f ? 1f : value;
            }
        }

        public float TurnShiftRatio
        {
            get
            {
                float value = _turnShiftRatio.Value;
                if (value < 0f)
                {
                    return 0f;
                }

                return value > 0.6f ? 0.6f : value;
            }
        }
    }
}
