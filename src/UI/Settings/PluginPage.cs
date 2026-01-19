using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LiteMonitor;
using LiteMonitor.src.Core;
using LiteMonitor.src.Plugins;
using LiteMonitor.src.UI.Controls;

namespace LiteMonitor.src.UI.SettingsPage
{
    public class PluginPage : SettingsPageBase
    {
        private Panel _container;

        public PluginPage()
        {
            this.BackColor = UIColors.MainBg;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(0);

            _container = new BufferedPanel 
            { 
                Dock = DockStyle.Fill, 
                AutoScroll = true, 
                Padding = new Padding(20) 
            };
            this.Controls.Add(_container);
        }

        public override void OnShow()
        {
            base.OnShow();
            if (!_isLoaded)
            {
                RebuildUI();
                _isLoaded = true;
            }
        }

        private bool _isLoaded = false;

        private void RebuildUI()
        {
            // ★★★ Fix: Save Scroll Position to prevent jumping to top ★★★
            int savedScroll = _container.VerticalScroll.Value;

            _container.SuspendLayout();
            ClearAndDispose(_container.Controls);

            var templates = PluginManager.Instance.GetAllTemplates();
            // Use Config instead of Settings.Load() to ensure consistency with SettingsForm context
            var instances = Config?.PluginInstances ?? Settings.Load().PluginInstances;

            // 1. Hint Note
            var hint = new LiteNote("⚠️说明：如需修改插件监控目标的显示名称、单位或排序，请前往 [监控项显示] 设置页面");
            hint.Dock = DockStyle.Top;
            var hintWrapper = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 20) };
            hintWrapper.Controls.Add(hint);
            _container.Controls.Add(hintWrapper);
            
            if (instances == null || instances.Count == 0)
            {
                var lbl = new Label { 
                    Text = "暂无插件实例", 
                    AutoSize = true, 
                    ForeColor = UIColors.TextSub, 
                    Location = new Point(UIUtils.S(20), UIUtils.S(60)) 
                };
                _container.Controls.Add(lbl);
            }
            else
            {
                var grouped = instances.GroupBy(i => i.TemplateId);

                foreach (var grp in grouped)
                {
                    var tmpl = templates.FirstOrDefault(t => t.Id == grp.Key);
                    if (tmpl == null) continue;

                    var list = grp.ToList();
                    for (int i = 0; i < list.Count; i++)
                    {
                        var inst = list[i];
                        bool isDefault = (i == 0); 
                        CreatePluginGroup(inst, tmpl, isDefault);
                    }
                }
            }
            
            _container.ResumeLayout();

