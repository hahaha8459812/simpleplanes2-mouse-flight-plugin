using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // P0 的测试按键：一发一次短脉冲，不做长期接管。
    // 长期接管会让游戏停止读取玩家自己的操作，测试时容易摔飞机，所以 P0 不提供这种模式。
    internal sealed class TestHotkeys
    {
        private readonly PluginSettings _settings;

        private bool _parsed;
        private string _parsedCameraKey;
        private bool _parsedCameraEnabled;
        private KeyCode _rollKey = KeyCode.None;
        private KeyCode _pitchKey = KeyCode.None;
        private KeyCode _yawKey = KeyCode.None;
        private KeyCode _cameraKey = KeyCode.None;
        private KeyCode _cameraYawKey = KeyCode.None;

        public TestHotkeys(PluginSettings settings)
        {
            _settings = settings;
        }

        public bool RollPulsePressed { get; private set; }

        public bool PitchPulsePressed { get; private set; }

        public bool YawPulsePressed { get; private set; }

        public bool CameraTogglePressed { get; private set; }

        public bool CameraYawPressed { get; private set; }
        public bool FreeLookHeld { get; private set; }




        public void Update()
        {
            EnsureParsed();

            RollPulsePressed = _rollKey != KeyCode.None && Input.GetKeyDown(_rollKey);
            PitchPulsePressed = _pitchKey != KeyCode.None && Input.GetKeyDown(_pitchKey);
            YawPulsePressed = _yawKey != KeyCode.None && Input.GetKeyDown(_yawKey);
            CameraTogglePressed = _cameraKey != KeyCode.None && Input.GetKeyDown(_cameraKey);
            CameraYawPressed = _cameraYawKey != KeyCode.None && Input.GetKeyDown(_cameraYawKey);
            FreeLookHeld = FlightInputPatch.FreeLookHeld;
        }

        private void EnsureParsed()
        {
            if (_parsed && _parsedCameraKey == _settings.CameraTestKey && _parsedCameraEnabled == _settings.FlightHotkeyEnabled)
            {
                return;
            }

            _parsed = true;
            _parsedCameraKey = _settings.CameraTestKey;
            _parsedCameraEnabled = _settings.FlightHotkeyEnabled;
            _rollKey = KeyUtil.Parse(_settings.PulseRollKey, KeyCode.None);
            _pitchKey = KeyUtil.Parse(_settings.PulsePitchKey, KeyCode.None);
            _yawKey = KeyUtil.Parse(_settings.PulseYawKey, KeyCode.None);
            _cameraKey = _settings.FlightHotkeyEnabled ? KeyUtil.Parse(_settings.CameraTestKey, KeyCode.None) : KeyCode.None;
            _cameraYawKey = KeyUtil.Parse(_settings.CameraYawStepKey, KeyCode.None);
        }
    }
}
