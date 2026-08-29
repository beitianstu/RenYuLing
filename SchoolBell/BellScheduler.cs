using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;


namespace SchoolBell;

public class BellScheduler : IDisposable
{
    // ==================== 供 MainForm 调用的公开属性与事件 ====================
    // 课表加载成功事件
    public event Action<List<ScheduleItem>>? ScheduleLoaded;

    // 课表列表
    public List<ScheduleItem> schedule { get; private set; } = new List<ScheduleItem>();

    // 铃声是否存在状态
    public bool isStartBellExist { get; private set; } = false;
    public bool isEndBellExist { get; private set; } = false;

    // 铃声路径/配置
    public string startBell { get; set; } = "start";
    public string endBell { get; set; } = "end";

    // 是否使用 Voicemeeter 静音控制
    public bool UseVoicemeeterMute { get; set; } = false;

    // 麦克风状态与事件
    public bool MicMuted { get; private set; } = false;
    public event Action<bool>? MicStatusChanged;

    public event Action<string>? OnLog;

    // ==================== 内部状态与调度器 ====================
    private readonly System.Windows.Forms.Timer schedulerTimer;
    private FileSystemWatcher? scheduleWatcher;
    private string watchedSchedulePath = "";

    private readonly SemaphoreSlim playLock = new(1, 1);
    private readonly WasapiAudioPlayer audioPlayer = new();

    private string outputDeviceName = "";
    private readonly HashSet<ScheduleItem> triggeredThisMinute = new();
    private int lastCheckedMinute = -1;

    public bool IsRunning => schedulerTimer.Enabled;

    // Win32 快捷键模拟 (Alt + M)
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_MENU = 0x12;
    private const byte VK_M = 0x4D;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public BellScheduler()
    {
        string bellsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells");
        if (!Directory.Exists(bellsDir))
        {
            Directory.CreateDirectory(bellsDir);
        }

        RefreshBellPaths(startBell, endBell);
        
        try
        {
            audioPlayer.Init("");
        }
        catch { }

        schedulerTimer = new System.Windows.Forms.Timer();
        schedulerTimer.Interval = 1000; // 每秒检查一次
        schedulerTimer.Tick += SchedulerTimer_Tick;
    }

    // ==================== 设备设置 ====================
    public void SetOutputDeviceByName(string deviceName)
    {
        outputDeviceName = deviceName;
        try
        {
            // 关键：调用 audioPlayer 的 Init 初始化 WASAPI 设备
            bool ok = audioPlayer.Init(deviceName);
            Console.WriteLine($"[音频] 切换输出设备到 [{deviceName}], 结果: {(ok ? "成功" : "失败")}");
            OnLog?.Invoke($"[音频] 切换输出设备到 [{deviceName}], 结果: {(ok ? "成功" : "失败")}");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[音频] 初始化设备失败: {ex.Message}");
        }
    }
    // public void SetOutputDeviceByName(string deviceName)
    // {
    //     outputDeviceName = deviceName;
    //     // 如果 WasapiAudioPlayer 具备设置设备方法则调用
    //     try
    //     {
    //         // audioPlayer.SetDevice(deviceName);
    //         OnLog?.Invoke($"[音频] 切换输出设备到: {deviceName}");
    //     }
    //     catch (Exception ex)
    //     {
    //         OnLog?.Invoke($"[音频] 切换设备失败: {ex.Message}");
    //     }
    // }

    // ==================== 课表加载与文件监听 ====================
    public string LoadSchedule(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            MessageBox.Show("请在程序根目录放入 schedule.json 作为课表", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return $"未找到课表文件: {jsonPath}";
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var items = JsonConvert.DeserializeObject<List<ScheduleItem>>(json);

            if (items == null || items.Count == 0)
                return "课表为空";

            schedule = items;
            triggeredThisMinute.Clear();

            // 触发课表加载完成事件供 UI 刷新
            ScheduleLoaded?.Invoke(schedule);

            // 启动文件热重载监听
            StartScheduleWatcher(jsonPath);

            return $"已加载 ({schedule.Count} 条)";
        }
        catch (Exception ex)
        {
            return "加载失败: " + ex.Message;
        }
    }

    private void StartScheduleWatcher(string jsonPath)
    {
        StopScheduleWatcher();
        watchedSchedulePath = jsonPath;

        var dir = Path.GetDirectoryName(jsonPath);
        var fileName = Path.GetFileName(jsonPath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName)) return;

        scheduleWatcher = new FileSystemWatcher(dir, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        scheduleWatcher.Changed += (s, e) =>
        {
            try
            {
                Thread.Sleep(200); // 避免保存冲突
                string json = File.ReadAllText(jsonPath);
                var items = JsonConvert.DeserializeObject<List<ScheduleItem>>(json);
                if (items != null)
                {
                    schedule = items;
                    triggeredThisMinute.Clear();
                    ScheduleLoaded?.Invoke(schedule);
                    OnLog?.Invoke("[配置] 课表文件已热更新。");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[配置] 热更新读取失败: {ex.Message}");
            }
        };
    }

    private void StopScheduleWatcher()
    {
        if (scheduleWatcher != null)
        {
            scheduleWatcher.EnableRaisingEvents = false;
            scheduleWatcher.Dispose();
            scheduleWatcher = null;
        }
    }

    // ==================== 铃声路径检测 ====================
    public void RefreshBellPaths(string startBellName, string endBellName)
    {
        startBell = startBellName;
        endBell = endBellName;

        string? startPath = ResolveBellPath(startBell, "start.mp3");
        string? endPath = ResolveBellPath(endBell, "end.mp3");
        
        isStartBellExist = File.Exists(startPath);
        isEndBellExist = File.Exists(endPath);
    }
    

    public static bool ContainsChinese(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // 检查是否有任何字符落在常用汉字编码范围内
        return path.Any(c => c >= 0x4E00 && c <= 0x9FA5);
    }

    private string? ResolveBellPath(string bellNameOrPath, string defaultFileName)
    {
        // 如果传入的内容包含中文（说明是 UI 提示文字或误传的说明），直接退回默认文件名
        if (string.IsNullOrWhiteSpace(defaultFileName) || ContainsChinese(defaultFileName))
        {
            return null;
        }

        if (File.Exists(bellNameOrPath))
            return bellNameOrPath;

        string inBells = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", bellNameOrPath);
        if (File.Exists(inBells))
            return inBells;

        string inBellsWithExt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", bellNameOrPath + ".mp3");
        if (File.Exists(inBellsWithExt))
            return inBellsWithExt;

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", defaultFileName);
    }

    // ==================== 麦克风控制 ====================
    public void ToggleMic(bool? forceState = null)
    {
        try
        {
            SendAltM();
            MicMuted = forceState ?? !MicMuted;
            MicStatusChanged?.Invoke(MicMuted);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"[麦克风控制异常]: {ex.Message}");
        }
    }

