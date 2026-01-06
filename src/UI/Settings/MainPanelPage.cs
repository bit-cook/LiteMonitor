using System;
using System.Drawing;
using System.Windows.Forms;
using LiteMonitor.src.Core;
using LiteMonitor.src.UI.Controls;

namespace LiteMonitor.src.UI.SettingsPage
{
    public class MainPanelPage : SettingsPageBase
    {
        private Panel _container;
        private bool _isLoaded = false;

        // 控件引用保留，用于布局或事件，但不再需要在Save中手动读取
        private LiteCheck _chkHideMain;
        private LiteCheck _chkAutoHide;
        private LiteCheck _chkTopMost;
        private LiteCheck _chkClickThrough;
        private LiteCheck _chkClamp;

        private LiteComboBox _cmbTheme;
        private LiteComboBox _cmbOrientation;
        private LiteComboBox _cmbWidth;
        private LiteComboBox _cmbOpacity;
        private LiteComboBox _cmbScale;

        public MainPanelPage()
        {
            this.BackColor = UIColors.MainBg;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(0);
            _container = new BufferedPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) };
            this.Controls.Add(_container);
        }

        public override void OnShow()
        {
            base.OnShow(); // ★ 必须调用：清理旧的绑定
            if (Config == null || _isLoaded) return;

            _container.SuspendLayout();
            _container.Controls.Clear();

            CreateBehaviorCard();
            CreateAppearanceCard();

            _container.ResumeLayout();
            _isLoaded = true;
        }

        private void CreateBehaviorCard()
        {
            var group = new LiteSettingsGroup(LanguageManager.T("Menu.MainFormSettings"));

            // 1. 显隐开关 (带联动逻辑)
            AddBool(group, "Menu.HideMainForm", 
                () => Config.HideMainForm, 
                v => Config.HideMainForm = v,
                // 这里的 lambda 完美替代了以前繁琐的事件绑定代码
                chk => chk.CheckedChanged += (s, e) => EnsureSafeVisibility(chk, null, null) 
            );

            // 2. 其他开关 (一行一个)
            AddBool(group, "Menu.TopMost", () => Config.TopMost, v => Config.TopMost = v);
            AddBool(group, "Menu.ClampToScreen", () => Config.ClampToScreen, v => Config.ClampToScreen = v);
            AddBool(group, "Menu.AutoHide", () => Config.AutoHide, v => Config.AutoHide = v);
            AddBool(group, "Menu.ClickThrough", () => Config.ClickThrough, v => Config.ClickThrough = v);

            // ★★★ 新增：双击动作设置 ★★★
            string[] actions = { 
                LanguageManager.T("Menu.ActionSwitchLayout"), // 0: 切换横竖屏
                LanguageManager.T("Menu.ActionTaskMgr"),      // 1: 任务管理器
                LanguageManager.T("Menu.ActionSettings"),           // 2: 设置
                LanguageManager.T("Menu.ActionTrafficHistory")      // 3: 历史流量
            };
            AddComboIndex(group, "Menu.DoubleClickAction", actions,
                () => Config.MainFormDoubleClickAction,
                idx => Config.MainFormDoubleClickAction = idx
            );

            AddGroupToPage(group);
        }

        private void CreateAppearanceCard()
        {
            var group = new LiteSettingsGroup(LanguageManager.T("Menu.Appearance"));

            // 1. 主题
            AddCombo(group, "Menu.Theme", ThemeManager.GetAvailableThemes(), 
                () => Config.Skin, 
                v => Config.Skin = v);

            // 2. 方向 (使用 Index 绑定辅助方法)
            AddComboIndex(group, "Menu.DisplayMode", 
                new[] { LanguageManager.T("Menu.Vertical"), LanguageManager.T("Menu.Horizontal") },
                () => Config.HorizontalMode ? 1 : 0, 
                idx => Config.HorizontalMode = (idx == 1));

            // 3. 宽度 (复杂逻辑：带单位转换)
            int[] widths = { 180, 200, 220, 240, 260, 280, 300, 360, 420, 480, 540, 600, 660, 720, 780, 840, 900, 960, 1020, 1080, 1140, 1200 };
            // 技巧：直接生成带单位的字符串列表，getter/setter 负责处理 " px" 后缀
            AddCombo(group, "Menu.Width", 
                widths.Select(w => w + " px"), 
                () => Config.PanelWidth + " px",
                s => Config.PanelWidth = UIUtils.ParseInt(s));


            // 5. 透明度
            double[] opacities = { 1.0, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.6, 0.5, 0.4, 0.3 };
            AddCombo(group, "Menu.Opacity",
                opacities.Select(o => Math.Round(o * 100) + "%"),
                () => Math.Round(Config.Opacity * 100) + "%",
                s => Config.Opacity = UIUtils.ParseDouble(s) / 100.0);

           
            // ★★★ 新增：内存显示模式下拉框 ★★★
            // 这里使用了 AddComboIndex，绑定到 Config.MemoryDisplayMode
            string[] memOptions = { LanguageManager.T("Menu.Percent"), LanguageManager.T("Menu.UsedSize") }; 
            AddComboIndex(group, "Menu.MemoryDisplayMode", memOptions,
                () => Config.MemoryDisplayMode, // Getter
                idx => Config.MemoryDisplayMode = idx // Setter
            );

            // 4. 缩放
            double[] scales = { 2.0, 1.75, 1.5, 1.25, 1.0, 0.9, 0.85, 0.8, 0.75, 0.7, 0.6, 0.5 };
            AddCombo(group, "Menu.Scale",
                scales.Select(s => (s * 100) + "%"),
                () => (Config.UIScale * 100) + "%",
                s => Config.UIScale = UIUtils.ParseDouble(s) / 100.0);

            group.AddFullItem(new LiteNote(LanguageManager.T("Menu.MemoryDisplayModeTip"), 0));

            AddGroupToPage(group);
        }

        private void AddGroupToPage(LiteSettingsGroup group)
        {
            var wrapper = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 20) };
            wrapper.Controls.Add(group);
            _container.Controls.Add(wrapper);
            _container.Controls.SetChildIndex(wrapper, 0);
        }

    }
}