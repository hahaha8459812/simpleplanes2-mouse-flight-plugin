using System;

namespace SimplePlanes2MouseFlight
{
    // One game channel per binder. Instructor enables player sharing; test pulses do not.
    // Remove only our own binding, never all AI overrides.
    internal sealed class ControlOverrideBinder
    {
        public const int ChannelPitch = 1;
        public const int ChannelRoll = 2;
        public const int ChannelYaw = 3;

        private object _controls;
        private object _input;
        private Func<float?> _func;
        private float _value;
        private float _lastInputAxis;
        private int _channel;
        private bool _bound;
        private bool _sharePlayer, _playerOnly;
        private readonly PlayerAxisControl _player = new PlayerAxisControl();
        public bool PlayerActive { get { return _player.Manual; } }
        public int PlayerEngagement { get { return _player.Engagement; } }
        public string PlayerInputError { get; private set; }
        public void EnablePlayerSharing() { _sharePlayer = true; }
        public void SetPlayerOnly(bool on) { _playerOnly = on; }
        public bool RefreshPlayerInput(out string error)
        {
            float axis;
            if (!GameApi.TryReadPlayerInputAxis(_input, out axis, out error))
            { PlayerInputError = error; _value = 0f; return false; }
            if (PlayerInputError != null) { error = PlayerInputError; return false; }
            _lastInputAxis = axis;
            _player.Sample(axis, _playerOnly);
            if (_player.Manual) _value = _player.Output;
            return true;
        }

        public bool IsBound
        {
            get { return _bound; }
        }

        public object BoundControls
        {
            get { return _controls; }
        }

        public int Channel
        {
            get { return _channel; }
        }

        public float Value
        {
            get { return _value; }
        }

        // 诊断用：最近一次在委托内部读到的本通道原始输入。
        // 用来判断"玩家的键盘输入在插件上下文里到底能不能读到"。
        public float LastInputAxis
        {
            get { return _lastInputAxis; }
        }

        public bool TryBind(object controls, int channel, object input, float value, out string error)
        {
            error = null;

            if (_bound)
            {
                Unbind();
            }

            if (controls == null || input == null)
            {
                error = "绑定参数有空引用";
                return false;
            }

            _func = GetValue;

            if (!SetOverride(controls, input, _func, ChannelName(channel), out error))
            {
                ResetFields();
                return false;
            }

            _controls = controls;
            _input = input;
            _channel = channel;
            _value = value;
            _bound = true;
            return true;
        }

        public void SetValue(float value)
        {
            SetValue(value, 0f, 0f);
        }
        public void SetValue(float value, float dt, float slew)
        { _value = _sharePlayer ? _player.ApplyAutomatic(value, dt, slew) : value; }

        // 返回是否成功摘除：失败时调用方要记一条警告，不能让日志谎报"已交还"。
        public bool Unbind()
        {
            string error;
            bool ok = true;

            if (!_bound)
            {
                ResetFields();
                return true;
            }

            if (!SetOverride(_controls, _input, null, ChannelName(_channel), out error))
            {
                ok = false;
            }

            ResetFields();
            return ok;
        }

        public static string ChannelName(int channel)
        {
            if (channel == ChannelPitch)
            {
                return "俯仰";
            }

            return channel == ChannelRoll ? "滚转" : "偏航";
        }

        private float? GetValue()
        {
            float axis;
            string error;

            if (_sharePlayer)
            {
                // Recheck when the game consumes the channel, irrespective of Update order.
                if (!RefreshPlayerInput(out error)) return 0f;
                return _player.Manual ? _player.Output : _value;
            }
            if (GameApi.TryReadInputAxis(_input, out axis, out error))
            {
                _lastInputAxis = axis;
            }

            return _value;
        }

        private static bool SetOverride(object controls, object input, Func<float?> func, string channel, out string error)
        {
            object result;

            if (!Reflect.TryCall(
                    controls,
                    GameApi.AircraftControlsType,
                    GameApi.SetInputOverrideMethod,
                    2,
                    new object[] { input, func },
                    out result,
                    out error))
            {
                error = channel + "：" + error;
                return false;
            }

            error = null;
            return true;
        }

        private void ResetFields()
        {
            _controls = null;
            _input = null;
            _func = null;
            _value = 0f;
            _channel = 0;
            _bound = false;
            _lastInputAxis = 0f;
            _sharePlayer = _playerOnly = false;
            PlayerInputError = null;
            _player.Reset();
        }
    }
}