    public void SendAltM()
    {
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_M, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        keybd_event(VK_M, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

// ==================== 音频播放 ====================
    public async Task PlayBellPublicAsync(string bellTypeOrPath, int repeatTimes = 1)
    {
        Console.WriteLine("stage 0");

        try
        {
            RefreshBellPaths(startBell, endBell);
            // 1. 自动解析实际完整路径（支持全路径、文件名、以及无扩展名）
            string? actualPath = ResolveBellPath(bellTypeOrPath, bellTypeOrPath);
            Console.WriteLine(actualPath);
            for (int i = 0; i < Math.Max(1, repeatTimes); i++)
            {
                // 如果还没初始化过设备，先用当前设置的设备名（或空字符串默认设备）初始化一次
                audioPlayer.Init(outputDeviceName);

                // 调用播放
                await audioPlayer.PlayFileAsync(actualPath, 1);
                Console.WriteLine("PlayFileAsync() has been called by PlayBellPublicAsync.");
            }

            Console.WriteLine("stage 1");
            if (actualPath == null)
            {
                MessageBox.Show("未找到铃声文件", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Console.WriteLine(File.Exists(actualPath));
            // 如果仍不存在，尝试在 bells 目录下补全 .mp3 或 .wav 扩展名寻找
            if (!File.Exists(actualPath))
            {
                string inBells = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", bellTypeOrPath);
                if (File.Exists(inBells)) actualPath = inBells;
                else if (File.Exists(inBells + ".mp3")) actualPath = inBells + ".mp3";
                else if (File.Exists(inBells + ".wav")) actualPath = inBells + ".wav";
            }

            Console.WriteLine("stage 2");
            // 检查文件最终是否存在
            if (!File.Exists(actualPath))
            {
                OnLog?.Invoke($"[播放失败] 找不到铃声文件: {bellTypeOrPath} (尝试路径: {actualPath})");
                return;
            }

            // 2. 获取播放锁（等待上一首播放完成，不设过短超时）
            await playLock.WaitAsync();
            Console.WriteLine("stage 3");
            try
            {
                Console.WriteLine("stage 4");
                for (int i = 0; i < Math.Max(1, repeatTimes); i++)
                {
                    // 3. 正确调用 WasapiAudioPlayer 的 PlayFileAsync
                    await audioPlayer.PlayFileAsync(actualPath, 1);
                    Console.WriteLine("PlayFileAsync() has been called by PlayBellPublicAsync.");
                }
            }
            finally
            {
                Console.WriteLine("stage 5");
                // 确保必定释放锁
                playLock.Release();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("stage 6/error");
            Console.WriteLine("================ [播放异常详情] ================");
            Console.WriteLine(ex.ToString()); // 打印完整的堆栈与行号
            Console.WriteLine("================================================");
            OnLog?.Invoke($"[音频播放异常]: {ex.Message}");
        }
    }
    // ==================== 调度器启停与核心循环 ====================
    public void Start()
    {
        lastCheckedMinute = -1;
        triggeredThisMinute.Clear();
        schedulerTimer.Start();
        OnLog?.Invoke("【调度器已启动】");
    }

    public void Stop()
    {
        schedulerTimer.Stop();
        OnLog?.Invoke("【调度器已停止】");
    }

    private void SchedulerTimer_Tick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.Now;

        // 只要进入新的一分钟，清空触发集合，确保下一分钟/下次排程正常打铃
        if (now.Minute != lastCheckedMinute)
        {
            lastCheckedMinute = now.Minute;
            triggeredThisMinute.Clear();
        }

        foreach (var item in schedule)
        {
            if (triggeredThisMinute.Contains(item))
            {
                continue;
            }

            // 匹配时与分
            if (item.Time.Hours == now.Hour && item.Time.Minutes == now.Minute)
            {
                triggeredThisMinute.Add(item); // 记录当前分钟已响，防止同分钟重复触发

                try
                {
                    OnLog?.Invoke($"[打铃] {now:HH:mm} 触发: {item.Type}");

                    if (item.Type == "start")
                    {
                        _ = PlayBellPublicAsync(startBell,3);
                    }
                    else if (item.Type == "end")
                    {
                        _ = PlayBellPublicAsync(endBell);
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"[打铃异常]: {ex.Message}");
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        schedulerTimer.Tick -= SchedulerTimer_Tick;
        schedulerTimer.Dispose();
        StopScheduleWatcher();
        playLock.Dispose();
        audioPlayer.Dispose();
    }
}