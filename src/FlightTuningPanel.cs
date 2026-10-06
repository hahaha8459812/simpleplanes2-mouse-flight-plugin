using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SimplePlanes2MouseFlight
{
    internal sealed class FlightTuningPanel
    {
        private readonly PluginSettings _settings;
        private readonly CraftTuning _craft;
        private readonly Action _release;
        private readonly Func<bool> _resume;
        private readonly Action<string> _record;
        private readonly Func<bool> _active;
        private readonly FlightPanelSettings _ui;
        private readonly FlightMonitor _monitor = new FlightMonitor();
        private readonly PanelPointerShield _shield = new PanelPointerShield();
        private readonly PanelPointerShield _buttonShield = new PanelPointerShield();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private Dictionary<string, float> _draft;
        private readonly Dictionary<string, string> _text = new Dictionary<string, string>();
        private readonly string[] _groups = { "俯仰", "滚转", "偏航", "通用" };
        private int _group, _selected;
        private Vector2 _scroll;
        private string _notice = "";
        private bool _resumePending;
        private bool _clearFocus, _togglePending;
        private bool _dirty, _dragging, _buttonDragging, _buttonMoved, _disablePending;
        private int _page;
        private int _pendingPage=-1, _pendingGroup=-1;
        private int _bindingKey, _pendingBinding;
        private KeyCode _pendingKey = KeyCode.None, _releaseKey = KeyCode.None;
        private int _keyReleaseFrame;
        private Vector2 _settingsScroll;
        public bool BlockHotkeys { get { return _bindingKey != 0 || _pendingBinding != 0 || _releaseKey != KeyCode.None || Time.frameCount <= _keyReleaseFrame; } }
        private Vector2 _buttonPosition, _buttonDragOffset, _buttonPress;
        private Vector2 _position = new Vector2(16, 16), _dragOffset;
        private Font _font;
        private GUIStyle _label, _button, _input, _selectedStyle, _primary, _muted, _title, _row, _rowAlt, _launchTop, _launchBottom, _readout, _small;
        private Texture2D _launchTexture, _launchActive, _dot;
        public bool Expanded { get; private set; }
        public bool Available { get; private set; }
        public bool Editing { get; private set; }

        public FlightTuningPanel(PluginSettings settings, CraftTuning craft, Action release, Func<bool> resume, Action<string> record, Func<bool> active, FlightPanelSettings ui)
        {
            _settings = settings; _craft = craft; _release = release; _resume = resume; _record = record;
            _active = active; _ui = ui;
            _position = new Vector2(ui.PanelX, ui.PanelY); _buttonPosition = new Vector2(ui.ButtonX, ui.ButtonY);
            FlightInputPatch.PanelPointerCapture = delegate { return _shield.CapturesPointer || _buttonShield.CapturesPointer; };
        }
        public void Update(bool allowed)
        {
            if (_releaseKey != KeyCode.None && !Input.GetKey(_releaseKey)) _releaseKey = KeyCode.None;
            if (_pendingBinding != 0)
            {
                if (_pendingBinding == 1) _settings.OverlayKey = _pendingKey.ToString();
                else _settings.CameraTestKey = _pendingKey.ToString();
                _releaseKey = _pendingKey; _keyReleaseFrame = Time.frameCount + 1;
                _pendingBinding = 0;
            }
            if(!Input.GetMouseButton(0) || !Application.isFocused) _ui.Flush();
            Available = allowed && _craft.Ready && _settings.Enabled;
            if (!Available || !Application.isFocused)
            {
                if(!_craft.Ready || !_settings.Enabled) Cancel();
                else { _resumePending=_disablePending=_togglePending=_dragging=_buttonDragging=Editing=false; _pendingPage=_pendingGroup=-1; }
                _bindingKey = 0;
                UpdateShields(false); return;
            }
            if(_pendingPage>=0) { _page=_pendingPage; _pendingPage=-1; _bindingKey=0; _clearFocus=true; Editing=false; }
            if(_pendingGroup>=0) { _group=_pendingGroup; _pendingGroup=-1; _selected=0; _scroll=Vector2.zero; _clearFocus=true; }
            if (_togglePending || (_settings.PanelHotkeyEnabled && !BlockHotkeys && Input.GetKeyDown(KeyUtil.Parse(_settings.OverlayKey, KeyCode.KeypadMultiply))))
            { _togglePending = false; if (Expanded) Collapse(); else Expand(); }
            if (_resumePending && !Input.GetMouseButton(0) && !Input.GetMouseButton(1)) Resume();
            if (_disablePending) { _disablePending = false; _release(); }
            FlightInputPatch.PanelOpen = false;
            if (Expanded && !_active())
            { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            ClampPosition(); UpdateShields(true);
        }
        public void Sample(bool active, Vector3 aim) { _monitor.Update(_craft.Ready && _settings.Enabled, active, aim); }
        private void UpdateShields(bool visible)
        {
            _shield.Update(PanelRect(), visible && Expanded, _dragging);
            _buttonShield.Update(ButtonRect(), visible, _buttonDragging);
            FlightInputPatch.PanelPointerCaptured = _shield.CapturesPointer || _buttonShield.CapturesPointer;
        }
        private void Expand()
        {
            if (!Available || Expanded) return;
            if (_draft == null) { _draft = _craft.EditValues(); FillText(); _dirty = false; }
            _notice = "保存本次参数不会折叠。保存到飞机请复制参数并在设计器保存。";
            Expanded = true; FlightInputPatch.PanelOpen = false; _resumePending = false;
            _clearFocus = true;
            if (!_active()) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        public void Cancel()
        {
            Expanded = false; _resumePending = false; FlightInputPatch.PanelOpen = false;
            _draft = null; _text.Clear();
            _dirty = _dragging = _buttonDragging = _disablePending = Editing = false;
            _togglePending = false;
            _pendingPage=_pendingGroup=-1;
            _bindingKey = _pendingBinding = 0; _releaseKey = KeyCode.None;
            UpdateShields(false); _monitor.Reset();
        }
        private void Collapse()
        { Expanded = false; _bindingKey = 0; _resumePending = false; _dragging = Editing = false; FlightInputPatch.PanelOpen = false; }
        private void FillText()
        {
            _text.Clear();
            foreach (TuningField f in FlightTuning.Fields) _text[f.Name] = _draft[f.Name].ToString("R", CultureInfo.InvariantCulture);
        }
        private bool ReadDraft()
        {
            Dictionary<string, float> candidate = FlightTuning.Copy(_draft);
            foreach (TuningField f in FlightTuning.Fields)
            {
                float v;
                if (!float.TryParse(_text[f.Name], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                { _notice = f.Label + "：请输入有效数值"; return false; }
                candidate[f.Name] = v;
            }
            string error;
            if (!FlightTuning.Validate(candidate, out error)) { _notice = error; return false; }
            _draft = candidate; return true;
        }
        private void Save()
        {
            if (!ReadDraft()) return;
            string error;
            if (!_craft.Apply(_draft, out error)) { _notice = error; return; }
            _dirty = false; _record(_settings.DescribeEffectiveConfig());
            _notice = "已保存并应用到本次飞行，面板保持展开；飞机文件和全局配置未改写。";
        }
        private void Resume()
        {
            _resumePending = false;
            FlightInputPatch.PanelOpen = false;
            if (!_resume())
            {
                FlightInputPatch.PanelOpen = false;
                _release(); _notice = "飞控接管失败；请确认未暂停且没有游戏菜单打开。";
            }
        }
        private void Copy(bool differencesOnly)
        {
            if (!ReadDraft()) return;
            GUIUtility.systemCopyBuffer = FlightTuning.Export(_draft, _craft.BaselineValues(), differencesOnly);
            _notice = differencesOnly ? "已复制差异参数；未写入项目依赖接收方插件配置。" : "已复制完整参数，可粘贴到飞机任意一个零件名称并保存。";
        }
        private List<TuningField> Rows()
        {
            List<TuningField> rows = new List<TuningField>();
            foreach (TuningField f in FlightTuning.Fields) if (f.Group == _groups[_group]) rows.Add(f);
            return rows;
        }
        private void Adjust(TuningField f, int direction, bool coarse)
        {
            float v;
            if (!float.TryParse(_text[f.Name], NumberStyles.Float, CultureInfo.InvariantCulture, out v) || !FlightRecorder.Finite(v)) v = _draft[f.Name];
            if (f.Toggle) v = v == 0 ? 1 : 0;
            else if (f.Sign) v = v < 0 ? 1 : -1;
            else v = Mathf.Clamp(v + direction * f.Step * (coarse ? 10f : 1f), f.Min, f.Max);
            _text[f.Name] = v.ToString("0.######", CultureInfo.InvariantCulture);
            _dirty = true;
        }
        public void Draw()
        {
            if (!Available || !Application.isFocused) return;
            EnsureStyles(); int depth = GUI.depth; GUI.depth = -100;
            Color color = GUI.color, contentColor = GUI.contentColor, backgroundColor = GUI.backgroundColor;
            GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
            try
            {
                if (_clearFocus) { GUI.FocusControl(null); _clearFocus = false; }
                ClampPosition(); Rect rect = PanelRect();
                if (Expanded)
                {
                    Paint(new Rect(rect.x+5,rect.y+5,rect.width,rect.height),new Color(0,0,0,.32f));
                    Paint(rect,new Color(.025f,.032f,.044f,.98f));
                    Paint(new Rect(rect.x,rect.y,rect.width,42),new Color(.018f,.055f,.105f,1));
                    Paint(new Rect(rect.x,rect.y+41,rect.width,2),new Color(.2f,.45f,.75f,1));
                    bool launcherOwns=_buttonDragging || ButtonRect().Contains(Event.current.mousePosition);
                    bool enabled=GUI.enabled; GUI.enabled=enabled && Cursor.visible && !_resumePending && !launcherOwns;
                    if(GUI.Button(new Rect(rect.x+6,rect.y+6,34,30),"▼",_button)) _togglePending=true;
                    GUI.Label(new Rect(rect.x+50,rect.y+7,rect.width-60,28),L("鼠标飞控","Mouse Flight"),_title);
                    if(!launcherOwns) HandleDrag(new Rect(rect.x+46,rect.y,rect.width-46,42));
                    string[] pages={L("飞行监控","Flight Monitor"),L("调参","Tuning"),L("设置","Settings")};
                    for(int i=0;i<3;i++) if(GUI.Button(new Rect(rect.x+12+i*(rect.width-24)/3,rect.y+52,(rect.width-30)/3,32),pages[i],_page==i?_primary:_button)) { _pendingPage=i; GUI.FocusControl(null); }
                    Rect content=new Rect(rect.x+12,rect.y+94,rect.width-24,rect.height-106);
                    if(_page==0) DrawMonitor(content);
                    else { GUILayout.BeginArea(content); if(_page==1) DrawWindow(0); else DrawSettings(); GUILayout.EndArea(); }
                    GUI.enabled=enabled;
                }
                DrawLauncher();
                string focused=GUI.GetNameOfFocusedControl();
                Editing=Expanded && _page==1 && Cursor.visible && focused!=null && focused.StartsWith("mf-value-",StringComparison.Ordinal);
                UpdateShields(true);
                Event e = Event.current;
                if (Cursor.visible && e != null && (e.isMouse || e.type == EventType.ScrollWheel)
                    && ((Expanded && PanelRect().Contains(e.mousePosition)) || ButtonRect().Contains(e.mousePosition))) e.Use();
            }
            finally { GUI.depth = depth; GUI.color = color; GUI.contentColor = contentColor; GUI.backgroundColor = backgroundColor; }
        }
        private Rect PanelRect()
        { return new Rect(_position.x,_position.y,Mathf.Max(1,Mathf.Min(_ui.Width,Screen.width-16)),Mathf.Max(1,Mathf.Min(_ui.Height,Screen.height-16))); }
        private Rect ButtonRect() { return new Rect(_buttonPosition.x,_buttonPosition.y,_ui.ButtonSize,_ui.ButtonSize); }
        private void ClampPosition()
        {
            Rect rect = PanelRect();
            _position.x = Mathf.Clamp(_position.x, 0, Mathf.Max(0, Screen.width - rect.width));
            _position.y = Mathf.Clamp(_position.y, 0, Mathf.Max(0, Screen.height - rect.height));
            _buttonPosition.x=Mathf.Clamp(_buttonPosition.x,0,Mathf.Max(0,Screen.width-_ui.ButtonSize));
            _buttonPosition.y=Mathf.Clamp(_buttonPosition.y,0,Mathf.Max(0,Screen.height-_ui.ButtonSize));
        }
        private void HandleDrag(Rect title)
        {
            Event e = Event.current;
            if (!Cursor.visible || e == null || _ui.LockPanel) return;
            if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(e.mousePosition))
            { _dragging = true; _dragOffset = e.mousePosition - _position; GUI.FocusControl(null); e.Use(); }
            else if (e.type == EventType.MouseDrag && _dragging)
            { _position = e.mousePosition - _dragOffset; ClampPosition(); e.Use(); }
            else if (e.type == EventType.MouseUp && _dragging)
            { _dragging = false; _ui.PanelX=_position.x; _ui.PanelY=_position.y; _ui.Save(); e.Use(); }
        }
        private void DrawLauncher()
        {
            Rect rect=ButtonRect(); Event e=Event.current;
            if(Cursor.visible && e!=null)
            {
                if(e.type==EventType.MouseDown && e.button==0 && rect.Contains(e.mousePosition))
                { _buttonDragging=true; _buttonMoved=false; _buttonPress=e.mousePosition; _buttonDragOffset=e.mousePosition-_buttonPosition; e.Use(); }
                else if(e.type==EventType.MouseDrag && _buttonDragging)
                { if(!_ui.LockButton && Vector2.Distance(e.mousePosition,_buttonPress)>4) { _buttonMoved=true; _buttonPosition=e.mousePosition-_buttonDragOffset; ClampPosition(); } e.Use(); }
                else if(e.type==EventType.MouseUp && e.button==0 && _buttonDragging)
                { _buttonDragging=false; if(_buttonMoved) { _ui.ButtonX=_buttonPosition.x; _ui.ButtonY=_buttonPosition.y; _ui.Save(); } else if(rect.Contains(e.mousePosition)) _togglePending=true; e.Use(); }
            }
            GUI.DrawTexture(rect,Expanded?_launchActive:_launchTexture);
            _launchTop.fontSize=Mathf.Clamp(Mathf.RoundToInt(rect.width*.28f),10,26);
            _launchBottom.fontSize=Mathf.Clamp(Mathf.RoundToInt(rect.width*.18f),8,18);
            GUI.Label(new Rect(rect.x,rect.y+rect.height*.16f,rect.width,rect.height*.34f),"SP2",_launchTop);
            GUI.Label(new Rect(rect.x,rect.y+rect.height*.52f,rect.width,rect.height*.28f),L("飞控","Flight"),_launchBottom);
        }
        private static void Paint(Rect rect, Color color)
        { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private void DrawWindow(int id)
        {
            if (_clearFocus) { GUI.FocusControl(null); _clearFocus = false; }
            GUILayout.Label((_craft.CraftName ?? L("当前飞机","Aircraft"))+" · "+(_active()?L("鼠标飞控已接管","Mouse flight active"):L("鼠标飞控未启用","Mouse flight inactive")),_muted);
            DrawFlightToggle();
            string source=_craft.HasCraftParameters?(_craft.CraftName+L("飞控参数"," flight parameters")):L("默认飞控参数","Default flight parameters");
            GUILayout.Label(source+L(" · 已应用"," · Applied")+(_dirty?L(" · 未保存"," · Unsaved"):""),_label);
            GUILayout.Label(L("拖动顶部移动 · ↑↓选行，←→细调，Shift 粗调，Tab 换分类。","Drag header · ↑↓ select · ←→ adjust · Shift: coarse · Tab: group"),_muted);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _groups.Length; i++)
            {
                if (GUILayout.Button(L(_groups[i],new string[]{"Pitch","Roll","Yaw","General"}[i]), _group == i ? _primary : _button, GUILayout.Height(32)))
                { _pendingGroup=i; GUI.FocusControl(null); }
            }
            GUILayout.EndHorizontal();
            List<TuningField> rows = Rows();
            Event e = Event.current;
            string focus = GUI.GetNameOfFocusedControl();
            if (GUI.enabled && Cursor.visible && !_resumePending && e.type == EventType.KeyDown && (focus == null || !focus.StartsWith("mf-value-", StringComparison.Ordinal)))
            {
                if (e.keyCode == KeyCode.Tab) { _pendingGroup=(_group+(e.shift?3:1))%4; e.Use(); }
                else if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
                { _selected = Mathf.Clamp(_selected + (e.keyCode == KeyCode.UpArrow ? -1 : 1), 0, rows.Count - 1); _scroll.y = Mathf.Max(0, _selected * 36 - 72); e.Use(); }
                else if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow)
                { Adjust(rows[_selected], e.keyCode == KeyCode.LeftArrow ? -1 : 1, e.shift); e.Use(); }
            }
            // Page/group changes wait for Update, before the next IMGUI Layout pass.
            bool enabled = GUI.enabled; GUI.enabled = enabled && !_resumePending;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < rows.Count; i++)
            {
                TuningField f = rows[i]; GUILayout.BeginHorizontal(i % 2 == 0 ? _row : _rowAlt, GUILayout.Height(36));
                if (GUILayout.Button(FieldLabel(f), i == _selected ? _selectedStyle : _button, GUILayout.Width(Mathf.Max(120, Mathf.Min(335, PanelRect().width-300)))))
                { _selected = i; GUI.FocusControl(null); }
                if (f.Toggle || f.Sign)
                {
                    float v = _draft[f.Name]; float.TryParse(_text[f.Name], NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                    if (GUILayout.Button(f.Toggle ? (v == 0 ? L("关闭","Off") : L("开启","On")) : (v < 0 ? L("反向 (-1)","Reverse (-1)") : L("正向 (+1)","Normal (+1)")), _button, GUILayout.Width(110))) Adjust(f, 1, false);
                }
                else
                {
                    if (GUILayout.Button("−", _button, GUILayout.Width(28))) Adjust(f, -1, Event.current.shift);
                    GUI.SetNextControlName("mf-value-" + f.Name);
                    string edit = GUILayout.TextField(_text[f.Name], 32, _input, GUILayout.Width(82));
                    if (edit != _text[f.Name]) { _text[f.Name] = edit; _dirty = true; }
                    if (GUILayout.Button("+", _button, GUILayout.Width(28))) Adjust(f, 1, Event.current.shift);
                }
                GUILayout.Label(f.Sign ? "±1" : f.Min.ToString("0.###") + " ~ " + f.Max.ToString("0.###"), _muted, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(NoticeText(), _label, GUILayout.Height(46));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_craft.HasCraftParameters?L("复原为作品参数","Restore craft parameters"):L("复原为默认参数","Restore defaults"), _button, GUILayout.Height(32))) { _draft = _craft.SourceValues(); FillText(); _dirty = true; _notice = "已恢复来源值；保存本次参数后生效。"; GUI.FocusControl(null); }
            if (GUILayout.Button(L("复制完整参数","Copy all parameters"), _button)) Copy(false);
            if (GUILayout.Button(L("复制差异参数","Copy differences"), _button)) Copy(true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(L("保存本次参数","Apply session parameters"), _primary, GUILayout.Height(36))) Save();
            if (GUILayout.Button(L("撤销未保存修改","Discard unsaved edits"), _button, GUILayout.Height(36)))
            { _draft = _craft.EditValues(); FillText(); _dirty = false; _notice = "已撤销未保存修改。"; GUI.FocusControl(null); }
            GUILayout.EndHorizontal(); GUI.enabled = enabled;
        }
        private void EnsureStyles()
        {
            if (_label != null) return;
            _font = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei", "SimHei", "Arial" }, 15);
            _label = new GUIStyle(GUI.skin.label); _label.font = _font; _label.fontSize = 15;
            _label.normal.textColor = new Color(.94f, .97f, 1, 1); _label.wordWrap = true;
            _muted = new GUIStyle(_label); _muted.fontSize = 14; _muted.normal.textColor = new Color(.75f, .83f, .94f, 1);
            _title = new GUIStyle(_label); _title.fontSize = 16; _title.fontStyle = FontStyle.Bold; _title.wordWrap = false;
            _readout = new GUIStyle(_title); _readout.fontSize = 22;
            _small = new GUIStyle(_muted); _small.fontSize = 12;
            _button = ButtonStyle(new Color(.10f, .16f, .25f, 1), new Color(.17f, .27f, .40f, 1), new Color(.07f, .12f, .20f, 1));
            _primary = ButtonStyle(new Color(.08f, .30f, .62f, 1), new Color(.13f, .42f, .80f, 1), new Color(.06f, .22f, .48f, 1));
            _selectedStyle = new GUIStyle(_primary); _selectedStyle.alignment = TextAnchor.MiddleLeft;
            _input = new GUIStyle(GUI.skin.textField); _input.font = _font; _input.fontSize = 15;
            _input.padding = new RectOffset(8, 8, 5, 5); _input.border = new RectOffset();
            Texture2D input = Texture(new Color(.055f, .08f, .12f, 1)), focus = Texture(new Color(.10f, .20f, .34f, 1));
            SetStates(_input, input, focus, focus);
            _row = new GUIStyle(); _row.normal.background = Texture(new Color(.045f, .062f, .09f, 1)); _row.padding = new RectOffset(4, 4, 3, 3);
            _rowAlt = new GUIStyle(_row); _rowAlt.normal.background = Texture(new Color(.065f, .085f, .12f, 1));
            _launchTop=new GUIStyle(_label); _launchTop.alignment=TextAnchor.MiddleCenter; _launchTop.fontStyle=FontStyle.Bold;
            _launchBottom=new GUIStyle(_launchTop); _launchBottom.normal.textColor=new Color(.86f,.94f,1,1);
            _launchTexture=LauncherTexture(new Color(40/255f,89/255f,158/255f,.98f),new Color(32/255f,73/255f,131/255f,1));
            _launchActive=LauncherTexture(new Color(50/255f,111/255f,198/255f,.98f),new Color(40/255f,92/255f,165/255f,1));
            _dot=new Texture2D(16,16); for(int y=0;y<16;y++) for(int x=0;x<16;x++) _dot.SetPixel(x,y,(new Vector2(x-7.5f,y-7.5f)).magnitude<=7.5f?Color.white:Color.clear); _dot.Apply(); _textures.Add(_dot);
        }
        private Texture2D LauncherTexture(Color fill,Color border)
        {
            Texture2D texture=new Texture2D(96,96);
            for(int y=0;y<96;y++) for(int x=0;x<96;x++)
            {
                float cx=Mathf.Clamp(x,12,83),cy=Mathf.Clamp(y,12,83),distance=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                texture.SetPixel(x,y,distance>12?Color.clear:(x<4||y<4||x>=92||y>=92||distance>8)?border:fill);
            }
            texture.Apply(); _textures.Add(texture); return texture;
        }
        private string L(string zh,string en) { return _ui.English?en:zh; }
        private string FieldLabel(TuningField field)
        {
            if(!_ui.English) return field.Label;
            if(field.Name == "MaxPitchRate") return "Pitch rate limit (°/s, 0=off)";
            if(field.Name == "MaxRollRate") return "Banking rate limit (°/s, 0=off)";
            if(field.Name == "MaxLevelRollRate") return "Levelling rate limit (°/s, 0=off)";
            if(field.Name == "MaxYawRate") return "Yaw rate limit (°/s, 0=off)";
            string name=System.Text.RegularExpressions.Regex.Replace(field.Name,"([a-z])([A-Z])","$1 $2");
            int unit=field.Label.IndexOf('('); return name+(unit>=0?" "+field.Label.Substring(unit):"");
        }
        private string NoticeText()
        {
            if(!_ui.English) return _notice;
            switch(_notice)
            {
                case "保存本次参数不会折叠。保存到飞机请复制参数并在设计器保存。": return "Apply keeps the panel open. To save with the craft, copy parameters into a part name and save in the designer.";
                case "已保存并应用到本次飞行，面板保持展开；飞机文件和全局配置未改写。": return "Applied to this flight. Craft file and global configuration are unchanged.";
                case "已恢复来源值；保存本次参数后生效。": return "Restored source values. Apply session parameters to activate them.";
                case "已撤销未保存修改。": return "Unsaved edits discarded.";
                case "已复制差异参数；未写入项目依赖接收方插件配置。": return "Differences copied. Omitted fields use the recipient's plugin configuration.";
                case "已复制完整参数，可粘贴到飞机任意一个零件名称并保存。": return "All parameters copied. Paste into one part name and save the craft.";
                case "飞控接管失败；请确认未暂停且没有游戏菜单打开。": return "Cannot engage mouse flight. Check pause and game menus.";
                case "滚转过渡终点须至少比起点大 1°": return "Bank blend end must exceed start by at least 1°.";
                case "竖直渐隐终点须至少比起点大 1°": return "Vertical fade end must exceed start by at least 1°.";
                case "回平退出角须至少比进入角大 0.5°": return "Roll capture exit must exceed entry by at least 0.5°.";
            }
            string text=_notice; foreach(TuningField f in FlightTuning.Fields) text=text.Replace(f.Label,FieldLabel(f));
            return text.Replace("取值无效，允许 ","Invalid value; allowed range: ").Replace(" 至 "," to ").Replace("请输入有效数值","Enter a valid number").Replace("参数","Parameter").Replace("必须","must").Replace("无效","invalid").Replace("范围","range");
        }
        private void DrawFlightToggle()
        {
            bool active=_active(); bool value=GUILayout.Toggle(active,L("启用鼠标飞控","Enable mouse flight"),active?_primary:_button,GUILayout.Height(32));
            if(value!=active) { if(value) _resumePending=true; else _disablePending=true; }
        }
        private void DrawSettings()
        {
            Event keyEvent = Event.current;
            if (_bindingKey != 0 && GUI.enabled && keyEvent.type == EventType.KeyDown)
            {
                KeyCode key = keyEvent.keyCode;
                if (key == KeyCode.Escape) { _bindingKey = 0; _keyReleaseFrame = Time.frameCount + 1; }
                else if (key != KeyCode.None && key < KeyCode.Mouse0)
                {
                    KeyCode other = KeyUtil.Parse(_bindingKey == 1 ? _settings.CameraTestKey : _settings.OverlayKey, KeyCode.None);
                    if (key == other) _notice = L("两个插件热键不能相同。", "Plugin hotkeys must be different.");
                    else { _pendingKey = key; _pendingBinding = _bindingKey; _bindingKey = 0; _notice = ""; }
                }
                keyEvent.Use();
            }
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll);
            GUILayout.BeginHorizontal(); GUILayout.Label(L("语言","Language"),_label);
            if(GUILayout.Button(_ui.English?"English → 中文":"中文 → English",_button,GUILayout.Width(190))) { _ui.English=!_ui.English; _ui.Save(); }
            GUILayout.EndHorizontal();
            bool lockButton=GUILayout.Toggle(_ui.LockButton,L("锁定按钮位置","Lock button position"),_ui.LockButton?_primary:_button,GUILayout.Height(36));
            bool lockPanel=GUILayout.Toggle(_ui.LockPanel,L("锁定面板位置","Lock panel position"),_ui.LockPanel?_primary:_button,GUILayout.Height(36));
            if(lockButton!=_ui.LockButton||lockPanel!=_ui.LockPanel) { _ui.LockButton=lockButton; _ui.LockPanel=lockPanel; _ui.Save(); }
            float width=SettingSlider(L("面板宽度","Panel width"),_ui.Width,480,1200);
            float height=SettingSlider(L("面板高度","Panel height"),_ui.Height,480,1000);
            float size=SettingSlider(L("按钮大小","Button size"),_ui.ButtonSize,36,96);
            if(width!=_ui.Width||height!=_ui.Height||size!=_ui.ButtonSize) { _ui.Width=width; _ui.Height=height; _ui.ButtonSize=size; _ui.Save(); }
            GUILayout.Space(16);
            DrawHotkey(1, L("面板展开 / 折叠", "Open / close panel"), _settings.OverlayKey);
            DrawHotkey(2, L("鼠标飞控启停", "Toggle mouse flight"), _settings.CameraTestKey);
            GUILayout.Label(_bindingKey != 0 ? L("按下新按键；Esc 取消。", "Press a new key; Esc cancels.") : L("点击按键后重新绑定，立即生效并保存。自由视角沿用游戏的下一视角绑定。", "Click a key to rebind. Changes apply and save immediately. Free look uses the game's Next View binding."), _muted);
            GUILayout.Label(_bindingKey != 0 ? _notice : "", _label, GUILayout.Height(24));
            GUILayout.EndScrollView();
        }
        private void DrawHotkey(int binding, string label, string current)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, _label);
            bool enabled = binding==1 ? _settings.PanelHotkeyEnabled : _settings.FlightHotkeyEnabled;
            bool desired = GUILayout.Toggle(enabled,enabled ? L("启用","On") : L("禁用","Off"),enabled ? _primary : _button,GUILayout.Width(64),GUILayout.Height(32));
            if(desired!=enabled) { if(binding==1) _settings.PanelHotkeyEnabled=desired; else _settings.FlightHotkeyEnabled=desired; _keyReleaseFrame=Time.frameCount+1; }
            if (GUILayout.Button(_bindingKey == binding ? L("等待按键…", "Press a key…") : current, _bindingKey == binding ? _primary : _button, GUILayout.Width(150), GUILayout.Height(32)))
            { _bindingKey = binding; _notice = ""; GUI.FocusControl(null); }
            GUILayout.EndHorizontal();
        }
        private float SettingSlider(string label,float value,float min,float max)
        {
            GUILayout.Space(14); GUILayout.BeginHorizontal(); GUILayout.Label(label,_label); GUILayout.Label(value.ToString("0")+" px",_muted,GUILayout.Width(100)); GUILayout.EndHorizontal();
            return Mathf.Round(GUILayout.HorizontalSlider(value,min,max));
        }
        private void DrawMonitor(Rect content)
        {
            bool active=_active(); bool desired=GUI.Toggle(new Rect(content.x,content.y,250,32),active,L("启用鼠标飞控","Enable mouse flight"),active?_primary:_button);
            if(desired!=active) { if(desired) _resumePending=true; else _disablePending=true; }
            if(!_monitor.Valid) { GUI.Label(new Rect(content.x,content.y+48,content.width,40),L("等待飞机监控数据","Waiting for aircraft data"),_label); return; }
            float visualHeight=Mathf.Min(290,Mathf.Max(170,content.height-150)),half=content.width*.5f;
            Rect status=new Rect(content.x,content.y+42,half-10,visualHeight);
            DrawAircraftStatus(status);
            DrawControls(new Rect(content.x+half,status.y,half,visualHeight));
            float y=status.yMax+12;
            float rowHeight=Mathf.Max(24,Mathf.Min(40,(content.yMax-y)/3));
            string[] labels={L("空速","Airspeed"),L("海拔高度","Altitude MSL"),L("迎角 / 侧滑","AoA / Sideslip"),L("航向","Heading"),L("三轴角速度 R/P/Y","Angular rate R/P/Y"),L("指向误差 P/Y","Aim error P/Y")};
            string[] values={F(_monitor.Speed," m/s"),F(_monitor.Altitude," m"),F(_monitor.Attack,"°")+" / "+F(_monitor.Slip,"°"),F(_monitor.Heading,"°"),F(_monitor.RollRate,"")+" / "+F(_monitor.PitchRate,"")+" / "+F(_monitor.YawRate," °/s"),_monitor.AimValid?F(_monitor.PitchError,"°")+" / "+F(_monitor.YawError,"°"):"—"};
            for(int i=0;i<labels.Length;i++)
            {
                float x=content.x+(i%2)*half,rowY=y+(i/2)*rowHeight;
                GUI.Label(new Rect(x,rowY,half*.45f,rowHeight),labels[i],_small);
                GUI.Label(new Rect(x+half*.45f,rowY,half*.55f-8,rowHeight),values[i],_small);
            }
        }
        private static void Card(Rect rect,bool active)
        {
            Paint(rect,active?new Color(.20f,.45f,.60f,1):new Color(.15f,.23f,.33f,1));
            Paint(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),active?new Color(.04f,.17f,.24f,1):new Color(.035f,.065f,.105f,1));
        }
        private void DrawAircraftStatus(Rect rect)
        {
            bool fuelKnown=FlightRecorder.Finite(_monitor.FuelFraction)&&FlightRecorder.Finite(_monitor.FuelCapacity);
            bool tank=fuelKnown&&_monitor.FuelCapacity>0;
            Rect fuel=new Rect(rect.x,rect.y,rect.width,64); Card(fuel,false);
            GUI.Label(new Rect(fuel.x+10,fuel.y+7,fuel.width-20,18),L("燃油","FUEL"),_small);
            string amount=tank?(_monitor.FuelFraction*100).ToString("0.0",CultureInfo.InvariantCulture)+"%":fuelKnown?L("无油箱","NO TANK"):"—";
            GUI.Label(new Rect(fuel.x+10,fuel.y+24,fuel.width-20,30),amount,_readout);
            if(tank) Paint(new Rect(fuel.x+1,fuel.yMax-4,(fuel.width-2)*Mathf.Clamp01(_monitor.FuelFraction),3),_monitor.FuelFraction<.15f?new Color(.94f,.65f,.26f,1):new Color(.29f,.71f,.89f,1));
            float cell=(rect.width-8)*.5f;
            StateCard(new Rect(rect.x,rect.y+72,cell,58),L("起落架","GEAR"),_monitor.LandingGearDown,L("放下","DOWN"),L("收起","UP"));
            StateCard(new Rect(rect.x+cell+8,rect.y+72,cell,58),L("手刹","PARK BRAKE"),_monitor.ParkingBrake,L("开启","ON"),L("关闭","OFF"));
            GUI.Label(new Rect(rect.x,rect.y+139,rect.width,20),L("激活组","ACTIVATION GROUPS"),_small);
            float tile=(rect.width-18)/4;
            float tileHeight=Mathf.Clamp((rect.height-166-6)*.5f,26,48);
            for(int i=0;i<8;i++)
            {
                bool? state=_monitor.ActivationGroups[i];
                Rect box=new Rect(rect.x+(i%4)*(tile+6),rect.y+166+(i/4)*(tileHeight+6),tile,tileHeight);
                Card(box,state.HasValue&&state.Value);
                GUI.Label(new Rect(box.x+4,box.center.y-9,22,18),(i+1).ToString("00"),_small);
                GUI.Label(new Rect(box.xMax-17,box.center.y-9,13,18),state.HasValue?(state.Value?"1":"0"):"—",_small);
            }
        }
        private void StateCard(Rect rect,string label,bool? state,string on,string off)
        {
            Card(rect,state.HasValue&&state.Value);
            GUI.Label(new Rect(rect.x+8,rect.y+6,rect.width-16,20),label,_small);
            GUI.Label(new Rect(rect.x+8,rect.y+29,rect.width-16,23),state.HasValue?((state.Value?"1 · ":"0 · ")+(state.Value?on:off)):"—",_label);
        }
        private static string F(float value,string unit) { return FlightRecorder.Finite(value)?value.ToString("0.0",CultureInfo.InvariantCulture)+unit:"—"; }
        private void DrawControls(Rect rect)
        {
            float trackTop=rect.y+48,trackBottom=rect.y+rect.height-70,left=rect.x+52,right=rect.xMax-52,mid=(left+right)*.5f;
            Rect roll=new Rect(left,trackTop,right-left,12),pitch=new Rect(mid-6,trackTop+12,12,Mathf.Max(12,trackBottom-trackTop-12)),yaw=new Rect(left,trackBottom,right-left,12);
            Color track=new Color(.1f,.16f,.25f,1); Paint(roll,track); Paint(pitch,track); Paint(yaw,track);
            Paint(new Rect(mid,roll.y-2,1,16),new Color(.75f,.83f,.94f,1)); Paint(new Rect(mid,yaw.y-2,1,16),new Color(.75f,.83f,.94f,1));
            Paint(new Rect(pitch.x-2,pitch.center.y,16,1),new Color(.75f,.83f,.94f,1));
            if(FlightRecorder.Finite(_monitor.Roll)) Dot(new Vector2(FlightPanelMath.MeterCentre(_monitor.Roll,roll.x,roll.width,8,true),roll.center.y));
            if(FlightRecorder.Finite(_monitor.Pitch)) Dot(new Vector2(pitch.center.x,FlightPanelMath.MeterCentre(-_monitor.Pitch,pitch.y,pitch.height,8,true)));
            if(FlightRecorder.Finite(_monitor.Yaw)) Dot(new Vector2(FlightPanelMath.MeterCentre(_monitor.Yaw,yaw.x,yaw.width,8,true),yaw.center.y));
            GUI.Label(new Rect(left,rect.y+8,right-left,30),"R "+StickText(_monitor.Roll),_muted);
            GUI.Label(new Rect(mid+14,pitch.center.y-16,Mathf.Max(30,right-mid-10),40),"P "+StickText(_monitor.Pitch),_muted);
            GUI.Label(new Rect(left,yaw.yMax+8,right-left,24),"Y "+StickText(_monitor.Yaw),_muted);
            DrawVertical(new Rect(rect.x+10,trackTop,12,trackBottom-trackTop+12),_monitor.Throttle,L("油门","Throttle"));
            DrawVertical(new Rect(rect.xMax-26,trackTop,12,trackBottom-trackTop+12),_monitor.Brake,L("刹车","Brake"));
        }
        private void Dot(Vector2 centre) { Color old=GUI.color; GUI.color=new Color(.54f,.72f,.96f,1); GUI.DrawTexture(new Rect(centre.x-4,centre.y-4,8,8),_dot); GUI.color=old; }
        private static string StickText(float value) { return FlightRecorder.Finite(value)?value.ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture):"—"; }
        private void DrawVertical(Rect rect,float value,string label)
        {
            Paint(rect,new Color(.1f,.16f,.25f,1)); if(FlightRecorder.Finite(value)) { float height=rect.height*Mathf.Clamp01(value); Paint(new Rect(rect.x,rect.yMax-height,rect.width,height),new Color(.3f,.6f,.95f,1)); }
            GUI.Label(new Rect(rect.x-10,rect.y-38,50,36),label,_muted);
            GUI.Label(new Rect(rect.x-10,rect.yMax+8,50,24),FlightRecorder.Finite(value)?(Mathf.Clamp01(value)*100).ToString("0")+"%":"—",_muted);
        }
        private GUIStyle ButtonStyle(Color normal, Color hover, Color active)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button); style.font = _font; style.fontSize = 15;
            style.padding = new RectOffset(8, 8, 5, 5); style.border = new RectOffset();
            SetStates(style, Texture(normal), Texture(hover), Texture(active)); return style;
        }
        private static void SetStates(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
        {
            style.normal.background = style.onNormal.background = normal;
            style.hover.background = style.onHover.background = hover;
            style.active.background = style.onActive.background = active;
            style.focused.background = style.onFocused.background = hover;
            foreach (GUIStyleState s in new GUIStyleState[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused }) s.textColor = Color.white;
        }
        private Texture2D Texture(Color color)
        { Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); _textures.Add(texture); return texture; }
        public void Dispose()
        {
            _ui.Flush();
            Cancel(); _shield.Dispose(); _buttonShield.Dispose(); _monitor.Dispose(); FlightInputPatch.PanelPointerCapture = null;
            if (_font != null) UnityEngine.Object.Destroy(_font); _font = null;
            foreach (Texture2D texture in _textures) UnityEngine.Object.Destroy(texture); _textures.Clear();
        }
    }
}
