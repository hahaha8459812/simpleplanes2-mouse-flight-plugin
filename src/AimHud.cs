using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    // 瞄准 HUD（方案 10.1）。三个元素：
    //   飞控准星——大环，画在瞄准方向的屏幕位置上；
    //   机头指向——小十字，横臂比竖臂长，跟着机身滚转；
    //   连线——从飞控准星到屏幕中心的虚线，镜头追上后消失。
    //
    // 用 IMGUI 画：只在 EventType.Repaint 时绘制；贴图在第一次绘制时程序生成、之后复用；
    // 尺寸全部按屏幕高度取比例，换分辨率观感一致。投影与状态更新都在 Update 里做，
    // 这里只负责画（共享文档 in-game-ui-panel.md 的约定）。
    internal sealed class AimHud
    {
        private const int TextureSize = 64;

        private readonly PluginSettings _settings;
        private readonly PluginLog _log;

        private Texture2D _ringTexture;
        private Texture2D _crossTexture;
        private Texture2D _dashTexture;
        private bool _texturesReady;
        private bool _loggedFirstDraw;

        private float _lastLinkLength;
        private int _lastDrawnFrame = -1;

        public AimHud(PluginSettings settings, PluginLog log)
        {
            _settings = settings;
            _log = log;
        }



        public void ResetSession()
        {
            _loggedFirstDraw = false;
        }

        public void Dispose()
        {
            if (_ringTexture != null)
            {
                UnityEngine.Object.Destroy(_ringTexture);
                _ringTexture = null;
            }

            if (_crossTexture != null)
            {
                UnityEngine.Object.Destroy(_crossTexture);
                _crossTexture = null;
            }

            if (_dashTexture != null)
            {
                UnityEngine.Object.Destroy(_dashTexture);
                _dashTexture = null;
            }

            _texturesReady = false;
        }

        public void Draw(Camera camera, Vector3 aimDirection, Vector3 noseDirection, Vector3 aircraftUp)
        {
            if (camera == null || Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_lastDrawnFrame == Time.frameCount)
            {
                return;
            }

            _lastDrawnFrame = Time.frameCount;

            EnsureTextures();

            float screenHeight = Screen.height;
            float ringRadius = screenHeight * _settings.CrosshairRadiusRatio * 1.5f;
            float crossRadius = screenHeight * _settings.NoseMarkerSizeRatio;

            _lastLinkLength = 0f;

            Vector3 crosshair;
            bool crosshairClamped;
            ComputeScreenPoint(camera, aimDirection, out crosshair, out crosshairClamped);

            Vector3 nose;
            bool noseClamped;
            ComputeScreenPoint(camera, noseDirection, out nose, out noseClamped);


            // 连线画在最下面，免得虚线压住准星。
            // 两端是"机头指向"与"飞控准星"：这条线的长度就是当前的瞄准偏差，
            // 也就是教官正在消除的那个偏移量，比连到屏幕中心更有意义。
            if (_settings.ShowLinkLine)
            {
                _lastLinkLength = Vector2.Distance(crosshair, nose);
                DrawDashedLine(crosshair, nose, screenHeight, _settings.ShowCrosshair ? ringRadius : 0, _settings.ShowNoseMarker ? crossRadius : 0);
            }

            if (_settings.ShowCrosshair)
            {
                // 方向在画面外时贴边显示并降低不透明度，读作"往那边转能追回来"。
                DrawCentered(crosshair, ringRadius * 2f, _ringTexture, 0f, crosshairClamped ? 0.45f : 1f);
            }

            if (_settings.ShowNoseMarker)
            {
                DrawCentered(nose, crossRadius * 2f, _crossTexture,
                    ScreenRollDegrees(camera, aircraftUp), noseClamped ? 0.45f : 1f);
            }

            if (!_loggedFirstDraw)
            {
                _loggedFirstDraw = true;
                _log.Info("准星开始绘制：屏幕 " + Screen.width + "×" + Screen.height
                    + "，飞控准星=" + Format(crosshair) + (crosshairClamped ? "（贴边）" : "")
                    + "，机头标记=" + Format(nose) + (noseClamped ? "（贴边）" : "")
                    + "，连线长度=" + _lastLinkLength.ToString("0") + " 像素"
                    + "，大环半径=" + ringRadius.ToString("0") + " 像素"
                    + "，小十字尺寸=" + crossRadius.ToString("0") + " 像素。");
            }
        }

        // 方向 → IMGUI 屏幕坐标。
        //
        // 用相机本地坐标直接算，而不是 WorldToScreenPoint：这样瞄准方向转到相机背后时也能得到一个
        // 有限的屏幕点，可以贴到画面边缘显示。超出画面时 clampToEdge 为真，调用方按"画面外指示"处理。
        private bool ComputeScreenPoint(Camera camera, Vector3 direction, out Vector3 guiPoint, out bool clamped)
        {
            guiPoint = Vector3.zero;
            clamped = false;

            Vector3 local = camera.transform.InverseTransformDirection(direction.normalized);

            // 相机本地 x 右、y 上、z 前；透视投影：屏幕上偏移 = (x/z, y/z) × 每单位像素数。
            float z = local.z;
            Vector2 planeOffset;

            if (z > 0.001f)
            {
                planeOffset = new Vector2(local.x, local.y) / z;
            }
            else
            {
                // 在背后：用 |z| 保持数值有限，并把方向翻到相反侧，读起来才像"往那边转能把它转回来"。
                float safeZ = Mathf.Max(0.001f, -z);
                planeOffset = new Vector2(-local.x, -local.y) / safeZ;
            }

            float halfFovRad = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float pixelsPerUnit = (Screen.height * 0.5f) / Mathf.Tan(halfFovRad);

            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            float x = centerX + planeOffset.x * pixelsPerUnit;
            float y = centerY - planeOffset.y * pixelsPerUnit;

            float margin = Mathf.Max(8f, Screen.height * 0.02f);
            float minX = margin;
            float maxX = Screen.width - margin;
            float minY = margin;
            float maxY = Screen.height - margin;

            if (x < minX || x > maxX || y < minY || y > maxY)
            {
                clamped = true;
                x = Mathf.Clamp(x, minX, maxX);
                y = Mathf.Clamp(y, minY, maxY);
            }

            guiPoint = new Vector3(x, y, 0f);
            return true;
        }

        // 机身在屏幕上的滚转角（度，顺时针为正）。用相机本地坐标算，不用做投影。
        private static float ScreenRollDegrees(Camera camera, Vector3 aircraftUp)
        {
            Vector3 localUp = camera.transform.InverseTransformDirection(aircraftUp);
            return Mathf.Atan2(localUp.x, localUp.y) * Mathf.Rad2Deg;
        }

        private void DrawCentered(Vector3 center, float size, Texture2D texture, float rotationDegrees, float alpha)
        {
            Rect rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            Color savedColor = GUI.color;

            GUI.color = new Color(1f, 1f, 1f, alpha);

            if (Mathf.Abs(rotationDegrees) < 0.01f)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            }
            else
            {
                Matrix4x4 savedMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(rotationDegrees, new Vector2(center.x, center.y));
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
                GUI.matrix = savedMatrix;
            }

            GUI.color = savedColor;
        }

        private void DrawDashedLine(Vector2 from, Vector2 to, float screenHeight, float ringRadius, float noseRadius)
        {
            float length = Vector2.Distance(from, to);
            if (length < 4f)
            {
                return;
            }

            // 短划长度固定，间隙随连线变长而变大：线越长偏差越大，虚线就越"散"，
            // 观感上能直接读出"镜头还差多少没追上"。
            float baseStep = screenHeight * _settings.DashSpacingRatio;
            if (baseStep < 4f)
            {
                baseStep = 4f;
            }

            float dash,gap;
            CameraViewMath.DashPattern(baseStep,length,screenHeight,out dash,out gap);
            float step = dash + gap;

            float dashHeight = Mathf.Max(2f, screenHeight * 0.0025f);
            Vector2 direction = (to - from) / length;
            float start,end;
            if(!CameraViewMath.LinkInterval(length,ringRadius>0?ringRadius+dashHeight:0,noseRadius>0?noseRadius+dashHeight:0,out start,out end)) return;

            // 短划随时间向前滚动，读起来有"从这里指向屏幕中心"的方向感。
            float offset = (Time.unscaledTime * 60f) % step;

            for (float t = start + offset; t < end; t += step)
            {
                Vector2 point = from + direction * t;
                float segment = Mathf.Min(dash, end - t);

                Rect rect = new Rect(
                    point.x,
                    point.y - dashHeight * 0.5f,
                    segment,
                    dashHeight);

                Matrix4x4 saved = GUI.matrix;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, new Vector2(point.x, point.y));
                GUI.DrawTexture(rect, _dashTexture, ScaleMode.StretchToFill, true);
                GUI.matrix = saved;
            }
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("0") + ", " + value.y.ToString("0") + ")";
        }

        private void EnsureTextures()
        {
            if (_texturesReady)
            {
                return;
            }

            _ringTexture = BuildRing();
            _crossTexture = BuildCross();
            _dashTexture = BuildSolid(8, 8, Color.white);
            _texturesReady = true;

            _log.Info("准星贴图已生成：大环 " + TextureSize + "×" + TextureSize
                + "、小十字（横臂长）、虚线短划。尺寸按屏幕高度比例缩放。");
        }

        private static Texture2D BuildRing()
        {
            Texture2D texture = NewTexture(TextureSize);
            Color[] pixels = new Color[TextureSize * TextureSize];

            float center = (TextureSize - 1) * 0.5f;
            float radius = TextureSize * 0.42f;
            float halfThickness = Mathf.Max(1.2f, TextureSize * 0.035f);

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    float edge = Mathf.Abs(distance - radius);
                    float alpha = Mathf.Clamp01(1f - (edge - halfThickness * 0.5f) / 1.4f);
                    pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // 横臂比竖臂长的小十字（用户定的外观）。
        private static Texture2D BuildCross()
        {
            Texture2D texture = NewTexture(TextureSize);
            Color[] pixels = new Color[TextureSize * TextureSize];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, 0f);
            }

            int center = (TextureSize - 1) / 2;
            int halfBar = Mathf.Max(1, (int)(TextureSize * 0.035f));
            int horizontalHalf = (int)(TextureSize * 0.44f);
            int verticalHalf = (int)(TextureSize * 0.20f);

            for (int y = -halfBar; y <= halfBar; y++)
            {
                for (int x = -horizontalHalf; x <= horizontalHalf; x++)
                {
                    SetPixel(pixels, center + x, center + y, 1f);
                }
            }

            for (int y = -verticalHalf; y <= verticalHalf; y++)
            {
                for (int x = -halfBar; x <= halfBar; x++)
                {
                    SetPixel(pixels, center + x, center + y, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void SetPixel(Color[] pixels, int x, int y, float alpha)
        {
            if (x < 0 || y < 0 || x >= TextureSize || y >= TextureSize)
            {
                return;
            }

            pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
        }

        private static Texture2D BuildSolid(int width, int height, Color color)
        {
            Texture2D texture = NewTexture(width, height);
            Color[] pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D NewTexture(int size)
        {
            return NewTexture(size, size);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
    }
}
