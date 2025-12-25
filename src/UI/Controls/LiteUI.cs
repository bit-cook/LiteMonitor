using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiteMonitor.src.UI.Controls
{
    public static class UIColors
    {
        public static Color MainBg = Color.FromArgb(243, 243, 243);
        public static Color SidebarBg = Color.FromArgb(240, 240, 240);
        public static Color CardBg = Color.White;
        public static Color Border = Color.FromArgb(220, 220, 220);
        public static Color Primary = Color.FromArgb(0, 120, 215);
        public static Color TextMain = Color.FromArgb(32, 32, 32);
        public static Color TextSub = Color.FromArgb(90, 90, 90);
        public static Color GroupHeader = Color.FromArgb(248, 249, 250); 
        
        public static Color NavSelected = Color.FromArgb(230, 230, 230); 
        public static Color NavHover = Color.FromArgb(235, 235, 235);

        public static Color TextWarn = Color.FromArgb(215, 145, 0); 
        public static Color TextCrit = Color.FromArgb(220, 50, 50); 
    }
    public static class UIFonts 
    {
        public static Font Regular(float size) => new Font("Microsoft YaHei UI", size, FontStyle.Regular);
        public static Font Bold(float size) => new Font("Microsoft YaHei UI", size, FontStyle.Bold);
    }

    // =======================================================================
    // 1. 容器组件
    // =======================================================================

    public class LiteSettingsGroup : Panel
    {
        private TableLayoutPanel _layout;
        private int _colTracker = 0;

        public LiteSettingsGroup(string title)
        {
            this.AutoSize = true;
            this.Dock = DockStyle.Top;
            this.Padding = new Padding(1); 
            this.BackColor = UIColors.Border; 
            this.Margin = new Padding(0, 0, 0, 15); 

            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoSize = true };
            
            var header = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = UIColors.GroupHeader };
            var lbl = new Label { 
                Text = title, Location = new Point(15, 10), AutoSize = true, 
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), ForeColor = UIColors.TextMain 
            };
            header.Controls.Add(lbl);
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(UIColors.Border), 0, 39, header.Width, 39);

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1,
                Padding = new Padding(25, 10, 25, 15), BackColor = Color.White
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            inner.Controls.Add(_layout);
            inner.Controls.Add(header);
            this.Controls.Add(inner);
        }

        public void AddItem(Control item)
        {
            _layout.Controls.Add(item);
            item.Dock = DockStyle.Fill;
            if (_colTracker == 0) { item.Margin = new Padding(0, 2, 30, 2); _colTracker = 1; }
            else { item.Margin = new Padding(30, 2, 0, 2); _colTracker = 0; }
        }

        public void AddFullItem(Control item)
        {
            _layout.Controls.Add(item);
            _layout.SetColumnSpan(item, 2);
            item.Dock = DockStyle.Fill;
            item.Margin = new Padding(0, 0, 0, 0); 
            _colTracker = 0; 
        }
    }

    public class LiteSettingsItem : Panel
    {
        public LiteSettingsItem(string text, Control ctrl)
        {
            this.Height = 40;
            this.Margin = new Padding(0, 2, 40, 2); 
            var lbl = new Label { 
                Text = text, AutoSize = true, 
                Font = new Font("Microsoft YaHei UI", 9F), ForeColor = UIColors.TextMain,
                TextAlign = ContentAlignment.MiddleLeft 
            };
            if (ctrl is LiteCheck) ctrl.Height = 22; 
            this.Controls.Add(lbl);
            this.Controls.Add(ctrl);
            this.Layout += (s, e) => {
                int mid = this.Height / 2;
                lbl.Location = new Point(0, mid - lbl.Height / 2);
                ctrl.Location = new Point(this.Width - ctrl.Width, mid - ctrl.Height / 2);
            };
            this.Paint += (s, e) => {
                using(var p = new Pen(Color.FromArgb(225, 225, 225))) 
                    e.Graphics.DrawLine(p, 0, Height-1, Width, Height-1);
            };
        }
    }

    public class LiteCard : Panel
    {
        public LiteCard() { BackColor = UIColors.CardBg; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Padding = new Padding(1); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var p = new Pen(UIColors.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
    }

    // =======================================================================
    // 2. 交互组件
    // =======================================================================

    // ★★★ [优化版] 下划线输入框：支持前置标签 ★★★
    public class LiteUnderlineInput : Panel
    {
        public TextBox Inner;
        private Label _lblUnit;   // 单位 (右侧)
        private Label _lblLabel;  // 标签 (左侧)

        public LiteUnderlineInput(string text, string unit = "", string labelPrefix = "", int width = 160, Color? fontColor = null,HorizontalAlignment align = HorizontalAlignment.Left) // ★ 新增参数)
        {
            this.Size = new Size(width, 26); // ★ 增加高度到 28 (原26)，防止文字裁切
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(0, 2, 0, 3); // ★ 减少顶部Padding (5->2)，给文字留足空间
            this.Cursor = Cursors.IBeam;

            // ★★★ 关键修复：先添加 Inner，再添加 Label ★★★
            // 在 Dock 布局中，后添加的控件 (Top Z-Order) 优先占据边缘。
            // 我们希望 Label 和 Unit 优先占据左右两侧，Inner 填充剩余空间。
            
            // 1. 创建并添加输入框 (垫底)
            Inner = new TextBox {
                Text = text,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = fontColor ?? UIColors.TextSub,
                // ★★★ 只需增加这一行 ★★★
                TextAlign = align // ★ 修改这里：赋值为传入的参数
            };
            this.Controls.Add(Inner); // 先加它！

            // 2. 添加单位 (Dock Right, 浮在右边)
            if (!string.IsNullOrEmpty(unit))
            {
                _lblUnit = new Label {
                    Text = unit,
                    AutoSize = true, 
                    Dock = DockStyle.Right,
                    Font = new Font("Microsoft YaHei UI", 8F), 
                    ForeColor = Color.Gray, 
                    TextAlign = ContentAlignment.BottomRight, 
                    Padding = new Padding(0, 0, 0, 4) 
                };
                this.Controls.Add(_lblUnit); // 后加，覆盖在 Right
                _lblUnit.Click += (s, e) => Inner.Focus();
            }

            // 3. 添加前置标签 (Dock Left, 浮在左边)
            if (!string.IsNullOrEmpty(labelPrefix))
            {
                _lblLabel = new Label {
                    Text = labelPrefix, 
                    AutoSize = true, 
                    Dock = DockStyle.Left,
                    Font = new Font("Microsoft YaHei UI", 9F), 
                    ForeColor = fontColor ?? UIColors.TextSub,
                    TextAlign = ContentAlignment.BottomLeft, 
                    Padding = new Padding(0, 0, 4, 3) 
                };
                this.Controls.Add(_lblLabel); // 最后加，覆盖在 Left
                _lblLabel.Click += (s, e) => Inner.Focus();
            }

            // 事件转发
            Inner.Enter += (s, e) => this.Invalidate();
            Inner.Leave += (s, e) => this.Invalidate();
            this.Click += (s, e) => Inner.Focus();
        }

        public void SetTextColor(Color c) => Inner.ForeColor = c;
        public void SetBg(Color c) { Inner.BackColor = c; }
        
        protected override void OnPaint(PaintEventArgs e) {
            var c = Inner.Focused ? UIColors.Primary : Color.LightGray; 
            int h = Inner.Focused ? 2 : 1;

            // 画线逻辑：如果有左侧标签，线条从标签右侧开始画
            int startX = 0;
            if (_lblLabel != null) startX = _lblLabel.Width; 
            int drawWidth = this.Width - startX;

            // 线条画在底部 (Height - h)
            using (var b = new SolidBrush(c)) 
                e.Graphics.FillRectangle(b, startX, Height - h, drawWidth, h);
        }
    }

    // =======================================================================
    // 新增：专门的数字输入框 (继承自下划线输入框)
    // =======================================================================
    public class LiteNumberInput : LiteUnderlineInput
    {
        // 升级构造函数：支持前缀标签(label)和文字颜色(color)，以适配 ThresholdPage
        public LiteNumberInput(
            string text, 
            string unit = "", 
            string label = "",      // 新增：支持前缀 (如 "警告")
            int width = 160, 
            Color? color = null,    // 新增：支持颜色 (如 红色/橙色)
            int maxLength = 10) 
            : base(text, unit, label, width, color, HorizontalAlignment.Center) // 调用基类完整构造
        {
            // 1. 设置最大长度
            this.Inner.MaxLength = maxLength;

            // 2. 核心：只允许输入数字、退格键、负号、小数点
            this.Inner.KeyPress += (s, e) =>
            {
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
                if (e.KeyChar == '.' && !this.Inner.Text.Contains(".")) return;
                if (e.KeyChar == '-' && this.Inner.SelectionStart == 0 && !this.Inner.Text.Contains("-")) return;
                e.Handled = true;
            };

            // 3. 失去焦点自动补零
            this.Inner.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(this.Inner.Text) || this.Inner.Text == "." || this.Inner.Text == "-")
                {
                    this.Inner.Text = "0";
                }
            };
        }
        
        public int ValueInt => int.TryParse(Inner.Text, out int v) ? v : 0;
        public double ValueDouble => double.TryParse(Inner.Text, out double v) ? v : 0.0;
    }

    public class LiteColorInput : Panel
    {
        public LiteUnderlineInput Input;
        public LiteColorPicker Picker;
        public string HexValue { get => Input.Inner.Text; set { Input.Inner.Text = value; Picker.SetHex(value); } }

        public LiteColorInput(string initialHex)
        {
            this.Size = new Size(95, 26); 
            Picker = new LiteColorPicker(initialHex) { Size = new Size(26, 22), Location = new Point(this.Width - 26, 3) };
            
            // 适配新构造函数
            Input = new LiteUnderlineInput(initialHex, "", "", 60) { Location = new Point(0, 0) };
            
            Picker.ColorChanged += (s, e) => Input.Inner.Text = $"#{Picker.Value.R:X2}{Picker.Value.G:X2}{Picker.Value.B:X2}";
            Input.Inner.TextChanged += (s, e) => Picker.SetHex(Input.Inner.Text);
            this.Controls.Add(Input);
            this.Controls.Add(Picker);
        }
    }

    public class LiteColorPicker : Control
    {
        private Color _color;
        public event EventHandler? ColorChanged;
        public Color Value { get => _color; set { _color = value; Invalidate(); } }
        public LiteColorPicker(string initialHex) { SetHex(initialHex); this.Size = new Size(24, 24); this.Cursor = Cursors.Hand; this.DoubleBuffered = true; this.Click += (s, e) => PickColor(); }
        public void SetHex(string hex) { try { _color = ColorTranslator.FromHtml(hex); Invalidate(); } catch {} }
        private void PickColor() { using (var cd = new ColorDialog()) { cd.Color = _color; cd.FullOpen = true; if (cd.ShowDialog() == DialogResult.OK) { _color = cd.Color; ColorChanged?.Invoke(this, EventArgs.Empty); Invalidate(); } } }
        protected override void OnPaint(PaintEventArgs e) { using (var b = new SolidBrush(_color)) e.Graphics.FillRectangle(b, 0, 0, Width - 1, Height - 1); using (var p = new Pen(Color.Gray)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
    }

    // 其他原有组件
    public class LiteNote : Panel { public LiteNote(string text, int indent = 0) { this.Dock = DockStyle.Top; this.Height = 32; this.Margin = new Padding(0); var lbl = new Label { Text = text, AutoSize = true, Font = new Font("Microsoft YaHei UI", 8F), ForeColor = Color.Gray, Location = new Point(indent, 10) }; this.Controls.Add(lbl); } }
    public class LiteComboBox : Panel { public ComboBox Inner; public LiteComboBox() { this.Size = new Size(110,28); this.BackColor = Color.White; this.Padding = new Padding(1); Inner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, ForeColor = UIColors.TextSub, Font = new Font("Microsoft YaHei UI", 9F), Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0) }; this.Controls.Add(Inner); this.Paint += (s, e) => { using (var p = new Pen(UIColors.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }; } public object SelectedItem { get => Inner.SelectedItem; set => Inner.SelectedItem = value; } public int SelectedIndex { get => Inner.SelectedIndex; set => Inner.SelectedIndex = value; } public ComboBox.ObjectCollection Items => Inner.Items; public override string Text { get => Inner.Text; set => Inner.Text = value; } }
    public class LiteCheck : CheckBox { public LiteCheck(bool val, string text = "") { Checked = val; AutoSize = true; Cursor = Cursors.Hand; Text = text; Padding = new Padding(2); ForeColor = UIColors.TextSub; Font = new Font("Microsoft YaHei UI", 9F); } }
    public class LiteButton : Button { public LiteButton(string t, bool p) { Text = t; Size = new Size(80, 32); FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand; Font = new Font("Segoe UI", 9F); if (p) { BackColor = UIColors.Primary; ForeColor = Color.White; FlatAppearance.BorderSize = 0; } else { BackColor = Color.White; ForeColor = UIColors.TextMain; FlatAppearance.BorderColor = UIColors.Border; } } }
    public class LiteNavBtn : Button { private bool _isActive; public bool IsActive { get => _isActive; set { _isActive = value; Invalidate(); } } public LiteNavBtn(string text) { Text = "  " + text; Size = new Size(150, 40); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; TextAlign = ContentAlignment.MiddleLeft; Font = new Font("Microsoft YaHei UI", 10F); Cursor = Cursors.Hand; Margin = new Padding(5, 2, 5, 2); BackColor = UIColors.SidebarBg; ForeColor = UIColors.TextMain; } protected override void OnPaint(PaintEventArgs e) { Color bg = _isActive ? UIColors.NavSelected : (ClientRectangle.Contains(PointToClient(Cursor.Position)) ? UIColors.NavHover : UIColors.SidebarBg); using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, ClientRectangle); if (_isActive) { using (var b = new SolidBrush(UIColors.Primary)) e.Graphics.FillRectangle(b, 0, 8, 3, Height - 16); Font = new Font(Font, FontStyle.Bold); } else { Font = new Font(Font, FontStyle.Regular); } TextRenderer.DrawText(e.Graphics, Text, Font, new Point(12, 9), UIColors.TextMain); } protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); } protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); } }
    public class LiteSortBtn : Button { public LiteSortBtn(string txt) { Text = txt; Size = new Size(24, 24); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.FromArgb(245, 245, 245); ForeColor = Color.DimGray; Cursor = Cursors.Hand; Font = new Font("Microsoft YaHei UI", 7F, FontStyle.Bold); Margin = new Padding(0); } }
    
    /// <summary>
    /// 终极防闪烁面板
    /// 开启了 WS_EX_COMPOSITED，强制让所有子控件参与双缓冲合成
    /// </summary>
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                          ControlStyles.UserPaint | 
                          ControlStyles.OptimizedDoubleBuffer | 
                          ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // 开启 WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}