            // ★★★ Fix: Restore Scroll Position ★★★
            if (savedScroll > 0)
            {
                _container.PerformLayout(); // Ensure scroll range is updated
                _container.AutoScrollPosition = new Point(0, savedScroll);
            }
        }

        private void CreatePluginGroup(PluginInstanceConfig inst, PluginTemplate tmpl, bool isDefault)
        {
            string title = $"{tmpl.Meta.Name} v{tmpl.Meta.Version} (ID: {inst.Id}) by: {tmpl.Meta.Author}";
            var group = new LiteSettingsGroup(title);

            // 1. Header Actions
            if (isDefault)
            {
                var linkCopy = new LiteLink(LanguageManager.T("Menu.Copy") == "Menu.Copy" ? "复制副本" : LanguageManager.T("Menu.Copy"), () => CopyInstance(inst));
                group.AddHeaderAction(linkCopy);
            }
            else
            {
                var linkDel = new LiteLink(LanguageManager.T("Menu.Delete") == "Menu.Delete" ? "删除插件" : LanguageManager.T("Menu.Delete"), () => DeleteInstance(inst));
                linkDel.ForeColor = Color.IndianRed;
                linkDel.SetColor(Color.IndianRed, Color.Red);
                group.AddHeaderAction(linkDel);
            }

            if (!string.IsNullOrEmpty(tmpl.Meta.Description))
            {
                 group.AddHint(tmpl.Meta.Description);
            }

            // 2. Enable Switch
            group.AddToggle(this, tmpl.Meta.Name, 
                () => inst.Enabled, 
                v => {
                    if (inst.Enabled != v) {
                        inst.Enabled = v;
                        SaveAndRestart(inst);
                    }
                }
            );

            // 3. Refresh Rate
            group.AddInt(this, "刷新频率", "s", 
                () => inst.CustomInterval > 0 ? inst.CustomInterval : tmpl.Execution.Interval,
                v => {
                    if (inst.CustomInterval != v) {
                        inst.CustomInterval = v;
                        SaveAndRestart(inst);
                    }
                }
            );

            // Split Inputs
            var globalInputs = tmpl.Inputs.Where(x => x.Scope != "target").ToList();
            var targetInputs = tmpl.Inputs.Where(x => x.Scope == "target").ToList();

            // 4. Global Inputs
            foreach (var input in globalInputs)
            {
                group.AddInput(this, input.Label, 
                    () => inst.InputValues.ContainsKey(input.Key) ? inst.InputValues[input.Key] : input.DefaultValue,
                    v => {
                        string old = inst.InputValues.ContainsKey(input.Key) ? inst.InputValues[input.Key] : input.DefaultValue;
                        if (old != v) {
                            inst.InputValues[input.Key] = v;
                            SaveAndRestart(inst);
                        }
                    }, 
                    input.Placeholder
                );
            }

            // 5. Targets Section
            if (targetInputs.Count > 0)
            {
                if (inst.Targets == null) inst.Targets = new List<Dictionary<string, string>>();
                
                // Ensure at least one target exists for UI display
                if (inst.Targets.Count == 0)
                {
                    var defaultTarget = new Dictionary<string, string>();
                    foreach(var input in targetInputs)
                    {
                        defaultTarget[input.Key] = input.DefaultValue;
                    }
                    inst.Targets.Add(defaultTarget);
                }
                
                for (int i = 0; i < inst.Targets.Count; i++)
                {
                    int index = i; 
                    var targetVals = inst.Targets[i];
                    
                    // Remove Action
                    var linkRem = new LiteLink("移除", () => {
                        inst.Targets.RemoveAt(index);
                        SaveAndRestart(inst);
                        RebuildUI();
                    });
                    linkRem.SetColor(Color.IndianRed, Color.Red);

                    if (inst.Targets.Count <= 1)
                    {
                        linkRem.Enabled = false;
                    }

                    var headerItem = new LiteSettingsItem($"# 监控目标 {index + 1}", linkRem);
                    headerItem.Label.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
                    headerItem.Label.ForeColor = UIColors.Primary;
                    
                    group.AddFullItem(headerItem);

                    foreach (var input in targetInputs)
                    {
                        var val = targetVals.ContainsKey(input.Key) ? targetVals[input.Key] : input.DefaultValue;
                        
                        if (input.Type == "select" && input.Options != null)
                        {
                            group.AddComboPair(this, "  " + input.Label, input.Options,
                                () => targetVals.ContainsKey(input.Key) ? targetVals[input.Key] : input.DefaultValue,
                                v => {
                                    string old = targetVals.ContainsKey(input.Key) ? targetVals[input.Key] : input.DefaultValue;
                                    if (old != v) {
                                        targetVals[input.Key] = v;
                                        SaveAndRestart(inst);
                                    }
                                }
                            );
                        }
                        else
                        {
                            group.AddInput(this, "  " + input.Label, 
                                () => targetVals.ContainsKey(input.Key) ? targetVals[input.Key] : input.DefaultValue,
                                v => {
                                    string old = targetVals.ContainsKey(input.Key) ? targetVals[input.Key] : input.DefaultValue;
                                    if (old != v) {
                                        targetVals[input.Key] = v;
                                        SaveAndRestart(inst);
                                    }
                                }, 
                                input.Placeholder,
                                HorizontalAlignment.Center // ★ Center alignment for target inputs
                            );
                        }
                    }
                }

                // Add Target Button
                var btnAdd = new LiteButton("+ 添加新监控目标", false, true); 
                btnAdd.Click += (s, e) => {
                    var newTarget = new Dictionary<string, string>();
                    if (targetInputs != null)
                    {
                        foreach(var input in targetInputs)
                        {
                            newTarget[input.Key] = input.DefaultValue;
                        }
                    }
                    inst.Targets.Add(newTarget);
                    RebuildUI();
                };
                
                group.AddFullItem(btnAdd);
                btnAdd.Margin = UIUtils.S(new Padding(0, 15, 0, 0));
            }

            AddGroupToPage(group);
        }

        private void AddGroupToPage(LiteSettingsGroup group)
        {
            var wrapper = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 20) };
            wrapper.Controls.Add(group);
            
            // ★★★ Fix for "Cannot set Win32 parent" Crash ★★★
            // The crash occurs because we are adding controls to _container while its parent (SettingsForm._pnlContent) 
            // has layout suspended (WM_SETREDRAW=false). When adding deep hierarchies (PluginPage -> Panel -> LiteSettingsGroup -> Panel -> Controls),
            // WinForms sometimes fails to resolve the HWND chain correctly if the root is not painting.
            
            // By adding to _container directly, we ensure the control tree is built. 
            // The parent handle should be valid because SettingsForm adds PluginPage BEFORE calling OnShow.
            
            _container.Controls.Add(wrapper);
            _container.Controls.SetChildIndex(wrapper, 0);
        }

        private void SaveAndRestart(PluginInstanceConfig inst)
        {
            if (Config != null) Config.Save();
            else Settings.Load().Save();

            PluginManager.Instance.RestartInstance(inst.Id);
        }

        private void CopyInstance(PluginInstanceConfig source)
        {
            var newInst = new PluginInstanceConfig
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                TemplateId = source.TemplateId,
                Enabled = source.Enabled,
                InputValues = new Dictionary<string, string>(source.InputValues),
                CustomInterval = source.CustomInterval
            };
            
            if (source.Targets != null)
            {
                 foreach(var t in source.Targets)
                 {
                     newInst.Targets.Add(new Dictionary<string, string>(t));
                 }
            }

            if (Config != null)
            {
                Config.PluginInstances.Add(newInst);
                Config.Save();
            }
            else
            {
                Settings.Load().PluginInstances.Add(newInst);
                Settings.Load().Save();
            }
            
            PluginManager.Instance.RestartInstance(newInst.Id);
            
            RebuildUI();
        }

        private void DeleteInstance(PluginInstanceConfig inst)
        {
            if (MessageBox.Show("确定要删除此插件副本吗？", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (Config != null)
                {
                    Config.PluginInstances.Remove(inst);
                    Config.Save();
                }
                else
                {
                    Settings.Load().PluginInstances.Remove(inst);
                    Settings.Load().Save();
                }

                PluginManager.Instance.RemoveInstance(inst.Id);
                RebuildUI();
            }
        }
    }
}
