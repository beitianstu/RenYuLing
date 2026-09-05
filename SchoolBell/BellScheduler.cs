using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
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
            Console.WriteLine($"[Audio] Changed Output device to [{deviceName}], Result: {(ok ? "Success" : "Fail")}");
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
        Console.WriteLine("ScheduleWatcher Started.");

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
        Console.WriteLine("Bell Path Refreshed!");
        startBell = startBellName;
        endBell = endBellName;

        string? startPath = ResolveBellPath(startBell, "start.mp3","start");
        string? endPath = ResolveBellPath(endBell, "end.mp3","end");
        
        isStartBellExist = File.Exists(startPath);
        isEndBellExist = File.Exists(endPath);
        Console.WriteLine("start:" + startPath + " end:" +  endPath);
    }
    

    public static bool ContainsChinese(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // 检查是否有任何字符落在常用汉字编码范围内
        return path.Any(c => c >= 0x4E00 && c <= 0x9FA5);
    }

    private string? ResolveBellPath(string bellNameOrPath, string defaultFileName, string bellType)
    {
        string? finalPath = null;
        if (bellType == "Test")
            bellType = "start";
        // 如果传入的内容包含中文（说明是 UI 提示文字或误传的说明），直接退回默认文件名
        if (string.IsNullOrWhiteSpace(bellNameOrPath) || ContainsChinese(bellNameOrPath))
        {
            Console.WriteLine("Path Inclueds Chinese.   " + defaultFileName);
            string inBellsWithExt = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", bellType + ".mp3");
            Console.WriteLine("inBellsWithExt: " + inBellsWithExt);
            if (File.Exists(inBellsWithExt))
                 finalPath = inBellsWithExt;
        }
        else
        {
            Console.WriteLine("else");
            if (File.Exists(bellNameOrPath))
            {
                finalPath = bellNameOrPath;
                
            }else
            {
                finalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells", defaultFileName);
            }
        }

        return finalPath;
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
public async Task PlayBellPublicAsync(string bellTypeOrPath, string bellType, int repeatTimes = 1)
{
    // Test 类型按上课铃的默认铃声处理
    if (bellType == "Test") bellType = "start";

    Console.WriteLine($"stage 0 | input: {bellTypeOrPath} | type: {bellType} | repeat: {repeatTimes}");

    try
    {
        // ---------- 1. 解析路径：全部校验通过后才抢锁、才动麦克风 ----------
        // 第二个参数必须传固定的默认文件名，绝不能传被污染的 bellTypeOrPath
        string defaultFile = bellType == "end" ? "end.mp3" : "start.mp3";
        string? actualPath = ResolveBellPath(bellTypeOrPath, defaultFile, bellType);
        
        Console.WriteLine("actualPath: " + actualPath);
        
        if (string.IsNullOrEmpty(actualPath) || !File.Exists(actualPath))
        {
            OnLog?.Invoke($"[播放失败] 找不到铃声文件: {bellTypeOrPath} (type: {bellType})");
            Console.WriteLine("actualPath is null or not exists, abort.");
            return;
        }
        Console.WriteLine("stage 1");
        // ---------- 2. 带超时抢锁：最多等 30 秒，等不到就放弃本次 ----------
        bool lockAcquired = false;
        try
        {
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));
            lockAcquired = await playLock.WaitAsync(TimeSpan.FromSeconds(30));
            if (!lockAcquired)
            {
                OnLog?.Invoke(DateTime.Now.ToString("HH:mm:ss") + "[播放跳过] 等待上一段铃声超时（30s），放弃本次播放");
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "Bell Skippped due to lockAcquired");
                return;
            }

            Console.WriteLine("stage 2 | TRIGGERED! Playing Bell: " + actualPath);

            // ---------- 3. 麦克风切换放进 try/finally，任何情况都恢复 ----------
            ToggleMic();
            try
            {
                for (int i = 0; i < Math.Max(1, repeatTimes); i++)
                {
                    await audioPlayer.PlayFileAsync(actualPath, 1);
                    Console.WriteLine($"PlayFileAsync() 第 {i + 1}/{Math.Max(1, repeatTimes)} 次完成");
                }
            }
            finally
            {
                ToggleMic();   // 播放成功或抛异常，麦克风状态必定恢复
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("================ [播放异常详情] ================");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("================================================");
            OnLog?.Invoke($"[音频播放异常]: {ex.Message}");
        }
        finally
        {
            // ---------- 4. 只在真正拿到过锁时释放，防止误放他人的锁 ----------
            if (lockAcquired)
            {
                playLock.Release();
                Console.WriteLine("stage 3 | lock released");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("stage 4/error");
        Console.WriteLine("================ [播放异常详情] ================");
        Console.WriteLine(ex.ToString());
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
                        _ = PlayBellPublicAsync(startBell,"start",3);
                    }
                    else if (item.Type == "end")
                    {
                        _ = PlayBellPublicAsync(endBell,"end");
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