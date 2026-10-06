using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // Aim guidance -> nominal rate -> stick with measured-rate damping.
    // Stage two adds conditional equilibrium compensation; it does not estimate control authority.
    // 符号来自手动飞行记录实测（Test/RecordKey）：游戏 Roll 轴正 = 右滚；
    // Pitch 轴正 = 低头（所以本插件"正 = 抬头"的需求要取反）；两个角速度点积都要取负号。
    internal sealed class Instructor
    {
        private readonly PluginSettings _settings;
        private readonly PluginLog _log;
        private readonly ControlOverrideBinder _rollChannel = new ControlOverrideBinder();
        private readonly ControlOverrideBinder _pitchChannel = new ControlOverrideBinder();
        private readonly ControlOverrideBinder _yawChannel = new ControlOverrideBinder();
        private readonly PitchBalance _pitchBalance = new PitchBalance();
        private readonly PitchRateController _pitchRateController = new PitchRateController();
        private readonly RateDemandProfile _pitchProfile = new RateDemandProfile();
        private readonly RateDemandProfile _rollProfile = new RateDemandProfile();
        private readonly RollCaptureState _rollCapture = new RollCaptureState();
        private float _pitchRateDemand;
        private Vector3 _previousAim;
        private bool _hasPreviousAim;
        private float _balanceApplied, _yawAimGain;
        private float _pullGate = 1f, _pitchDemand, _bankDegrees;

        private bool _active;
        private bool _yawBound;
        private object _yawPlayerInput;
        private float _nextDiagTime;
        private float _nextJumpTime;
        private float _commandHistoryTime;
        private float _rollCommandAgo;
        private float _pitchCommandAgo;

        private float _rollRate;
        private float _pitchRate;
        private float _yawRate;
        private float _blendSmoothed;
        private float _flightPathYawRate;
        private float _lastFlightPathYaw;
        private bool _hasFlightPathYaw;

        private float _off;
        private float _rollError;
        private float _pitchError;
        private float _rollRateCmd;
        private float _pitchRateCmd;
        private float _rollCommand;
        private float _pitchCommand;
        private float _yawError;
        private float _yawRateCmd;
        private float _yawCommand;
        private bool _playerOnly, _manualPitch, _manualRoll, _manualYaw;
        private int _pitchEngagement, _rollEngagement;
        public bool PlayerOnly { get { return _playerOnly; } }
        public bool PlayerPitchActive { get { return _pitchChannel.PlayerActive; } }
        public bool PlayerRollActive { get { return _rollChannel.PlayerActive; } }
        public bool PlayerYawActive { get { return _yawBound && _yawChannel.PlayerActive; } }
        public float PlayerPitch { get { return _pitchChannel.LastInputAxis; } }
        public float PlayerRoll { get { return _rollChannel.LastInputAxis; } }
        public float PlayerYaw { get { return _yawBound ? _yawChannel.LastInputAxis : 0f; } }

        public bool TryReadPlayerManeuver(out bool maneuver)
        {
            maneuver = false;
            if (!IsActive) return false;
            string error;
            float yaw = 0f;
            if (!_rollChannel.RefreshPlayerInput(out error) || !_pitchChannel.RefreshPlayerInput(out error)) return false;
            if (_yawBound)
            {
                if (!_yawChannel.RefreshPlayerInput(out error)) return false;
                yaw = _yawChannel.LastInputAxis;
            }
            else if (_yawPlayerInput != null && !GameApi.TryReadPlayerInputAxis(_yawPlayerInput, out yaw, out error)) return false;
            // Raw mapped input only: neither aircraft motion nor automatic commands trigger takeover.
            maneuver = Mathf.Abs(_rollChannel.LastInputAxis) > 0.0001f
                || Mathf.Abs(_pitchChannel.LastInputAxis) > 0.0001f || Mathf.Abs(yaw) > 0.0001f;
            return true;
        }
        public void SetPlayerOnly(bool on)
        {
            _playerOnly = on;
            _rollChannel.SetPlayerOnly(on); _pitchChannel.SetPlayerOnly(on);
            if (_yawBound) _yawChannel.SetPlayerOnly(on);
        }

        public Instructor(PluginSettings settings, PluginLog log)
        {
            _settings = settings;
            _log = log;
        }

        public bool IsActive
        {
            get { return _active; }
        }

        public float PitchCommand { get { return _pitchCommand; } }
        public float RollCommand { get { return _rollCommand; } }
        public float YawCommand { get { return _yawCommand; } }
        public float CommandTime { get; private set; }
        public int CommandFrame { get; private set; }
        public float BalanceStick { get { return _pitchBalance.Value; } }
        public float BalanceApplied { get { return _balanceApplied; } }
        public bool BalanceLearning { get { return _pitchBalance.Learning; } }
        public bool BalanceResponseSeen { get { return _pitchBalance.ResponseSeen; } }
        public float RollBlend { get { return _blendSmoothed; } }
        public float YawAimGain { get { return _yawAimGain; } }
        public float PullGate { get { return _pullGate; } }
        public float PitchDemand { get { return _pitchDemand; } }
        public float PitchRateTarget { get { return _pitchRateCmd; } }
        public float PitchRateStick { get { return _pitchRateController.Stick; } }
        public float PitchHeldStick { get { return _pitchRateController.HeldStick; } }
        public bool PitchTrackingLimited { get { return _pitchRateController.TrackingLimited; } }
        public bool PitchIntegralReleasing { get { return _pitchRateController.Releasing; } }
        public float PitchRateDemand { get { return _pitchRateDemand; } }
        public float RollRateTarget { get { return _rollRateCmd; } }
        public bool RollCaptured { get { return _rollCapture.Captured; } }
        public float PitchReleaseRemaining { get { return _pitchRateController.ReleaseRemaining; } }
        public bool PitchFastResponse { get { return _pitchRateController.FastResponse; } }

        public bool IsBoundTo(object controls)
        {
            return _active && ReferenceEquals(_rollChannel.BoundControls, controls)
                && ReferenceEquals(_pitchChannel.BoundControls, controls);
        }

        public bool TryActivate(object controls, object rollInput, object pitchInput, object yawInput, out string error)
        {
            error = null;

            if (!_settings.InstructorEnabled)
            {
                error = "教官按配置关闭";
                return false;
            }

            if (!InstructorOverridePatch.IsInstalled)
            {
                error = "飞控通道保护未安装";
                return false;
            }

            if (_active)
            {
                return true;
            }

            if (controls == null || rollInput == null || pitchInput == null)
            {
                error = "操纵对象或通道输入为空";
                return false;
            }

            if (!_rollChannel.TryBind(controls, ControlOverrideBinder.ChannelRoll, rollInput, 0f, out error))
            {
                return false;
            }

            if (!_pitchChannel.TryBind(controls, ControlOverrideBinder.ChannelPitch, pitchInput, 0f, out error))
            {
                _rollChannel.Unbind();
                return false;
            }

            // 偏航是可选通道：失败只关掉方向舵，不影响滚转与俯仰接管
            _yawBound = false;
            if (yawInput == null)
            {
                _log.Warn("方向舵未接管：取不到偏航通道输入。滚转与俯仰照常工作。");
            }
            else
            {
                string yawError2;
                if (_yawChannel.TryBind(controls, ControlOverrideBinder.ChannelYaw, yawInput, 0f, out yawError2))
                {
                    _yawBound = true;
                }
                else
                {
                    _log.Warn("方向舵未接管：" + yawError2 + "。滚转与俯仰照常工作。");
                }
            }

            _active = true;
            _rollChannel.EnablePlayerSharing(); _pitchChannel.EnablePlayerSharing();
            _yawPlayerInput = yawInput;
            if (_yawBound) _yawChannel.EnablePlayerSharing();
            _manualPitch = _manualRoll = _manualYaw = false;
            _pitchEngagement = _rollEngagement = 0;
            SetPlayerOnly(false);
            InstructorOverridePatch.Bind(controls);
            _nextDiagTime = 0f;
            _rollRate = 0f;
            _pitchRate = 0f;
            _yawRate = 0f;
            _rollCommand = 0f;
            _pitchCommand = 0f;
            _yawCommand = 0f;
            _blendSmoothed = 0f;
            _flightPathYawRate = 0f;
            _hasFlightPathYaw = false;
            _rollCommandAgo = 0f;
            _pitchCommandAgo = 0f;
            _commandHistoryTime = 0f;
            _pitchBalance.Reset();
            _pitchRateController.Reset();
            _pitchProfile.Reset(); _rollProfile.Reset(); _rollCapture.Reset(); _pitchRateDemand = 0f;
            _hasPreviousAim = false;
            _balanceApplied = 0f;
            _pullGate = 1f;
            _pitchDemand = 0f;
            CommandTime = 0f;
            CommandFrame = 0;

            _log.Info("教官已接管（制导→姿态→速率三级）：回平进入/退出 "
                + _settings.RollCaptureAngle.ToString("0.0") + "/" + _settings.RollCaptureExitAngle.ToString("0.0") + " 度，"
                + "进入需指向转速低于 " + _settings.RollCaptureRate.ToString("0.0") + " 度/秒，"
                + "正上/正下渐隐 " + _settings.VerticalFadeStart.ToString("0") + "~" + _settings.VerticalFadeEnd.ToString("0") + " 度，"
                + "额外拉杆限制关闭，"
                + "滚转/偏航响应 " + _settings.AttitudeResponse.ToString("0.00") + " 秒，"
                + "俯仰收尾半径 " + _settings.PitchArrivalRadius.ToString("0.0")
                + " 度，俯仰转速限制=" + (_settings.MaxPitchRate > 0 ? _settings.MaxPitchRate.ToString("0.0") + " 度/秒" : "关闭")
                + "，实测转速反馈，无滚转角度限制、无失速保护。");
            return true;
        }

        public void Deactivate(string reason)
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            SetPlayerOnly(false);
            _pitchBalance.Reset();
            _pitchRateController.Reset();
            _pitchProfile.Reset(); _rollProfile.Reset(); _rollCapture.Reset(); _pitchRateDemand = 0f;
            _balanceApplied = 0f;
            _hasPreviousAim = false;
            InstructorOverridePatch.Unbind(_rollChannel.BoundControls);

            bool rollOk = _rollChannel.Unbind();
            bool pitchOk = _pitchChannel.Unbind();
            bool yawOk = !_yawBound || _yawChannel.Unbind();
            _yawBound = false;
            _yawPlayerInput = null;

            if (rollOk && pitchOk && yawOk)
            {
                _log.Info("教官已交还滚转与俯仰通道（原因：" + reason + "）。");
            }
            else
            {
                _log.Warn("教官交还通道失败（原因：" + reason
                    + "）：滚转=" + (rollOk ? "成功" : "失败")
                    + "，俯仰=" + (pitchOk ? "成功" : "失败")
                    + "，这几条通道会一直由插件驱动。");
            }
        }

        public void Shutdown()
        {
            Deactivate("插件卸载");
        }

        public void Update(
            float deltaTime,
            Vector3 aimDirection,
            Quaternion aircraftRotation,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            if (!_active)
            {
                return;
            }

            string inputError;
            if (!FlightRecorder.Finite(deltaTime) || deltaTime <= 0f)
            { Deactivate("更新时间无效"); return; }
            if (!_rollChannel.RefreshPlayerInput(out inputError)
                || !_pitchChannel.RefreshPlayerInput(out inputError)
                || (_yawBound && !_yawChannel.RefreshPlayerInput(out inputError)))
            { _log.Warn("玩家操纵输入读取失败：" + inputError); Deactivate("玩家输入不可读"); return; }
            bool pitchManual = _pitchChannel.PlayerActive;
            bool rollManual = _rollChannel.PlayerActive;
            bool yawManual = _yawBound && _yawChannel.PlayerActive;
            bool yawTouched = yawManual || _manualYaw;
            bool pitchTouched = pitchManual || _manualPitch || _pitchEngagement != _pitchChannel.PlayerEngagement;
            bool rollTouched = rollManual || _manualRoll || _rollEngagement != _rollChannel.PlayerEngagement;
            if (pitchTouched)
            { _pitchRateController.SuspendForPlayer(); _pitchProfile.Reset(); _pitchBalance.SuspendLearning(); _balanceApplied = 0f; }
            if (rollTouched) { _rollProfile.Reset(); _rollCapture.Reset(); }
            if (pitchManual != _manualPitch || rollManual != _manualRoll || yawManual != _manualYaw)
                _log.Info("操纵控制权：滚转=" + (rollManual ? "玩家" : "鼠标飞控")
                    + "，俯仰=" + (pitchManual ? "玩家" : "鼠标飞控")
                    + "，偏航=" + (yawManual ? "玩家" : "鼠标飞控") + "，纯玩家=" + _playerOnly);
            _manualPitch = pitchManual; _manualRoll = rollManual; _manualYaw = yawManual;
            _pitchEngagement = _pitchChannel.PlayerEngagement; _rollEngagement = _rollChannel.PlayerEngagement;
            if (pitchTouched) _pitchCommand = _pitchChannel.Value;
            if (rollTouched) _rollCommand = _rollChannel.Value;
            if (yawTouched) _yawCommand = _yawChannel.Value;

            if (_playerOnly)
            {
                _pitchRateController.SuspendForPlayer(); _pitchBalance.SuspendLearning();
                _pitchProfile.Reset(); _rollProfile.Reset(); _rollCapture.Reset();
                _balanceApplied = _pitchRateCmd = _rollRateCmd = _yawRateCmd = _pitchRateDemand = 0f;
                _hasPreviousAim = _hasFlightPathYaw = false;
                _rollRate = -Vector3.Dot(angularVelocity, forward) * Mathf.Rad2Deg;
                _pitchRate = -Vector3.Dot(angularVelocity, right) * Mathf.Rad2Deg;
                _yawRate = Vector3.Dot(angularVelocity, up) * Mathf.Rad2Deg;
                _blendSmoothed = _flightPathYawRate = 0f;
                WriteCommands(deltaTime);
                return;
            }

            if (!FlightRecorder.Finite(deltaTime) || deltaTime <= 0f
                || !FlightRecorder.Finite(aimDirection) || !FlightRecorder.Finite(velocity)
                || !FlightRecorder.Finite(angularVelocity) || !FlightRecorder.Finite(forward)
                || !FlightRecorder.Finite(right) || !FlightRecorder.Finite(up)
                || !FlightRecorder.Finite(aircraftRotation))
            { Deactivate("飞控状态无效"); return; }
            // A long render stall cannot be treated as uninterrupted stable flight.
            if (deltaTime > 0.1f) { _pitchBalance.Reset(); _pitchRateController.Reset(); _pitchProfile.Reset(); _rollProfile.Reset(); _rollCapture.Reset(); _hasPreviousAim = false; }
            float aimChange = _hasPreviousAim ? Vector3.Angle(_previousAim, aimDirection) : 0f;
            float aimRate = aimChange / deltaTime;
            if (aimChange > 15f) { _pitchBalance.Reset(); _pitchRateController.Reset(); _pitchProfile.Reset(); _rollProfile.Reset(); _rollCapture.Reset(); }
            _previousAim = aimDirection; _hasPreviousAim = true;

            // ---- 实测角速度：取负号后"正 = 向右滚 / 正 = 抬头"，再低通供阻尼用 ----
            float rollRateRaw = -Vector3.Dot(angularVelocity, forward) * Mathf.Rad2Deg;
            float pitchRateRaw = -Vector3.Dot(angularVelocity, right) * Mathf.Rad2Deg;
            // 方向舵：机体上方向的角速度分量。实测结论——游戏 Yaw 轴正值 = 机头向右 = 该分量为正，故不取负号
            float yawRateRaw = Vector3.Dot(angularVelocity, up) * Mathf.Rad2Deg;

            float tau = _settings.RateSmoothing;
            float alpha = tau <= 0f ? 1f : deltaTime / (tau + deltaTime);
            _rollRate += (rollRateRaw - _rollRate) * alpha;
            _pitchRate += (pitchRateRaw - _pitchRate) * alpha;
            _yawRate += (yawRateRaw - _yawRate) * alpha;

            // ---- 航迹偏航率：速度矢量在机体系里的横向角每帧转了多少 ----
            // 这是"速度矢量真实在转多快"，方向舵据它跟随转弯（参考实现：跟随航迹偏航率），
            // 而不是用机身滚转角去猜转弯强度——猜出来的量会让方向舵抢在滚转前面把机头掰过去。
            if (velocity.sqrMagnitude > 25f)
            {
                Vector3 velocityLocal = Quaternion.Inverse(aircraftRotation) * velocity;
                float flightPathYaw = Mathf.Atan2(velocityLocal.x, velocityLocal.z) * Mathf.Rad2Deg;

                if (_hasFlightPathYaw)
                {
                    float rawRate = Mathf.DeltaAngle(_lastFlightPathYaw, flightPathYaw)
                        / Mathf.Max(deltaTime, 0.0001f);
                    rawRate = Mathf.Clamp(rawRate, -40f, 40f);
                    _flightPathYawRate += (rawRate - _flightPathYawRate) * alpha;
                }

                _lastFlightPathYaw = flightPathYaw;
                _hasFlightPathYaw = true;
            }
            else
            {
                _flightPathYawRate = 0f;
                _hasFlightPathYaw = false;
            }

            // ---- 制导：瞄准方向在机体坐标系里的分量 ----
            Vector3 aimLocal = Quaternion.Inverse(aircraftRotation) * aimDirection;
            float lateral = aimLocal.x;
            float vertical = aimLocal.y;
            float tangent = Mathf.Sqrt(lateral * lateral + vertical * vertical);
            // atan2 remains accurate for small angles where float acos rounds z to one.
            _off = Mathf.Atan2(tangent, aimLocal.z) * Mathf.Rad2Deg;

            // 滚转：两种模式按方向混合（不按角度，避免 ±180 处翻符号）
            //   机翼回平：机体右向的竖直分量。右机身滚转角时为负 → 命令向左滚 → 回平（与实测符号一致）
            //   朝目标朝目标滚转：瞄准方向在机体右向的分量，目标在右侧就向右压
            // A1：回平误差按**机身滚转角**（度）线性给，不再是 right.y——
            // right.y 在 80~90 度附近饱和，滚到 85 度和滚到 90 度给一样大的杆量，收不回来。
            // 线性给法：滚转 45 度给半杆、15 度给六分之一杆，接近水平时自然收杆；
            // 力度倍数由 WingsLevelGain 控制（默认 1.33，约 68 度即给满杆）。
            float bankDegrees = -Mathf.Atan2(right.y, up.y) * Mathf.Rad2Deg;   // 正 = 向右滚
            _bankDegrees = bankDegrees;
            float wingsLevel = Mathf.Clamp(
                -bankDegrees * _settings.WingsLevelGain / 90f, -1f, 1f);
            float bankInto = lateral;
            float blend = SmoothStep(_settings.BankBlendStart, _settings.BankBlendEnd, _off);
            blend = _rollCapture.Update(deltaTime, _off,
                Mathf.Sqrt(_pitchRate * _pitchRate + _yawRate * _yawRate), blend,
                _settings.RollCaptureAngle, _settings.RollCaptureExitAngle,
                _settings.RollCaptureRate, _settings.RollCaptureDwell);

            // 目标接近机体正上/正下时"机翼回平"方向不定 → 渐隐，滚转保持当前机身滚转角
            // 判据是"目标离机身竖直轴有多近"，对单位向量就是纵向分量本身 |vertical| = cos(离竖直轴的角度)。
            // 之前错写成 |vertical| / tangent：那个量在目标位于机身竖直**平面**内时也是 1
            // （正前方、正上方、正下方都一样），于是平飞时机翼再也不回平，倒飞同理。
            float verticality = Mathf.Abs(vertical);
            // |纵向| 越大 = 越接近机身竖直轴，SmoothStep 输出越大 = 回平越该让位。
            // 这里**不能**再取反（A1：多一层 1- 会让回平在正常平飞时被永久关掉，
            // 而在真正该关的正上/正下区域反而生效）。
            float fade = SmoothStep(Mathf.Cos(_settings.VerticalFadeEnd * Mathf.Deg2Rad),
                                    Mathf.Cos(_settings.VerticalFadeStart * Mathf.Deg2Rad),
                                    verticality);
            blend = Mathf.Max(blend, fade);

            // Smooth the blend even after narrowing the near-aim transition.
            float blendTau = _settings.BlendSmoothing;
            float blendAlpha = blendTau <= 0f ? 1f : deltaTime / (blendTau + deltaTime);
            _blendSmoothed += (blend - _blendSmoothed) * blendAlpha;
            blend = _blendSmoothed;

            _rollError = Mathf.Lerp(wingsLevel, bankInto, blend);

            // Pitch follows the geometric vertical error without an extra roll-first restriction.
            if (tangent > 1e-4f)
            {
                _pitchError = _off * vertical / tangent;
            }
            else
            {
                _pitchError = aimLocal.z < 0f ? 180f : 0f;
            }

            // Diagnostic comparison build: remove both the 2..8 degree transition and
            // the additional restriction at every angle. Preserve the geometric projection.
            float pullGate = 1f;
            _pullGate = pullGate;
            float pitchDemand = _pitchError;
            _pitchDemand = pitchDemand;

            // 偏航：收尾最后几度机头方向，并在大转弯时把活让给滚转（参考实现里方向舵的职责）。
            // 误差用切平面上的横向分量，正 = 目标在机体右侧 → 机头需要向右偏。
            if (tangent > 1e-4f)
            {
                // 对准收尾：随滚转接管而减弱，但保留 YawInTurnShare 的份额——
                // 战雷里方向舵在大机动中同样参与，不是完全让位给滚转。
                _yawError = _off * lateral / tangent
                    * Mathf.Lerp(1f, _settings.YawInTurnShare, blend);
            }
            else
            {
                _yawError = 0f;
            }

            // Existing coordination uses the derivative of body-relative velocity azimuth.
            // It is not a direct measurement of world flight-path turning rate.
            float yawLimit = _settings.MaxYawRate;
            float yawCoordination = (yawLimit > 0 ? Mathf.Clamp(_flightPathYawRate, -yawLimit, yawLimit) : _flightPathYawRate) * _settings.YawCoordStrength;

            // A3：朝目标滚转与回平用不同上限——回平要柔和（45 度/秒），朝目标滚转要利落（140 度/秒）。
            // 数值取自 MouseAimFlightRedux 发布的 Normal / Cruise 两套参数。
            float rollLimit = Mathf.Lerp(_settings.MaxLevelRollRate, _settings.MaxRollRate, blend);
            float rollErrorDegrees = _rollError * 90f;
            float pitchErrorDegrees = pitchDemand;
            float rollDemand = _settings.MaxLevelRollRate > 0 && _settings.MaxRollRate > 0
                ? ArrivalRate(rollErrorDegrees, rollLimit)
                : Mathf.Lerp(ArrivalRate(rollErrorDegrees, _settings.MaxLevelRollRate), ArrivalRate(rollErrorDegrees, _settings.MaxRollRate), blend);
            _rollRateCmd = _rollProfile.Update(rollDemand,
                deltaTime, _settings.RollDemandAcceleration);
            float lateralError = _off * lateral / Mathf.Max(tangent, 1e-4f);
            _pitchRateDemand = PitchRateController.ArrivalRate(pitchErrorDegrees, lateralError,
                _pitchRate, _yawRate, _settings.PitchArrivalRadius, _settings.PitchBrakingLead,
                _settings.MaxPitchRate);
            _pitchRateCmd = _pitchProfile.Update(_pitchRateDemand, deltaTime, _settings.PitchDemandAcceleration);

            // ---- 速率：角速度误差 → 杆量（比例 + 阻尼）----
            float rollScale = _settings.MaxRollRate > 0 ? _settings.MaxRollRate : 120f;
            float rollStick = _rollRateCmd / rollScale - _settings.KdRoll * (_rollRate / rollScale);
            float actualPitch;
            bool actualValid = FlightRecorder.ReadFloat(_pitchChannel.BoundControls,
                GameApi.AircraftControlsType, "Pitch", out actualPitch);
            if (!actualValid) { Deactivate("实际俯仰操纵不可读"); return; }
            float pitchStick = pitchManual ? 0f : _pitchRateController.Update(deltaTime, _pitchRateCmd,
                _pitchRate, _settings.MaxPitchRate, actualPitch * _settings.PitchSign,
                _pitchCommand * _settings.PitchSign, _balanceApplied,
                _settings.PitchRateTrackingGain, _settings.PitchRateIntegralGain, _settings.PitchAccelerationDamping,
                _settings.PitchIntegralReleaseGain, _settings.PitchFastResponseProtection);
            float balanceWeight = (1f - SmoothStep(5f, 15f, _off))
                * (1f - SmoothStep(15f, 45f, Mathf.Abs(bankDegrees)));
            if (!pitchManual) _pitchBalance.Update(deltaTime, _pitchError, _pitchRate, pitchRateRaw,
                rollRateRaw, bankDegrees, _off * lateral / Mathf.Max(tangent, 1e-4f),
                pullGate, Vector3.Dot(velocity, forward), aimRate, pitchStick, balanceWeight,
                actualValid, actualPitch * _settings.PitchSign,
                _pitchCommand * _settings.PitchSign, _settings.PitchBalanceGain,
                _settings.PitchBalanceLimit, _settings.PitchBalanceSlew);
            _balanceApplied = pitchManual ? 0f : _pitchBalance.Value * balanceWeight;
            pitchStick += _balanceApplied;
            _yawAimGain = Mathf.Lerp(_settings.YawFineGain, _settings.YawAimGain,
                SmoothStep(0.3f, 6f, _off));
            _yawRateCmd = ArrivalRate(_yawError * _yawAimGain - yawRateRaw * _settings.YawBrakingLead, yawLimit) + yawCoordination;
            if (yawLimit > 0) _yawRateCmd = Mathf.Clamp(_yawRateCmd, -yawLimit, yawLimit);
            // Reduce large-maneuver demand after saturation, preserving fine aim and rate damping.
            _yawRateCmd *= Mathf.Lerp(1f, 0.65f, SmoothStep(12f, 30f, _off));
            float yawScale = yawLimit > 0 ? yawLimit : 20f;
            float yawStick = _settings.YawDemandGain * _yawRateCmd / yawScale - _settings.KdYaw * (_yawRate / yawScale);

            float slew = _settings.OutputSlew * deltaTime;
            _rollCommand = Mathf.MoveTowards(_rollCommand,
                _settings.RollSign * Mathf.Clamp(rollStick, -1f, 1f), slew);
            _pitchCommand = Mathf.MoveTowards(_pitchCommand,
                _settings.PitchSign * Mathf.Clamp(pitchStick, -1f, 1f), slew);
            _yawCommand = Mathf.MoveTowards(_yawCommand,
                _settings.YawSign * Mathf.Clamp(yawStick, -1f, 1f), slew);

            WriteCommands(deltaTime);

            LogCommandJumpIfAny(deltaTime);

            LogDiagnosticIfDue(rollRateRaw, pitchRateRaw, yawRateRaw);
        }

        private void WriteCommands(float deltaTime)
        {
            _rollChannel.SetValue(_rollCommand, deltaTime, _settings.OutputSlew);
            _pitchChannel.SetValue(_pitchCommand, deltaTime, _settings.OutputSlew);
            _rollCommand = _rollChannel.Value; _pitchCommand = _pitchChannel.Value;
            CommandTime = Time.time;
            CommandFrame = Time.frameCount;
            if (_yawBound)
            {
                _yawChannel.SetValue(_yawCommand, deltaTime, _settings.OutputSlew);
                _yawCommand = _yawChannel.Value;
            }
        }

        // 角度域是关键：小角度时杆量不会被压掉（5 度的滚转误差给 20 度/秒，而不是按杆量比例只给 2.5 度/秒），
        // 这正是"不动鼠标时机头左右摇摆"的一个成因。
        private float ArrivalRate(float errorDegrees, float rateLimit)
        {
            float response = _settings.AttitudeResponse;
            if (response <= 0f)
            {
                return rateLimit > 0 ? Mathf.Clamp(errorDegrees, -rateLimit, rateLimit) : errorDegrees;
            }

            float wanted = errorDegrees / response;

            return rateLimit > 0 ? Mathf.Clamp(wanted, -rateLimit, rateLimit) : wanted;
        }


        // low..high 上的平滑插值（比线性过渡少一点折角）
        private static float SmoothStep(float low, float high, float value)
        {
            if (high - low < 1e-6f)
            {
                return value >= high ? 1f : 0f;
            }

            float t = Mathf.Clamp01((value - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        // 抓"突然打满"：输出限速让单帧跳变不超过 0.1，所以看 0.25 秒内的累计变化。
        private void LogCommandJumpIfAny(float deltaTime)
        {
            _commandHistoryTime += deltaTime;
            if (_commandHistoryTime < 0.25f)
            {
                return;
            }

            float rollDelta = Mathf.Abs(_rollCommand - _rollCommandAgo);
            float pitchDelta = Mathf.Abs(_pitchCommand - _pitchCommandAgo);

            if (rollDelta > 0.5f || pitchDelta > 0.5f)
            {
                if (Time.unscaledTime >= _nextJumpTime)
                {
                    _nextJumpTime = Time.unscaledTime + 2f;
                    _log.Info("教官命令跳变：0.25 秒内 滚转=" + rollDelta.ToString("0.00")
                        + "（" + _rollCommandAgo.ToString("0.00") + "→" + _rollCommand.ToString("0.00") + "）"
                        + " 俯仰=" + pitchDelta.ToString("0.00")
                        + "（" + _pitchCommandAgo.ToString("0.00") + "→" + _pitchCommand.ToString("0.00") + "）"
                        + "；此刻 总偏移=" + _off.ToString("0.0") + " 度，滚转误差=" + _rollError.ToString("+0.00;-0.00")
                        + " 俯仰误差=" + _pitchError.ToString("+0.0;-0.0") + " 度。");
                }
            }

            _rollCommandAgo = _rollCommand;
            _pitchCommandAgo = _pitchCommand;
            _commandHistoryTime = 0f;
        }

        private void LogDiagnosticIfDue(float rollRateRaw, float pitchRateRaw, float yawRateRaw)
        {
            if (!_log.IsNormal)
            {
                return;
            }

            if (Time.unscaledTime < _nextDiagTime)
            {
                return;
            }

            _nextDiagTime = Time.unscaledTime + 0.2f;

            _log.Info("教官诊断：总偏移=" + _off.ToString("0.0")
                + " 度，滚转误差=" + _rollError.ToString("+0.00;-0.00")
                + " 俯仰误差=" + _pitchError.ToString("+0.0;-0.0")
                + " 度，角速度 滚=" + rollRateRaw.ToString("0") + "/" + _rollRate.ToString("0")
                + " 俯=" + pitchRateRaw.ToString("0") + "/" + _pitchRate.ToString("0")
                + " 度/秒，目标角速度 滚=" + _rollRateCmd.ToString("0")
                + " 俯=" + _pitchRateCmd.ToString("0")
                + " 度/秒，偏航 误差=" + _yawError.ToString("+0.0;-0.0")
                + " 角速度=" + yawRateRaw.ToString("0") + " 目标=" + _yawRateCmd.ToString("0")
                + " 度/秒，命令 滚转=" + _rollCommand.ToString("0.00")
                + " 俯仰=" + _pitchCommand.ToString("0.00")
                + " 偏航=" + _yawCommand.ToString("0.00")
                + "，平衡补偿=" + _balanceApplied.ToString("0.000")
                + "（学习=" + _pitchBalance.Learning + "，响应=" + _pitchBalance.ResponseSeen + "）"
                + "，机身滚转=" + _bankDegrees.ToString("0.0")
                + "，拉杆放行=" + _pullGate.ToString("0.000")
                + "，有效俯仰误差=" + _pitchDemand.ToString("0.00")
                + "，俯仰反馈杆量=" + _pitchRateController.Stick.ToString("0.000")
                + "，输出等待=" + _pitchRateController.TrackingLimited
                + "，撤去残留拉杆=" + _pitchRateController.Releasing
                + "，快速响应保护=" + _pitchRateController.FastResponse
                + "，回平状态=" + _rollCapture.Captured
                + (_yawBound ? "" : "（方向舵未接管）") + "。");
        }
    }
}
