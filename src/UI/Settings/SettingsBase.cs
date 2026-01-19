using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LiteMonitor.src.Core;
using LiteMonitor.src.UI.Controls;

namespace LiteMonitor.src.UI.SettingsPage
{
    public interface ISettingsPage
    {
        void Save();
        void OnShow();
    }

    public class SettingsPageBase : UserControl, ISettingsPage
    {
        protected Settings Config;
        protected MainForm MainForm;
        protected UIController UI;
        
        // ★★★ Staging Mechanism for Deferred Save ★★★
        protected List<Action> _saveActions = new List<Action>();
        // ★★★ Refresh Mechanism for Deferred Load ★★★
        protected List<Action> _refreshActions = new List<Action>();

        public static readonly Color GlobalBackColor = Color.FromArgb(249, 249, 249); 

        public SettingsPageBase() 
        {
            this.BackColor = GlobalBackColor; 
            this.Dock = DockStyle.Fill;
            this.DoubleBuffered = true;
        }

        public void SetContext(Settings cfg, MainForm form, UIController ui)
        {
            Config = cfg;
            MainForm = form;
            UI = ui;
        }

        public void RegisterDelaySave(Action action)
        {
            _saveActions.Add(action);
        }

        public void RegisterRefresh(Action action)
        {
            _refreshActions.Add(action);
        }

        public virtual void OnShow()
        {
            // Base implementation can be empty or used for common logic
            // Note: Do NOT clear _saveActions here, as OnShow is called even when _isLoaded is true.
            
            // Execute refresh actions to ensure UI reflects the latest Config
            if (Config != null)
            {
                foreach (var action in _refreshActions)
                {
                    action.Invoke();
                }
            }
        }

        public virtual void Save()
        {
            // Execute all deferred save actions
            foreach (var action in _saveActions)
            {
                action.Invoke();
            }
        }

        protected void ClearAndDispose(Control.ControlCollection controls)
        {
            // Clear pending actions whenever we destroy the UI controls
            _saveActions.Clear();

            while (controls.Count > 0)
            {
                var c = controls[0];
                controls.RemoveAt(0);
                c.Dispose();
            }
        }

        protected void EnsureSafeVisibility(LiteCheck chkHideMain, LiteCheck chkHideTray, LiteCheck chkShowTaskbar)
        {
            bool hideMain = chkHideMain != null ? chkHideMain.Checked : Config.HideMainForm;
            bool hideTray = chkHideTray != null ? chkHideTray.Checked : Config.HideTrayIcon;
            bool showBar  = chkShowTaskbar != null ? chkShowTaskbar.Checked : Config.ShowTaskbar;

            if (hideMain && hideTray && !showBar)
            {
                MessageBox.Show("为了防止程序无法唤出，不能同时隐藏 [主界面]、[托盘图标] 和 [任务栏]。", 
                                "安全警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                
                if (chkHideMain != null) chkHideMain.Checked = false;
                if (chkHideTray != null) chkHideTray.Checked = false;
                if (chkShowTaskbar != null) chkShowTaskbar.Checked = true;
            }
        }
    }
}
