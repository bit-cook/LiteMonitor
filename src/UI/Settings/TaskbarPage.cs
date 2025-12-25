using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LiteMonitor.src.Core;
using LiteMonitor.src.UI.Controls;

namespace LiteMonitor.src.UI.SettingsPage
{
    public class TaskbarPage : SettingsPageBase
    {
        private Panel _container;
        private bool _isLoaded = false;
        private List<Control> _customColorInputs = new List<Control>();

        public TaskbarPage()
        {
            this.BackColor = UIColors.MainBg;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(0);
            _container = new BufferedPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) }; 
            this.Controls.Add(_container);
        }

        public override void OnShow()
        {
            base.OnShow();
            if (Config == null || _isLoaded) return;
            
            _container.SuspendLayout();
            _container.Controls.Clear();

            CreateGeneralGroup(); 
            CreateColorGroup();   

            _container.ResumeLayout();
            _isLoaded = true;
        }

        private void CreateGeneralGroup()
        {
            var group = new LiteSettingsGroup(LanguageManager.T("Menu.TaskbarSettings"));

            // 1. 总开关
            AddBool(group, "Menu.TaskbarShow", 
                () => Config.ShowTaskbar, 
                v => Config.ShowTaskbar = v,
                chk => chk.CheckedChanged += (s, e) => EnsureSafeVisibility(null, null, chk)
            );

            // 2. 鼠标穿透
            AddBool(group, "Menu.ClickThrough", () => Config.TaskbarClickThrough, v => Config.TaskbarClickThrough = v);

            // 3. 样式 (Bold/Regular)
            AddComboIndex(group, "Menu.TaskbarStyle",
                new[] { LanguageManager.T("Menu.TaskbarStyleBold"), LanguageManager.T("Menu.TaskbarStyleRegular") },
                () => (Math.Abs(Config.TaskbarFontSize - 9f) < 0.1f && !Config.TaskbarFontBold) ? 1 : 0,
                idx => {
                    if (idx == 1) { Config.TaskbarFontSize = 9f; Config.TaskbarFontBold = false; }
                    else { Config.TaskbarFontSize = 10f; Config.TaskbarFontBold = true; }
                }
            );

            // 4. 对齐
            AddComboIndex(group, "Menu.TaskbarAlign",
                new[] { LanguageManager.T("Menu.TaskbarAlignRight"), LanguageManager.T("Menu.TaskbarAlignLeft") },
                () => Config.TaskbarAlignLeft ? 1 : 0,
                idx => Config.TaskbarAlignLeft = (idx == 1)
            );

            group.AddFullItem(new LiteNote(LanguageManager.T("Menu.TaskbarAlignTip"), 0));
            AddGroupToPage(group);
        }

        private void CreateColorGroup()
        {
            var group = new LiteSettingsGroup(LanguageManager.T("Menu.TaskbarCustomColors"));
            _customColorInputs.Clear();

            // 1. 自定义开关 (控制下方 Enabled)
            AddBool(group, "Menu.TaskbarCustomColors", 
                () => Config.TaskbarCustomStyle, 
                v => Config.TaskbarCustomStyle = v,
                chk => chk.CheckedChanged += (s, e) => {
                    foreach(var c in _customColorInputs) c.Enabled = chk.Checked;
                }
            );
            
            group.AddFullItem(new LiteNote(LanguageManager.T("Menu.TaskbarCustomTip"), 0));

            // 2. 批量添加颜色
            void AddC(string key, Func<string> get, Action<string> set)
            {
                // 使用工厂方法
                var input = AddColor(group, key, get, set, Config.TaskbarCustomStyle);
                _customColorInputs.Add(input);
            }

            AddC("Menu.LabelColor",      () => Config.TaskbarColorLabel, v => Config.TaskbarColorLabel = v);
            AddC("Menu.ValueSafeColor",  () => Config.TaskbarColorSafe,  v => Config.TaskbarColorSafe = v);
            AddC("Menu.ValueWarnColor",  () => Config.TaskbarColorWarn,  v => Config.TaskbarColorWarn = v);
            AddC("Menu.ValueCritColor",  () => Config.TaskbarColorCrit,  v => Config.TaskbarColorCrit = v);
            AddC("Menu.BackgroundColor", () => Config.TaskbarColorBg,    v => Config.TaskbarColorBg = v);

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