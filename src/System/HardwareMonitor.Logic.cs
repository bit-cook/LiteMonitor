using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using LibreHardwareMonitor.Hardware;
using LiteMonitor.src.Core;
using Debug = System.Diagnostics.Debug;
namespace LiteMonitor.src.SystemServices
{
    // 依然是 HardwareMonitor 的一部分
    public sealed partial class HardwareMonitor
    {
        // ===========================================================
        // ===================== 公共取值入口 =========================
        // ===========================================================
        public float? Get(string key)
        {
            EnsureMapFresh();

            // 1. 网络与磁盘 (独立逻辑)
            switch (key)
            {
                case "NET.Up": case "NET.Down": return GetNetworkValue(key);
                case "DISK.Read": case "DISK.Write": return GetDiskValue(key);
            }

            // ★★★ [新增] 获取今日流量 (从 TrafficLogger 拿) ★★★
            if (key == "DATA.DayUp")
            {
                return TrafficLogger.GetTodayStats().up;
            }
            if (key == "DATA.DayDown")
            {
                return TrafficLogger.GetTodayStats().down;
            }

            // 2. 频率与功耗 (复合计算逻辑)
            if (key.Contains("Clock") || key.Contains("Power"))
            {
                return GetHardwareCompositeValue(key);
            }

            // ★ 修改：增强的内存计算逻辑
            if (key == "MEM.Load")
            {
                // 只要还没探测到，就尝试探测
                if (Settings.DetectedRamTotalGB <= 0)
                {
                    lock (_lock)
                    {
                        if (_map.TryGetValue("MEM.Used", out var u) && _map.TryGetValue("MEM.Available", out var a))
                        {
                            if (u.Value.HasValue && a.Value.HasValue)
                            {
                                
                                float rawTotal = u.Value.Value + a.Value.Value;
                                // 如果数值 > 512，认为是 MB，除以 1024；否则认为是 GB (Data 类型通常直接是 GB)
                                Settings.DetectedRamTotalGB = rawTotal > 512.0f ? rawTotal / 1024.0f : rawTotal;
                            }
                        }
                    }
                }
            }

            // 3. 显存百分比 (特殊计算)
            if (key == "GPU.VRAM")
            {
                float? used = Get("GPU.VRAM.Used");
                float? total = Get("GPU.VRAM.Total");
                if (used.HasValue && total.HasValue && total > 0)
                {
                    // 假设 total 是 MB (LHM SmallData 默认是 MB)这里统一转成 GB 存起来
                    if (Settings.DetectedGpuVramTotalGB <= 0)
                    {
                        Settings.DetectedGpuVramTotalGB = total.Value / 1024f;
                    }

                    // 简单单位换算防止数值过大溢出 (虽 float 够用，但为了逻辑统一)
                    if (total > 10485760) { used /= 1048576f; total /= 1048576f; }
                    return used / total * 100f;
                }
                // Fallback: 如果有 Load 传感器直接用
                lock (_lock) { if (_map.TryGetValue("GPU.VRAM.Load", out var s) && s.Value.HasValue) return s.Value; }
                return null;
            }

            // 4. 普通传感器 (直接读字典)
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var sensor))
                {
                    var val = sensor.Value;
                    if (val.HasValue && !float.IsNaN(val.Value))
                    {
                        _lastValid[key] = val.Value;
                        return val.Value;
                    }
                    if (_lastValid.TryGetValue(key, out var last)) return last;
                }
            }

            return null;
        }

        // ===========================================================
        // ========= [核心算法] CPU/GPU 频率功耗复合计算 ==============
        // ===========================================================
        private float? GetHardwareCompositeValue(string key)
        {
            // --- CPU 频率：加权平均算法 ---
            if (key == "CPU.Clock")
            {
                if (_cpuCoreCache.Count == 0) return null;

                double weightedSum = 0;
                double totalLoad = 0;
                float maxRawClock = 0;
                float validCoreCount = 0; // 新增：记录读到频率的核心数
                double sumRawClock = 0;   // 新增：记录频率总和（用于算简单平均）
                // ★★★ [Zen 5 科学修正准备] ★★★
                float correctionFactor = 1.0f;
                // 检查总线频率是否异常 (正常是 100MHz，如果小于 20MHz 肯定是 LHM 驱动 Bug)
                if (_cpuBusSpeedSensor != null && _cpuBusSpeedSensor.Value.HasValue)
                {
                    float bus = _cpuBusSpeedSensor.Value.Value;
                    
                    // 捕捉 15.3MHz 这种异常值 (排除 0 和极小干扰)
                    if (bus > 1.0f && bus < 20.0f) 
                    {
                        // 动态计算修正系数：把当前读数还原回 100MHz 标准
                        float factor = 100.0f / bus;
                        
                        // [安全钳制] 正常修正大概在 6.5 倍左右 (100/15.3 ≈ 6.53)
                        // 如果算出来系数过大 (如 >10)，可能是传感器读数错误，不予修正，防止显示爆炸
                        if (factor > 2.0f && factor < 10.0f) 
                        {
                            correctionFactor = factor;
                        }
                    }
                }
                // 遍历缓存，零 GC，极速
                foreach (var core in _cpuCoreCache)
                {
                    if (core.Clock == null || !core.Clock.Value.HasValue) continue;

                    // ★★★ [应用修正] ★★★
                    // 如果触发了 Bug，这里会自动乘以系数还原；否则系数为 1.0 (无影响)
                    float clk = core.Clock.Value.Value * correctionFactor;

                    if (clk > maxRawClock) maxRawClock = clk;
                    // 累加基础数据
                    sumRawClock += clk;
                    validCoreCount++;

                    // 加权逻辑
                    if (core.Load != null && core.Load.Value.HasValue)
                    {
                        float ld = core.Load.Value.Value;
                        weightedSum += clk * ld;
                        totalLoad += ld;
                    }
                }

                // 记录物理最高频 (用于颜色自适应)
                if (maxRawClock > 0) _cfg.UpdateMaxRecord(key, maxRawClock);

                // 现逻辑：如果算不出加权平均（因为没负载），就返回简单平均值；如果简单平均也没有，返回最高频
                if (totalLoad <= 0.001) 
                {
                    if (validCoreCount > 0) return (float)(sumRawClock / validCoreCount);
                    return maxRawClock > 0 ? maxRawClock : 0;
                }

                return (float)(weightedSum / totalLoad);
            }

            // --- CPU 功耗：直接读取或回落 ---
            if (key == "CPU.Power")
            {
                // 优先从 Map 读 (NormalizeKey 已处理 Package 映射)
                lock (_lock)
                {
                    if (_map.TryGetValue("CPU.Power", out var s) && s.Value.HasValue)
                    {
                        _cfg.UpdateMaxRecord(key, s.Value.Value);
                        return s.Value.Value;
                    }
                }
                return null;
            }

            // --- GPU 频率/功耗：使用显卡缓存 ---
            if (key.StartsWith("GPU"))
            {
                if (_cachedGpu == null) return null;

                ISensor? s = null;
                // 注意：GPU 传感器少，LINQ 查询开销可忽略
                if (key == "GPU.Clock")
                { 
                    // 增加 "shader" 匹配
                    s = _cachedGpu.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Clock && 
                        (Has(x.Name, "graphics") || Has(x.Name, "core") || Has(x.Name, "shader")));
                    
                    // ★★★ 【修复 1】频率异常过滤 ★★★
                    // 如果读数为 0 (休眠) 是正常的，但如果超过 6000MHz (6GHz) 肯定是传感器抽风
                    if (s != null && s.Value.HasValue)
                    {
                        float val = s.Value.Value;
                        if (val > 6000.0f) return null; // 过滤异常高频
                        
                        _cfg.UpdateMaxRecord(key, val);
                        return val;
                    }
                }
                else if (key == "GPU.Power")
                {
                    // 增加 "core" 匹配
                    s = _cachedGpu.Sensors.FirstOrDefault(x => x.SensorType == SensorType.Power && 
                        (Has(x.Name, "package") || Has(x.Name, "ppt") || Has(x.Name, "board") || Has(x.Name, "core") || Has(x.Name, "gpu")));

                    // ★★★ 【修复 2】功耗异常过滤 (你的问题核心) ★★★
                    // 消费级显卡瞬间功耗不可能超过 1500W (4090 峰值也就 600W 左右)
                    // 16368W 显然是错误数据，直接丢弃
                    if (s != null && s.Value.HasValue)
                    {
                        float val = s.Value.Value;
                        
                        // 设定一个 2000W 的安全阀值
                        if (val > 2000.0f) return null; 

                        _cfg.UpdateMaxRecord(key, val);
                        return val;
                    }
                }
            }

            return null;
        }

        // ===========================================================
        // ==================== 网络 (Network) =======================
        // ===========================================================
        private float? GetNetworkValue(string key)
        {
            // 1. 优先手动指定
            if (!string.IsNullOrWhiteSpace(_cfg.PreferredNetwork))
            {
                var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Network && h.Name.Equals(_cfg.PreferredNetwork, StringComparison.OrdinalIgnoreCase));
                if (hw != null) return ReadNetworkSensor(hw, key);
            }

            // 2. 自动选优 (带缓存)
            return GetBestNetworkValue(key);
        }

        private float? GetBestNetworkValue(string key)
        {
            // A. 尝试运行时缓存
            if (_cachedNetHw != null)
            {
                float? cachedVal = ReadNetworkSensor(_cachedNetHw, key);
                // 逻辑优化：
                // 1. 如果有流量，直接用。
                // 2. 如果没流量，但距离上次全盘扫描还不到 3 秒 (配合 UpdateAll 的节奏)，也直接用 0。
                //    不要每秒都去扫，那样太耗 CPU。
                if ((cachedVal.HasValue && cachedVal.Value > 0.1f) || 
                    (DateTime.Now - _lastNetScan).TotalSeconds < 3) 
                {
                    return cachedVal;
                }
                // 如果超过 3 秒还是没流量，说明可能切网卡了，放行到底部去全盘扫描
            }

            // ★★★ [漏掉的部分] B. 尝试启动时缓存 (Settings 中的记录) ★★★
            // 确保 Settings.cs 里已经定义了 public string LastAutoNetwork { get; set; } = "";
            if (_cachedNetHw == null && !string.IsNullOrEmpty(_cfg.LastAutoNetwork))
            {
                // 尝试直接找上次记住的网卡
                var savedHw = _computer.Hardware.FirstOrDefault(h => h.Name == _cfg.LastAutoNetwork);
                if (savedHw != null)
                {
                    // 找到了！直接设为缓存，跳过全盘扫描
                    _cachedNetHw = savedHw;
                    _lastNetScan = DateTime.Now;
                    return ReadNetworkSensor(savedHw, key);
                }
            }

            // C. 全盘扫描 (代码保持不变)
            IHardware? bestHw = null;
            double bestScore = double.MinValue;
            ISensor? bestTarget = null;

            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Network))
            {
                // ... (你的原有扫描逻辑) ...
                // ... (复制你文件里 foreach 的内容) ...
                double penalty = _virtualNicKW.Any(k => Has(hw.Name, k)) ? -1e9 : 0;
                ISensor? up = null, down = null;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != SensorType.Throughput) continue;
                    if (_upKW.Any(k => Has(s.Name, k))) up ??= s;
                    if (_downKW.Any(k => Has(s.Name, k))) down ??= s;
                }
                if (up == null && down == null) continue;
                double score = (up?.Value ?? 0) + (down?.Value ?? 0) + penalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHw = hw;
                    bestTarget = (key == "NET.Up") ? up : down;
                }
            }

            // D. 更新缓存
            if (bestHw != null)
            {
                _cachedNetHw = bestHw;
                _lastNetScan = DateTime.Now;
                
                // ★★★ [漏掉的部分] 记住这次的选择 ★★★
                if (_cfg.LastAutoNetwork != bestHw.Name)
                {
                    _cfg.LastAutoNetwork = bestHw.Name;
                }
            }

            // ... (返回结果部分保持不变) ...
            if (bestTarget?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private float? ReadNetworkSensor(IHardware hw, string key)
        {
            ISensor? target = null;
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType != SensorType.Throughput) continue;
                if (key == "NET.Up" && _upKW.Any(k => Has(s.Name, k))) { target = s; break; } // 找到即停
                if (key == "NET.Down" && _downKW.Any(k => Has(s.Name, k))) { target = s; break; }
            }

            if (target?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private static readonly string[] _upKW = { "upload", "up", "sent", "send", "tx", "transmit" };
        private static readonly string[] _downKW = { "download", "down", "received", "receive", "rx" };
        private static readonly string[] _virtualNicKW = { "virtual", "vmware", "hyper-v", "hyper v", "vbox", "loopback", "tunnel", "tap", "tun", "bluetooth", "zerotier", "tailscale", "wan miniport" };

        // ===========================================================
        // ===================== 磁盘 (Disk) =========================
        // ===========================================================
        private float? GetDiskValue(string key)
        {
            if (!string.IsNullOrWhiteSpace(_cfg.PreferredDisk))
            {
                var hw = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Storage && h.Name.Equals(_cfg.PreferredDisk, StringComparison.OrdinalIgnoreCase));
                if (hw != null) return ReadDiskSensor(hw, key);
            }
            return GetBestDiskValue(key);
        }

        private float? GetBestDiskValue(string key)
        {
            // A. 尝试运行时缓存
            if (_cachedDiskHw != null)
            {
                float? cachedVal = ReadDiskSensor(_cachedDiskHw, key);
                // 有读写活动或冷却期内，直接返回
                if ((cachedVal.HasValue && cachedVal.Value > 0.1f) || (DateTime.Now - _lastDiskScan).TotalSeconds < 10)
                    return cachedVal;
            }

            // ★★★ [新增] B. 尝试启动时缓存 (Settings 记忆) ★★★
            if (_cachedDiskHw == null && !string.IsNullOrEmpty(_cfg.LastAutoDisk))
            {
                var savedHw = _computer.Hardware.FirstOrDefault(h => h.Name == _cfg.LastAutoDisk);
                if (savedHw != null)
                {
                    // 命中缓存！跳过全盘扫描
                    _cachedDiskHw = savedHw;
                    _lastDiskScan = DateTime.Now;
                    return ReadDiskSensor(savedHw, key);
                }
            }

            // C. 全盘扫描 (逻辑保持不变)
            string sysPrefix = "";
            try { sysPrefix = Path.GetPathRoot(Environment.SystemDirectory)?.Substring(0, 2) ?? ""; } catch { }

            IHardware? bestHw = null;
            double bestScore = double.MinValue;
            ISensor? bestTarget = null;

            foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage))
            {
                // ... (复制你原有的扫描逻辑) ...
                bool isSystem = !string.IsNullOrEmpty(sysPrefix) && (Has(hw.Name, sysPrefix) || hw.Sensors.Any(s => Has(s.Name, sysPrefix)));
                
                ISensor? read = null, write = null;
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != SensorType.Throughput) continue;
                    if (Has(s.Name, "read")) read ??= s;
                    if (Has(s.Name, "write")) write ??= s;
                }

                if (read == null && write == null) continue;

                double score = (read?.Value ?? 0) + (write?.Value ?? 0);
                if (isSystem) score += 1e9; // 系统盘优先

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHw = hw;
                    bestTarget = (key == "DISK.Read") ? read : write;
                }
            }

            // D. 更新缓存
            if (bestHw != null)
            {
                _cachedDiskHw = bestHw;
                _lastDiskScan = DateTime.Now;
                
                // ★★★ [新增] 记住这次的选择 ★★★
                if (_cfg.LastAutoDisk != bestHw.Name)
                {
                    _cfg.LastAutoDisk = bestHw.Name;
                }
            }

            // E. 返回结果
            if (bestTarget?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        private float? ReadDiskSensor(IHardware hw, string key)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType != SensorType.Throughput) continue;
                if (key == "DISK.Read" && Has(s.Name, "read")) return SafeRead(s, key);
                if (key == "DISK.Write" && Has(s.Name, "write")) return SafeRead(s, key);
            }
            return SafeRead(null, key);
        }

        private float? SafeRead(ISensor? s, string key)
        {
            if (s?.Value is float v && !float.IsNaN(v))
            {
                lock (_lock) _lastValid[key] = v;
                return v;
            }
            lock (_lock) { if (_lastValid.TryGetValue(key, out var last)) return last; }
            return null;
        }

        // ===========================================================
        // ================== 辅助 / 映射 (Helpers) ===================
        // ===========================================================
        
        // 静态工具：菜单使用
        public static List<string> ListAllNetworks() => Instance?._computer.Hardware.Where(h => h.HardwareType == HardwareType.Network).Select(h => h.Name).Distinct().ToList() ?? new List<string>();
        public static List<string> ListAllDisks() => Instance?._computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).Select(h => h.Name).Distinct().ToList() ?? new List<string>();

        // [重要] 传感器名称标准化映射
        private static string? NormalizeKey(IHardware hw, ISensor s)
        {
            string name = s.Name;
            var type = hw.HardwareType;

            // --- CPU ---
            if (type == HardwareType.Cpu)
            {
                if (s.SensorType == SensorType.Load && Has(name, "total")) return "CPU.Load";
                // [深度优化后的温度匹配逻辑]
                if (s.SensorType == SensorType.Temperature)
                {
                    // 1. 黄金标准：包含这些词的通常就是我们要的
                    if (Has(name, "package") ||  // Intel/AMD 标准
                        Has(name, "average") ||  // LHM 聚合数据
                        Has(name, "tctl") ||     // AMD 风扇控制温度 (最准)
                        Has(name, "tdie") ||     // AMD 核心硅片温度
                        Has(name, "ccd") ||       // AMD 核心板
                        Has(name, "cores"))     // 通用核心温度
                    {
                        return "CPU.Temp";
                    }

                    // 2. 银牌标准：通用名称兜底 (修复 AMD 7840HS 等移动端 CPU)
                    // 必须严格排除干扰项 (如 SOC, VRM, Pump 等)
                    if ((Has(name, "cpu") || Has(name, "core")) && 
                        !Has(name, "soc") &&     // 排除核显/片上系统
                        !Has(name, "vrm") &&     // 排除供电
                        !Has(name, "fan") &&     // 排除风扇(虽类型不同，但防名字干扰)
                        !Has(name, "pump") &&    // 排除水泵
                        !Has(name, "liquid") &&  // 排除水冷液
                        !Has(name, "coolant") && // 排除冷却液
                        !Has(name, "distance"))  // 排除 "Distance to TjMax"
                    {
                        // 注意：这里可能会匹配到 "Core #1"，虽然不是 Package，
                        // 但在没有 Package 传感器的情况下，这是唯一的有效读数。
                        return "CPU.Temp";
                    }
                }
                if (s.SensorType == SensorType.Power && (Has(name, "package") || Has(name, "cores"))) return "CPU.Power";
                // 注意：Clock 不走 Map，走加权平均缓存，所以这里不需要映射
            }

            // --- GPU ---
            if (type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            {
                if (s.SensorType == SensorType.Load && (Has(name, "core") || Has(name, "d3d 3d"))) return "GPU.Load";
                if (s.SensorType == SensorType.Temperature && (Has(name, "core") || Has(name, "hot spot") || Has(name, "soc") || Has(name, "vr"))) return "GPU.Temp";
                
                // VRAM
                if (s.SensorType == SensorType.SmallData && (Has(name, "memory") || Has(name, "dedicated")))
                {
                    if (Has(name, "used")) return "GPU.VRAM.Used";
                    if (Has(name, "total")) return "GPU.VRAM.Total";
                }
                if (s.SensorType == SensorType.Load && Has(name, "memory")) return "GPU.VRAM.Load";
            }

            // --- Memory ---
            if (type == HardwareType.Memory) 
            {
                // 1. 负载 (保持不变)
                if (s.SensorType == SensorType.Load && Has(name, "memory")) return "MEM.Load";
                
                // 2. ★ 增强版匹配：同时接受 Data 和 SmallData
                if (s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData)
                {
                    if (Has(name, "used")) return "MEM.Used";
                    if (Has(name, "available")) return "MEM.Available";
                }
            }

            return null;
        }

        private static bool Has(string source, string sub)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sub)) return false;
            return source.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}