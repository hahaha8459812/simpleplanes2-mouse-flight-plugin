using UnityEngine;
using UnityEngine.UI;

namespace SimplePlanes2MouseFlight
{
    // IMGUI consumes its own events, but cannot consume the game's Canvas/EventSystem raycasts.
    // A transparent topmost Graphic covers only this panel, including its folded title bar.
    internal sealed class PanelPointerShield
    {
        private GameObject _root;
        private RectTransform _rect;
        private Rect _pressRect;
        private bool _pressed;
        private int _releaseFrame = -1;
        private Rect _region;
        public bool CapturesPointer
        {
            get
            {
                if (_root == null || !_root.activeSelf || !Application.isFocused || !Cursor.visible) return false;
                Vector3 mouse = Input.mousePosition;
                return _pressed || (_releaseFrame >= 0 && Time.frameCount <= _releaseFrame + 1)
                    || _region.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            }
        }
        public void Update(Rect panel, bool visible, bool dragging)
        {
            bool canUse = visible && Application.isFocused && Cursor.visible;
            Vector3 mouse = Input.mousePosition;
            bool over = canUse && panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            bool buttons = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (over && buttons && !_pressed) { _pressed = true; _pressRect = panel; }
            if (_pressed && !buttons) { _pressed = false; _releaseFrame = Time.frameCount; }
            if (!canUse) { _pressed = false; _releaseFrame = -1; }
            bool retainRelease = canUse && _releaseFrame >= 0 && Time.frameCount <= _releaseFrame + 1;
            FlightInputPatch.PanelPointerCaptured = canUse && (over || _pressed || retainRelease || dragging);
            if (!canUse) { if (_root != null) _root.SetActive(false); return; }
            EnsureCreated(); _root.SetActive(true);
            // Retain the original press area through release, even if the header folded mid-click.
            Rect region = (_pressed || retainRelease) ? Union(panel, _pressRect) : panel;
            _region = region;
            _rect.anchoredPosition = new Vector2(region.x, -region.y);
            _rect.sizeDelta = new Vector2(region.width, region.height);
        }
        private static Rect Union(Rect a, Rect b)
        {
            float x = Mathf.Min(a.xMin, b.xMin), y = Mathf.Min(a.yMin, b.yMin);
            return new Rect(x, y, Mathf.Max(a.xMax, b.xMax) - x, Mathf.Max(a.yMax, b.yMax) - y);
        }
        private void EnsureCreated()
        {
            if (_root != null) return;
            _root = new GameObject("MouseFlightPanelPointerShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32767;
            GameObject surface = new GameObject("PanelPointerSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            surface.transform.SetParent(_root.transform, false);
            _rect = surface.GetComponent<RectTransform>();
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0, 1);
            Image image = surface.GetComponent<Image>();
            image.color = new Color(0, 0, 0, 0); image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
        }
        public void Dispose()
        {
            FlightInputPatch.PanelPointerCaptured = false;
            _pressed = false; _releaseFrame = -1;
            if (_root != null) Object.Destroy(_root);
            _root = null; _rect = null;
        }
    }
}
