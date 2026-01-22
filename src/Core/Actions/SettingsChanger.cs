using System.Linq;
using LiteMonitor.src.Core;
using System.Reflection;
using System.Collections.Generic;
namespace LiteMonitor.src.Core.Actions
{
    /// <summary>
    /// 封装所有修改 Settings 对象的逻辑。
    /// 支持草稿/提交 (Draft/Commit) 架构。
    /// 使用反射的简化版本。
    /// </summary>
    public static class SettingsChanger
    {
        /// <summary>
        /// 使用反射将草稿设置 (Draft) 合并到实时设置 (Live) 中。
        /// 保留在黑名单中定义的仅运行时属性。
        /// </summary>
        public static void Merge(Settings live, Settings draft)
        {
            if (live == null || draft == null) return;

            // 不应被草稿覆盖的属性黑名单 (运行时数据)
            var runtimeProps = new HashSet<string>
            {
                // 最大记录值
                "RecordedMaxCpuPower", "RecordedMaxCpuClock",
                "RecordedMaxGpuPower", "RecordedMaxGpuClock",
                "RecordedMaxCpuFan", "RecordedMaxCpuPump",
                "RecordedMaxGpuFan", "RecordedMaxChassisFan",
                
                // 其他运行时状态
                "LastAutoNetwork", "LastAutoDisk",
                "ScreenDevice", "MaxLimitTipShown",
                
                // 流量统计 (累加值)
                "TotalUpload", "TotalDownload",
                "SessionUploadBytes", "SessionDownloadBytes",
                
                // 时间戳
                "LastAutoSaveTime", "LastAlertTime"
            };

            var props = typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanWrite || !p.CanRead) continue;
                
                // 1. 跳过黑名单中的运行时属性
                if (runtimeProps.Contains(p.Name)) continue;
                
                // 2. 特殊处理集合类型
                if (p.Name == "MonitorItems")
                {
                    UpdateMonitorList(live, draft.MonitorItems, draft.HorizontalFollowsTaskbar);
                    continue;
                }
                if (p.Name == "PluginInstances")
                {
                    live.PluginInstances = new List<PluginInstanceConfig>(draft.PluginInstances);
                    continue;
                }
                if (p.Name == "Thresholds")
                {
                    // 阈值是简单对象，只要不需要部分合并，直接赋值即可
                    live.Thresholds = draft.Thresholds; 
                    continue;
                }
                // 3. 特殊处理字典类型
                 if (p.Name == "GroupAliases")
                {
                     live.GroupAliases = new Dictionary<string, string>(draft.GroupAliases);
                     continue;
                }

                // 3. 默认：直接复制
                // 处理所有 bool, int, string, enum 等类型
                var val = p.GetValue(draft);
                p.SetValue(live, val);
            }
        }

        /// <summary>
        /// 基于 UI 的工作列表更新目标 Settings 对象中的 MonitorItems 列表。
        /// 处理合并逻辑以保留动态属性 (如 DynamicLabel)。
        /// </summary>
        public static void UpdateMonitorList(Settings target, List<MonitorItemConfig> workingList, bool horizontalFollowsTaskbar)
        {
            if (target == null || workingList == null) return;

            target.HorizontalFollowsTaskbar = horizontalFollowsTaskbar;

            // 合并逻辑
            var activeKeys = new HashSet<string>(target.MonitorItems.Select(x => x.Key));
            
            // 1. 获取配置中存在的项 (保留 UI 排序/更改)
            var mergedList = workingList.Where(x => activeKeys.Contains(x.Key)).ToList();

            // 2. 添加配置中出现但工作列表中缺失的新项 
            var workingKeys = new HashSet<string>(workingList.Select(x => x.Key));
            var newItems = target.MonitorItems.Where(x => !workingKeys.Contains(x.Key)).ToList();
            
            if (newItems.Count > 0)
            {
                mergedList.AddRange(newItems);
            }
            
            target.MonitorItems = mergedList;
        }

        /// <summary>
        /// 向设置中添加新的插件实例。
        /// </summary>
        public static void AddPlugin(Settings target, PluginInstanceConfig plugin)
        {
            if (target == null || plugin == null) return;
            target.PluginInstances.Add(plugin);
        }

        /// <summary>
        /// 从设置中移除插件实例。
        /// </summary>
        public static void RemovePlugin(Settings target, PluginInstanceConfig plugin)
        {
            if (target == null || plugin == null) return;
            target.PluginInstances.Remove(plugin);
        }
    }
}
