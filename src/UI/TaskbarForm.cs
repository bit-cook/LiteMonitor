using Microsoft.Win32;
using System.Runtime.InteropServices;
using LiteMonitor.src.Core;

namespace LiteMonitor
{
    public class TaskbarForm : Form
    {
        private Dictionary<uint, ToolStripItem> _commandMap = new Dictionary<uint, ToolStripItem>();
        private readonly Settings _cfg;
        private readonly UIController _ui;
        private readonly System.Windows.Forms.Timer _timer = new();

        private HorizontalLayout _layout;

        private IntPtr _hTaskbar = IntPtr.Zero;
        private IntPtr _hTray = IntPtr.Zero;

        private Rectangle _taskbarRect = Rectangle.Empty;
        private int _taskbarHeight = 32;
        private bool _isWin11;

        private Color _transparentKey = Color.Black;
        private bool _lastIsLightTheme = false;

        private System.Collections.Generic.List<Column>? _cols;
        private readonly MainForm _mainForm;

        private const int WM_RBUTTONUP = 0x0205;
        

        public void ReloadLayout()
        {
            _layout = new HorizontalLayout(ThemeManager.Current, 300, LayoutMode.Taskbar, _cfg);
            SetClickThrough(_cfg.TaskbarClickThrough);
            CheckTheme(true);

            if (_cols != null && _cols.Count > 0)
            {
                _layout.Build(_cols, _taskbarHeight);
                Width = _layout.PanelWidth;
                UpdatePlacement(Width);
            }
            Invalidate();
        }

        public TaskbarForm(Settings cfg, UIController ui, MainForm mainForm)
        {
            _cfg = cfg;
            _ui = ui;
            _mainForm = mainForm;
            
            ReloadLayout();

            _isWin11 = Environment.OSVersion.Version >= new Version(10, 0, 22000);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ControlBox = false;
            TopMost = false;
            DoubleBuffered = true;

            CheckTheme(true);
            FindHandles();
            
            // 尝试挂载
            AttachToTaskbar();

            _timer.Interval = Math.Max(_cfg.RefreshMs, 60);
            _timer.Tick += (_, __) => Tick();
            _timer.Start();

            Tick();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            if (!_isWin11 && m.Msg == WM_RBUTTONUP)
            {
                this.BeginInvoke(new Action(ShowContextMenu));
                return; 
            }
            base.WndProc(ref m);
        }

        private void ShowContextMenu()
        {
            var menu = MenuManager.Build(_mainForm, _cfg, _ui);
            SetForegroundWindow(this.Handle);
            menu.Show(Cursor.Position);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                switch (_cfg.TaskbarDoubleClickAction)
                {
                    case 1: // 任务管理器
                        _mainForm.OpenTaskManager();
                        break;
                    case 2: // 设置
                        _mainForm.OpenSettings();
                        break;
                    case 3: // 历史流量
                        _mainForm.OpenTrafficHistory();
                        break;
                    case 0: // 默认：显隐切换
                    default:
                        if (_mainForm.Visible)
                            _mainForm.HideMainWindow();
                        else
                            _mainForm.ShowMainWindow();
                        break;
                }
            }
        }

