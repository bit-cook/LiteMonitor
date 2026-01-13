using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using LiteMonitor.src.Core;
using LiteMonitor.src.UI.Controls;
using System.Diagnostics; // Process
using LiteMonitor.src.SystemServices;

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
            CreateWebCard(); // ★★★ 新增：网页显示分组 ★★★
            

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

        // ★★★ 新增：网页显示分组 ★★★
        // ★★★ [修改] 网页显示分组：将按钮移至 Tips 右侧 ★★★
        private void CreateWebCard()
        {
            // 1. 创建分组
            // 注意：请确保你的语言文件里有 Menu.WebSettings 或 Menu.WebServer
            var group = new LiteSettingsGroup(LanguageManager.T("Menu.WebServer")); 

            // ==========================================================
            // A. 构建头部区域 (Tips + 右侧按钮)
            // ==========================================================
            
            // A1. 创建“打开网页”按钮
            // 第二个参数 false 表示使用"灰色/次要"样式，类似监控项的按钮
            var btnOpen = new LiteButton(LanguageManager.T("Menu.OpenWeb"), false); 
            
            // 调整尺寸：高度设为 24px (比默认略矮)，宽度适中
            btnOpen.Size = new Size(UIUtils.S(80), UIUtils.S(24)); 
            // 停靠在右侧
            btnOpen.Dock = DockStyle.Right; 

            // 按钮点击逻辑 (保持你的 IP 获取逻辑不变)
            btnOpen.Click += (s, e) => 
            {
                try 
                {
                    string host = "localhost";
                    // 尝试从 HardwareMonitor 获取真实 IP
                    if (HardwareMonitor.Instance != null)
                    {
                        string ip = HardwareMonitor.Instance.GetNetworkIP();
                        if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0" && ip != "127.0.0.1") 
                        {
                            host = ip;
                        }
                    }
                    var url = $"http://{host}:{Config.WebServerPort}";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };

            // A2. 创建 Tips 文本
            var note = new LiteNote(LanguageManager.T("Menu.WebServerTip"), 0);
            note.Dock = DockStyle.Fill; // 填满左侧剩余空间

            // A3. 创建容器 Panel
            var headerPanel = new Panel {
                Height = UIUtils.S(30), // 设置高度，足以容纳按钮和文字
                Padding = new Padding(0) 
            };

            // ★★★ 布局关键：先添加 Fill 的，再添加 Right 的，但在 Z 轴上 Right 要优先 ★★★
            // 为了保证 btnOpen 能够切掉右边的空间，我们需要正确处理
            // 最稳妥的方法：
            headerPanel.Controls.Add(btnOpen); // 先加按钮 (Dock=Right)
            headerPanel.Controls.Add(note);    // 后加文字 (Dock=Fill)
            // 调整 Z-Order 确保按钮不被遮挡 (将按钮置于顶层)
            btnOpen.BringToFront(); 

            // A4. 将这个组合面板作为 FullItem 加入分组
            group.AddFullItem(headerPanel);


            // ==========================================================
            // B. 常规设置项
            // ==========================================================

            // 网页显示 开启
            AddBool(group, "Menu.WebServer", 
                () => Config.WebServerEnabled, 
                v => Config.WebServerEnabled = v
            );

            // 端口 输入框
            AddNumberInt(group, "Menu.WebServerPort", "", 
                () => Config.WebServerPort, 
                v => Config.WebServerPort = v,
                60 // 宽度
            );

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