using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 鼠标读取（方案 5.1）。接管期间锁定并隐藏系统光标，按帧读增量。
    //
    // 两条路径：
    //   轴增量（默认）：Input.GetAxisRaw("Mouse X"/"Mouse Y")。前提是这两个轴在 SP2 里可用。
    //   位置差分（退路）：锁定光标后 mousePosition 不会移动，所以这条路径下不锁光标，
    //                     改用 CursorLockMode.Confined，并对 mousePosition 做帧间差分。
    // 实际走了哪条路径会写进日志，便于判断"鼠标没反应"到底卡在哪。
    internal sealed class MouseReader
    {
        private readonly PluginSettings _settings;
        private readonly PluginLog _log;

        private bool _active;
        private CursorLockMode _savedLockState;
        private bool _savedVisible;
        private bool _stateSaved;

        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        private int _zeroAxisFrames;
        private bool _reportedCursorFight;
        private bool _freeLook;
        private int _skipReadFrame = -1;
        private bool _warnedZeroAxis;
        private float _nextSampleLogTime;

        public MouseReader(PluginSettings settings, PluginLog log)
        {
            _settings = settings;
            _log = log;
        }

        public bool IsActive
        {
            get { return _active; }
        }

        public string ModeText
        {
            get { return _settings.UseMousePositionDelta ? "位置差分" : "轴增量"; }
        }

        public void Begin()
        {
            if (_active)
            {
                return;
            }

            _savedLockState = Cursor.lockState;
            _savedVisible = Cursor.visible;
            _stateSaved = true;

            if (_settings.UseMousePositionDelta)
            {
                // 位置差分需要光标能移动，所以只限制在窗口内、不锁定。
                Cursor.lockState = CursorLockMode.Confined;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
            }

            Cursor.visible = false;

            _active = true;
            _freeLook = false;
            _skipReadFrame = Time.frameCount;
            _hasLastPosition = false;
            _zeroAxisFrames = 0;
            _warnedZeroAxis = false;
            _reportedCursorFight = false;
            _nextSampleLogTime = 0f;

            _log.Info("鼠标已接管：读取方式=" + ModeText
                + "，光标隐藏（原状态 " + _savedLockState + " / visible=" + _savedVisible + "）。");
        }

        public void End()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _freeLook = false;
            _hasLastPosition = false;
            _skipReadFrame = -1;

            if (_stateSaved)
            {
                Cursor.lockState = _savedLockState;
                Cursor.visible = _savedVisible;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            _stateSaved = false;

            _log.Info("鼠标已交还：光标状态恢复为 " + Cursor.lockState + " / visible=" + Cursor.visible + "。");
        }

        // 每帧调用：游戏自己也管光标（InteractiveCameraController.UpdateCursor），
        // 一旦它把光标显示回来或改了锁定状态，这里立刻改回去。
        // 自由视角（M4 第一步）：按住期间把鼠标交还玩家（显示光标、解除锁定），
        // 准星保持当前朝向不变。松开后 Tick 会重新抢回光标并隐藏，不需要在这里恢复。
        public void SetFreeLook(bool on)
        {
            if (_freeLook == on)
            {
                return;
            }

            _freeLook = on;
            _hasLastPosition = false;
            _skipReadFrame = Time.frameCount;
            Tick();
            _log.Info(on ? "自由视角开始：鼠标已交还。" : "自由视角结束：鼠标重新锁定并隐藏。");
        }

        public void Tick()
        {
            if (!_active)
            {
                return;
            }

            if (_freeLook)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            CursorLockMode want = _settings.UseMousePositionDelta
                ? CursorLockMode.Confined
                : CursorLockMode.Locked;

            bool fixedSomething = false;

            if (Cursor.visible)
            {
                Cursor.visible = false;
                fixedSomething = true;
            }

            if (Cursor.lockState != want)
            {
                Cursor.lockState = want;
                fixedSomething = true;
            }

            if (fixedSomething && !_reportedCursorFight)
            {
                _reportedCursorFight = true;
                _log.Info("游戏把光标状态改了回来，已重新隐藏并锁定（期望 " + want + "）。");
            }
        }

        public bool TryRead(out float dx, out float dy)
        {
            dx = 0f;
            dy = 0f;

            if (!_active || _freeLook || _skipReadFrame == Time.frameCount)
            {
                return false;
            }

            if (_settings.UseMousePositionDelta)
            {
                return TryReadPositionDelta(out dx, out dy);
            }

            return TryReadAxisDelta(out dx, out dy);
        }

        private bool TryReadAxisDelta(out float dx, out float dy)
        {
            dx = Input.GetAxisRaw("Mouse X");
            dy = Input.GetAxisRaw("Mouse Y");

            bool zero = Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dy) < 0.0001f;

            if (zero)
            {
                _zeroAxisFrames++;

                // 连续多帧读不到、且这段期间玩家动过鼠标，就提示切到位置差分。
                if (!_warnedZeroAxis && _zeroAxisFrames > 300 && Input.mousePosition != _lastPosition)
                {
                    _warnedZeroAxis = true;
                    _log.Warn("鼠标轴连续 " + _zeroAxisFrames
                        + " 帧读数都是 0，但光标位置在变：这两个轴在 SP2 里可能被占用。"
                        + "把配置项 Mouse/UseMousePositionDelta 改成 true 可以改用位置差分。");
                }
            }
            else
            {
                _zeroAxisFrames = 0;
            }

            _lastPosition = Input.mousePosition;

            if (Time.unscaledTime >= _nextSampleLogTime)
            {
                _nextSampleLogTime = Time.unscaledTime + 3f;
                _log.Info("鼠标读数（轴增量）：dx=" + dx.ToString("0.000") + " dy=" + dy.ToString("0.000")
                    + "，锁定状态=" + Cursor.lockState + "，光标可见=" + Cursor.visible);
            }

            return true;
        }

        private bool TryReadPositionDelta(out float dx, out float dy)
        {
            Vector3 current = Input.mousePosition;

            if (!_hasLastPosition)
            {
                _lastPosition = current;
                _hasLastPosition = true;
                dx = 0f;
                dy = 0f;
                return true;
            }

            dx = current.x - _lastPosition.x;
            dy = current.y - _lastPosition.y;
            _lastPosition = current;

            if (Time.unscaledTime >= _nextSampleLogTime)
            {
                _nextSampleLogTime = Time.unscaledTime + 3f;
                _log.Info("鼠标读数（位置差分）：dx=" + dx.ToString("0.0") + " dy=" + dy.ToString("0.0")
                    + "，光标位置=" + current.x.ToString("0") + "," + current.y.ToString("0"));
            }

            return true;
        }
    }
}