        // -------------------------------------------------------------
        // Win32 API
        // -------------------------------------------------------------
        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string cls, string? name);
        [DllImport("user32.dll")] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? name);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int idx);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int idx, int value);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        // ★★★ 新增：获取父窗口 API ★★★
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)] private static extern IntPtr GetParent(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint LWA_COLORKEY = 0x00000001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }
        [DllImport("shell32.dll")] private static extern uint SHAppBarMessage(uint msg, ref APPBARDATA pData);
        private const uint ABM_GETTASKBARPOS = 5;

        // -------------------------------------------------------------
        // 主题检测与颜色设置
        // -------------------------------------------------------------
        private bool IsSystemLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? val = key.GetValue("SystemUsesLightTheme");
                    if (val is int i) return i == 1;
                }
            }
            catch { }
            return false;
        }

        private void CheckTheme(bool force = false)
        {
            bool isLight = IsSystemLightTheme();
            if (!force && isLight == _lastIsLightTheme) return;
            _lastIsLightTheme = isLight;

            if (_cfg.TaskbarCustomStyle)
            {
                try 
                {
                    Color customColor = ColorTranslator.FromHtml(_cfg.TaskbarColorBg);
                    if (customColor.R == customColor.G && customColor.G == customColor.B)
                    {
                        int r = customColor.R;
                        int g = customColor.G;
                        int b = customColor.B;
                        if (b >= 255) b = 254; else b += 1;
                        _transparentKey = Color.FromArgb(r, g, b);
                    }
                    else
                    {
                        _transparentKey = customColor;
                    }
                } 
                catch { _transparentKey = Color.Black; }
            }
            else
            {
                if (isLight) _transparentKey = Color.FromArgb(210, 210, 211); 
                else _transparentKey = Color.FromArgb(40, 40, 41);       
            }

            BackColor = _transparentKey;
            if (IsHandleCreated) ApplyLayeredAttribute();
            Invalidate();
        }

        public void SetClickThrough(bool enable)
        {
            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            if (enable) exStyle |= WS_EX_TRANSPARENT; 
            else exStyle &= ~WS_EX_TRANSPARENT; 
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);
        }

        private void ApplyLayeredAttribute()
        {
            uint colorKey = (uint)(_transparentKey.R | (_transparentKey.G << 8) | (_transparentKey.B << 16));
            SetLayeredWindowAttributes(Handle, colorKey, 0, LWA_COLORKEY);
        }

        // -------------------------------------------------------------
        // 核心逻辑
        // -------------------------------------------------------------
        private void FindHandles()
        {
            _hTaskbar = FindWindow("Shell_TrayWnd", null);
            _hTray = FindWindowEx(_hTaskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        }

        private void AttachToTaskbar()
        {
            if (_hTaskbar == IntPtr.Zero) FindHandles();
            if (_hTaskbar == IntPtr.Zero) return;

            // 尝试将自己设为任务栏子窗口
            SetParent(Handle, _hTaskbar);

            int style = GetWindowLong(Handle, GWL_STYLE);
            // 必须移除 WS_POPUP (0x80000000)，添加 WS_CHILD
            style &= (int)~0x80000000;
            style |= WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS;
            SetWindowLong(Handle, GWL_STYLE, style);

            ApplyLayeredAttribute();
        }

        private void Tick()
        {
            if (Environment.TickCount % 5000 < _cfg.RefreshMs) CheckTheme();

            _cols = _ui.GetTaskbarColumns();
            if (_cols == null || _cols.Count == 0) return;
            
            UpdateTaskbarRect(); 
                
            _layout.Build(_cols, _taskbarHeight);
            Width = _layout.PanelWidth;
            Height = _taskbarHeight;
            
            UpdatePlacement(Width);
            Invalidate();
        }

        // -------------------------------------------------------------
        // 定位与辅助
        // -------------------------------------------------------------
        private void UpdateTaskbarRect()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            uint res = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
            
            if (res != 0)
            {
                _taskbarRect = Rectangle.FromLTRB(abd.rc.left, abd.rc.top, abd.rc.right, abd.rc.bottom);
            }
            else
            {
                // ★★★ 核心修复：API 失败时的 Fallback ★★★
                // 确保默认认为任务栏在底部，而不是 (0,0,0,0)
                var s = Screen.PrimaryScreen;
                if (s != null)
                {
                    int fallbackHeight = 40; // 假设标准高度
                    _taskbarRect = new Rectangle(
                        s.Bounds.Left, 
                        s.Bounds.Bottom - fallbackHeight, 
                        s.Bounds.Width, 
                        fallbackHeight
                    );
                }
            }
            _taskbarHeight = Math.Max(24, _taskbarRect.Height);
        }

        public static bool IsCenterAligned()
        {
            if (Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < 22000) 
                return false;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                return ((int)(key?.GetValue("TaskbarAl", 1) ?? 1)) == 1;
            }
            catch { return false; }
        }

        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
        public static int GetTaskbarDpi()
        {
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar != IntPtr.Zero)
            {
                try { return (int)GetDpiForWindow(taskbar); } catch { }
            }
            return 96;
        }

        public static int GetWidgetsWidth()
        {
            int dpi = TaskbarForm.GetTaskbarDpi();
            if (Environment.OSVersion.Version >= new Version(10, 0, 22000))
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string pkg = Path.Combine(local, "Packages");
                bool hasWidgetPkg = false;
                try { hasWidgetPkg = Directory.GetDirectories(pkg, "MicrosoftWindows.Client.WebExperience*").Any(); } catch {}
                
                if (!hasWidgetPkg) return 0;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                if (key == null) return 0;

                object? val = key.GetValue("TaskbarDa");
                if (val is int i && i != 0) return 150 * dpi / 96;
            }
            return 0;
        }

       // ★★★ 核心修复：防飞天 + 强制置顶 ★★★
        private void UpdatePlacement(int panelWidth)
        {
            if (_hTaskbar == IntPtr.Zero) return;

            var scr = Screen.PrimaryScreen;
            if (scr == null) return;
            
            GetWindowRect(_hTray, out RECT tray);
            bool bottom = _taskbarRect.Bottom >= scr.Bounds.Bottom - 2;

            // 1. 获取系统对齐状态
            bool sysCentered = IsCenterAligned(); 
            bool alignLeft = _cfg.TaskbarAlignLeft && sysCentered; 
            int rightSideWidgetOffset = sysCentered ? 0 : GetWidgetsWidth();

            int leftScreen, topScreen;

            // Y轴计算
            if (bottom) topScreen = _taskbarRect.Bottom - _taskbarHeight;
            else topScreen = _taskbarRect.Top;

            // X轴计算
            if (alignLeft)
            {
                int startX = _taskbarRect.Left + 6;
                if (GetWidgetsWidth() > 0) startX += GetWidgetsWidth();
                leftScreen = startX;
            }
            else
            {
                leftScreen = tray.left - panelWidth - 6 - rightSideWidgetOffset;
            }

            // ===========================================================
            // ★★★ 防飞天逻辑 (含 Z-Order 修复) ★★★
            // ===========================================================
            
            IntPtr currentParent = GetParent(Handle);
            bool isAttached = (currentParent == _hTaskbar);

            // 如果断开了，尝试重连
            if (!isAttached)
            {
                AttachToTaskbar();
                currentParent = GetParent(Handle);
                isAttached = currentParent == _hTaskbar;
            }

            int finalX = leftScreen;
            int finalY = topScreen;
            
            if (isAttached)
            {
                // [情况A] 正常挂载：坐标转为相对坐标
                POINT pt = new POINT { X = leftScreen, Y = topScreen };
                ScreenToClient(_hTaskbar, ref pt);
                finalX = pt.X;
                finalY = pt.Y;

                // 保持原有的层级关系 (由任务栏管理)，不改变 Z-Order
                SetWindowPos(Handle, IntPtr.Zero, finalX, finalY, panelWidth, _taskbarHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            }
            else
            {
                // [情况B] 挂载失败 / 测试模式：使用绝对坐标
                // ★关键修正★：必须移除 SWP_NOZORDER，并传入 HWND_TOPMOST (-1)
                // 否则普通窗口会被任务栏遮挡，导致"看不见"
                IntPtr HWND_TOPMOST = (IntPtr)(-1);
                SetWindowPos(Handle, HWND_TOPMOST, finalX, finalY, panelWidth, _taskbarHeight, SWP_NOACTIVATE);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(_transparentKey);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_cols == null) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            TaskbarRenderer.Render(g, _cols, _lastIsLightTheme);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW; 
                return cp;
            }
        }
    }
}