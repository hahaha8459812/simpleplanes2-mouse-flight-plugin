using BepInEx.Configuration;
using System;

namespace SimplePlanes2MouseFlight
{
    internal sealed class FlightPanelSettings
    {
        private readonly ConfigFile _config;
        private bool _savePending;
        private readonly ConfigEntry<string> _language;
        private readonly ConfigEntry<bool> _lockButton, _lockPanel;
        private readonly ConfigEntry<float> _buttonX, _buttonY, _panelX, _panelY, _width, _height, _buttonSize;
        public FlightPanelSettings(ConfigFile config)
        {
            _config = config;
            _language = config.Bind("Panel", "Language", "zh", "面板语言：zh / en。Panel language.");
            _lockButton = config.Bind("Panel", "LockButton", false, "锁定入口按钮位置。Lock launcher position.");
            _lockPanel = config.Bind("Panel", "LockPanel", false, "锁定面板位置。Lock panel position.");
            _buttonX = config.Bind("Panel", "ButtonX", 16f, "入口按钮横坐标。");
            _buttonY = config.Bind("Panel", "ButtonY", 16f, "入口按钮纵坐标。");
            _panelX = config.Bind("Panel", "PanelX", 80f, "面板横坐标，与按钮独立。");
            _panelY = config.Bind("Panel", "PanelY", 80f, "面板纵坐标，与按钮独立。");
            _width = config.Bind("Panel", "Width", 820f, "面板宽度，480~1200 px。");
            _height = config.Bind("Panel", "Height", 680f, "面板高度，480~1000 px。");
            _buttonSize = config.Bind("Panel", "ButtonSize", 52f, "入口按钮大小，36~96 px。");
        }
        public bool English { get { return _language.Value == "en"; } set { Set(_language, value ? "en" : "zh"); } }
        public bool LockButton { get { return _lockButton.Value; } set { Set(_lockButton, value); } }
        public bool LockPanel { get { return _lockPanel.Value; } set { Set(_lockPanel, value); } }
        public float ButtonX { get { return Safe(_buttonX.Value, 0, 10000, 16); } set { Set(_buttonX, value); } }
        public float ButtonY { get { return Safe(_buttonY.Value, 0, 10000, 16); } set { Set(_buttonY, value); } }
        public float PanelX { get { return Safe(_panelX.Value, 0, 10000, 80); } set { Set(_panelX, value); } }
        public float PanelY { get { return Safe(_panelY.Value, 0, 10000, 80); } set { Set(_panelY, value); } }
        public float Width { get { return Safe(_width.Value, 480, 1200, 820); } set { Set(_width, Safe(value, 480, 1200, 820)); } }
        public float Height { get { return Safe(_height.Value, 480, 1000, 680); } set { Set(_height, Safe(value, 480, 1000, 680)); } }
        public float ButtonSize { get { return Safe(_buttonSize.Value, 36, 96, 52); } set { Set(_buttonSize, Safe(value, 36, 96, 52)); } }
        public void Save() { _savePending=true; }
        public void Flush() { if(_savePending) { _config.Save(); _savePending=false; } }
        private void Set<T>(ConfigEntry<T> entry,T value)
        {
            bool automatic=_config.SaveOnConfigSet;
            _config.SaveOnConfigSet=false;
            try { entry.Value=value; } finally { _config.SaveOnConfigSet=automatic; }
        }
        private static float Safe(float value, float min, float max, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value)); }
    }
}
