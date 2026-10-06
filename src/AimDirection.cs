using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 瞄准方向：一个固定在世界空间的单位向量（方案 5.2 / 5.4）。
    //
    // 为什么不是屏幕坐标、也不是 (-1,1) 的偏移：鼠标增量是设备位移量，把它按角度加到
    // 一个世界方向向量上，屏幕分辨率与 FOV 都不会进入这个式子，换分辨率手感不变。
    // 参考实现（MouseFlight / MouseAimFlight）都是这么做的，见方案 5.4。
    internal sealed class AimDirection
    {
        private Vector3 _direction = Vector3.forward;
        private Vector3 _nose = Vector3.forward;
        private float _lastMouseMagnitude;
        private float _pixelsPerUnit;
        private float _limitX;
        private float _limitY;
        private bool _screenLimitClamped;

        // 当前瞄准方向（世界空间单位向量）。
        public Vector3 Direction
        {
            get { return _direction; }
        }

        // 机头方向：供可选的回中与诊断（偏角）用，HUD 的机头标记也取它。

        // 瞄准方向相对机头的偏角（度）。
        public float AngleFromNose
        {
            get { return Vector3.Angle(_nose, _direction); }
        }

        // 上一次鼠标增量的模长，用于诊断。
        public float LastMouseMagnitude
        {
            get { return _lastMouseMagnitude; }
        }

        // 是否有限制生效（屏幕矩形限制由插件每帧设置）。
        public bool ScreenLimitActive
        {
            get { return _pixelsPerUnit > 0.0001f && _limitX > 0f && _limitY > 0f; }
        }

        // 上一帧是否真的被屏幕边界卡住了，用于诊断。
        public bool ScreenLimitClamped
        {
            get { return _screenLimitClamped; }
        }

        // 屏幕矩形限制：像素/单位换算比例，以及相对屏幕中心的允许像素偏移。
        public void SetScreenLimit(float pixelsPerUnit, float limitX, float limitY)
        {
            _pixelsPerUnit = pixelsPerUnit;
            _limitX = limitX;
            _limitY = limitY;
        }

        // 接管瞬间：瞄准方向对齐机头。
        public void Reset(Vector3 noseDirection)
        {
            _nose = Safe(noseDirection, Vector3.forward);
            _direction = _nose;
            _lastMouseMagnitude = 0f;
        }

        // 每帧更新机头方向（飞机姿态会变）。
        public void SetNose(Vector3 noseDirection)
        {
            _nose = Safe(noseDirection, _nose);
        }

        public bool IsOutsideView(Quaternion cameraRotation, float verticalFov, float aspect)
        {
            Vector3 local = Quaternion.Inverse(cameraRotation) * _direction;
            if (local.z <= 0f) return true;
            float halfHeight = local.z * Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Abs(local.y) > halfHeight || Mathf.Abs(local.x) > halfHeight * aspect;
        }

        // 按鼠标增量转动瞄准方向：以**相机自身的轴**为转轴，这样任何飞机姿态下
        // "鼠标往右 = 屏幕上的准星往右"都成立。
        // degreesPerUnit 的单位是"度 / 鼠标单位"，与像素和屏幕尺寸无关。
        public void ApplyMouse(float dx, float dy, Quaternion cameraRotation, float degreesPerUnit, float deadzone)
        {
            float mx = Mathf.Abs(dx) < deadzone ? 0f : dx;
            float my = Mathf.Abs(dy) < deadzone ? 0f : dy;

            _lastMouseMagnitude = Mathf.Sqrt(mx * mx + my * my);

            if (mx == 0f && my == 0f)
            {
                return;
            }

            Vector3 right = cameraRotation * Vector3.right;
            Vector3 up = cameraRotation * Vector3.up;

            // 鼠标往右（mx>0）：绕相机上轴正向转 → 瞄准方向向右。
            _direction = Quaternion.AngleAxis(mx * degreesPerUnit, up) * _direction;
            // 鼠标往上（my>0）：绕相机右轴**负向**转 → 瞄准方向向上
            //（Unity 里绕 +X 正向转是俯冲方向，所以这里取负）。
            _direction = Quaternion.AngleAxis(-my * degreesPerUnit, right) * _direction;
            _direction = Safe(_direction, _nose);
        }

        // 诊断键用：把瞄准方向绕世界竖直轴转一个角度。
        public void RotateYaw(float degrees)
        {
            _direction = Quaternion.AngleAxis(degrees, Vector3.up) * _direction;
            _direction = Safe(_direction, _nose);
        }

        // 屏幕空间限制：瞄准方向在屏幕上的投影不允许超出画面，最大只能到屏幕边缘。
        // 超出时按同一比例把方向往相机轴线方向收（等价于在画面上沿半径方向拉回边界），
        // 所以拉到底就是"贴住屏幕边缘"，不会跑到画面外，也不会有角度死板的手感。
        public void ClampToScreen(Quaternion cameraRotation)
        {
            _screenLimitClamped = false;

            if (!ScreenLimitActive)
            {
                return;
            }

            Vector3 local = Quaternion.Inverse(cameraRotation) * _direction;

            if (local.z <= 0.0001f)
            {
                // 方向跑到相机背后（正常路径下不会出现，因为每帧都夹）：拉到最靠边的方向上。
                Vector3 flat = new Vector3(local.x, local.y, 0f);

                if (flat.sqrMagnitude < 1e-8f)
                {
                    flat = Vector3.forward;
                }

                flat.Normalize();

                float t = Mathf.Min(
                    _limitX / _pixelsPerUnit / Mathf.Max(Mathf.Abs(flat.x), 0.0001f),
                    _limitY / _pixelsPerUnit / Mathf.Max(Mathf.Abs(flat.y), 0.0001f));

                _direction = Safe(cameraRotation * new Vector3(flat.x * t, flat.y * t, 1f).normalized, _nose);
                _screenLimitClamped = true;
                return;
            }

            float pixelX = local.x / local.z * _pixelsPerUnit;
            float pixelY = local.y / local.z * _pixelsPerUnit;

            float scale = 1f;

            if (Mathf.Abs(pixelX) > _limitX)
            {
                scale = Mathf.Min(scale, _limitX / Mathf.Abs(pixelX));
            }

            if (Mathf.Abs(pixelY) > _limitY)
            {
                scale = Mathf.Min(scale, _limitY / Mathf.Abs(pixelY));
            }

            if (scale >= 1f)
            {
                return;
            }

            Vector3 clampedLocal = new Vector3(local.x * scale, local.y * scale, local.z).normalized;
            _direction = Safe(cameraRotation * clampedLocal, _nose);
            _screenLimitClamped = true;
        }

        // 可选辅助：瞄准方向按时间常数缓慢转回机头。seconds<=0 表示关闭。
        public void Recenter(float deltaTime, float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-deltaTime / seconds);
            _direction = Vector3.Slerp(_direction, _nose, t);
            _direction = Safe(_direction, _nose);
        }

        private static Vector3 Safe(Vector3 value, Vector3 fallback)
        {
            if (value.sqrMagnitude < 1e-8f)
            {
                return fallback.sqrMagnitude < 1e-8f ? Vector3.forward : fallback.normalized;
            }

            return value.normalized;
        }
    }
}
