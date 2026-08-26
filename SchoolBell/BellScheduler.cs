using System.Runtime.InteropServices;
using NAudio.Wave;
using Newtonsoft.Json;
using Timer = System.Windows.Forms.Timer;

namespace SchoolBell;

public class BellScheduler : IDisposable
{
    private static readonly TimeSpan MaxCatchUpDelay = TimeSpan.FromSeconds(30);
    private readonly string bellDir;
    private readonly HashSet<string> triggeredBellKeys = new();

    private int deviceIndex = -1;

    // WASAPI 常驻播放器：复用单个输出实例，避免反复开关虚拟设备
    private readonly WasapiAudioPlayer audioPlayer = new();
    // 播放串行化信号量：同一时间只播一个，一个播完再播下一个
    private readonly SemaphoreSlim playLock = new(1, 1);
    private string outputDeviceName = "";

    public string startBell = "";
    public string endBell = "";
    public bool isEndBellExist = true;
    public bool isStartBellExist = true;

    private bool micMuted;
    private DateTime lastCheckTime = DateTime.MinValue;
    private DateOnly lastTriggerDate = DateOnly.MinValue;
    public List<ScheduleItem>? schedule;

    private Timer? schedulerTimer;
    private FileSystemWatcher? scheduleWatcher;
    private string? watchedSchedulePath;
    private DateTime lastReloadTime = DateTime.MinValue;

    public BellScheduler()
    {
        bellDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bells");
        Directory.CreateDirectory(bellDir);

        // 初始化时先用空字符串，稍后由 MainForm 调用 RefreshBellPaths
        RefreshBellPaths("", "");
    }

    public event Action<bool>? MicStatusChanged;
    public event Action<List<ScheduleItem>>? ScheduleLoaded;

    public void Dispose()
    {
        Stop();
        StopScheduleWatcher();
        audioPlayer.Dispose();
    }

    // 公开播放：供 MainForm 测试按钮使用（同样排队 + 走 WASAPI）
    public async Task PlayBellPublicAsync(string filePath, int repeatTimes)
    {
        await playLock.WaitAsync();
        try
        {
            await audioPlayer.PlayFileAsync(filePath, repeatTimes);
        }
        finally
        {
            playLock.Release();
        }
    }

    public void ToggleMic(bool? forceState = null)
    {
        // ★ 发送 Alt + M 全局快捷键
        SendAltM();

        // ★ 内部状态切换（腾讯会议是切换式开关）
        micMuted = forceState.HasValue ? !forceState.Value : !micMuted;

        // ★ 通知 UI
        MicStatusChanged?.Invoke(micMuted);
    }

    private static void SendAltM()
    {
        // KEYEVENTF flags
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        // Virtual Keys
        const byte VK_MENU = 0x12; // Alt
        const byte VK_M = 0x4D;

        // 按下 Alt
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYDOWN, 0);

        // 按下 M
        keybd_event(VK_M, 0, KEYEVENTF_KEYDOWN, 0);
        keybd_event(VK_M, 0, KEYEVENTF_KEYUP, 0);

        // 松开 Alt
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    public void RefreshBellPaths(string startBellD, string endBellD)
    {
        (startBell, isStartBellExist) = ResolveBell("start", startBellD);
        (endBell, isEndBellExist) = ResolveBell("end", endBellD);

        Console.WriteLine($"isStartBellExist: {isStartBellExist}, isEndBellExist: {isEndBellExist}");
    }

    private (string path, bool exists) ResolveBell(string baseName, string fallback)
    {
        var defaultPath = ResolveDefaultBellPath(baseName);
        if (defaultPath != null)
            return (defaultPath, true);

        return (fallback, File.Exists(fallback));
    }

    private string? ResolveDefaultBellPath(string baseName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var searchDirectories = new[]
        {
            bellDir,
            Path.Combine(baseDirectory, "bell"),
            baseDirectory
        };

        var fileNames = new[]
        {
            $"{baseName}.mp3",
            $"{baseName}.wav"
        };

        foreach (var dir in searchDirectories.Distinct())
        {
            foreach (var fileName in fileNames)
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    // 设置播放设备（旧接口，按设备号，已弃用，保留兼容）
    public void SetDevice(int index)
    {
        deviceIndex = index;
    }

    // 设置播放设备（新接口，按设备名称，供 WASAPI 使用）
    public void SetOutputDeviceByName(string deviceName)
    {
        outputDeviceName = deviceName;
        var ok = audioPlayer.Init(deviceName);
        Console.WriteLine($"[音频] 切换输出设备到 [{deviceName}]，结果：{(ok ? "成功" : "失败")}");
    }

    // 加载 JSON 课表
    public string LoadSchedule(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            MessageBox.Show("请在程序根目录放入schedule.json作为课表", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return $"未找到课表文件：{jsonPath}";
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var items = JsonConvert.DeserializeObject<List<ScheduleItem>>(json);

            if (items == null || items.Count == 0)
                return "课表为空";

            schedule = items;
            ScheduleLoaded?.Invoke(schedule);

            // 加载成功后开始监听文件变化（热加载）
            StartScheduleWatcher(jsonPath);

            return $"已加载（{schedule.Count} 条）";
        }
        catch (Exception ex)
        {
            return "加载失败：" + ex.Message;
        }
    }

    // ===== 课表热加载：监听 schedule.json 变化并自动重载 =====
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

        // 文件被修改/重命名/创建时都触发
        scheduleWatcher.Changed += OnScheduleFileChanged;
        scheduleWatcher.Created += OnScheduleFileChanged;
        scheduleWatcher.Renamed += OnScheduleFileChanged;

        Console.WriteLine("[热加载] 已开始监听 " + jsonPath);
    }

    private void StopScheduleWatcher()
    {
        if (scheduleWatcher == null) return;

        scheduleWatcher.EnableRaisingEvents = false;
        scheduleWatcher.Changed -= OnScheduleFileChanged;
        scheduleWatcher.Created -= OnScheduleFileChanged;
        scheduleWatcher.Renamed -= OnScheduleFileChanged;
        scheduleWatcher.Dispose();
        scheduleWatcher = null;
    }

    private void OnScheduleFileChanged(object sender, FileSystemEventArgs e)
    {
        // 编辑器保存时可能连续触发多次（先清空再写入），做去抖：
        // 距上次重载太近就忽略
        var now = DateTime.Now;
        if ((now - lastReloadTime).TotalMilliseconds < 500) return;
        lastReloadTime = now;

        // 文件可能正被编辑器占用（写入中），稍等并重试读取
        if (watchedSchedulePath == null) return;

        // 文件系统事件在线程池线程触发，重载逻辑要回到 UI 线程（涉及界面刷新）
        // 用一个短延迟后台任务来重载，避免在事件线程里做文件 IO
        Task.Run(async () =>
        {
            await Task.Delay(300); // 等编辑器写完

            // 重试几次，应对文件被临时锁定
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    var json = File.ReadAllText(watchedSchedulePath);
                    var items = JsonConvert.DeserializeObject<List<ScheduleItem>>(json);
                    if (items == null) return;

                    schedule = items;
                    // 课表变了，清空当天已触发记录，避免新课表该响的不响
                    triggeredBellKeys.Clear();
                    ScheduleLoaded?.Invoke(schedule);
                    Console.WriteLine($"[热加载] 课表已更新（{schedule.Count} 条）");
                    return;
                }
                catch (IOException)
                {
                    // 文件被占用，稍等重试
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[热加载] 解析失败：" + ex.Message);
                    return;
                }
            }
        });
    }

    // 启动铃声调度
    public void Start()
    {
        StopTimer();

        schedulerTimer = new Timer();
        schedulerTimer.Interval = 500;
        schedulerTimer.Tick += (_, _) => CheckBell();
        schedulerTimer.Start();
        lastCheckTime = DateTime.Now;
        lastTriggerDate = DateOnly.FromDateTime(lastCheckTime);
        triggeredBellKeys.Clear();
        Console.WriteLine("Start() has been called.");
        Console.WriteLine(startBell + " " + endBell);
    }

    // 停止铃声调度
    public void Stop()
    {
        StopTimer();
        triggeredBellKeys.Clear();
        lastCheckTime = DateTime.MinValue;
        Console.WriteLine("Stop() has been called.");
    }

    private void StopTimer()
    {
        if (schedulerTimer == null) return;

        schedulerTimer.Stop();
        schedulerTimer.Dispose();
        schedulerTimer = null;
    }

    // 打铃时是否用 Voicemeeter 控制 Strip[0]（Stereo Input 1）静音
    // true = 用 Voicemeeter API 静音输入1（推荐）；false = 退回旧的 Alt+M 开关麦
    public bool UseVoicemeeterMute { get; set; } = true;

    private const int MicStripIndex = 0; // Strip[0] = Stereo Input 1

    private async Task PlayBellWithMicOpenThenMute(string file, int repeatTimes)
    {
        // 排队：同一时间只播一个，一个播完再播下一个，避免并发冲击虚拟设备
        await playLock.WaitAsync();
        var vmMuted = false;
        try
        {
            // 打铃时两个层面同时动作：
            // 1. Voicemeeter：静音 Strip[0]（物理麦克风），让会议里只听到铃声
            if (UseVoicemeeterMute && VoicemeeterRemote.IsLoggedIn && VoicemeeterRemote.IsRunning())
            {
                vmMuted = VoicemeeterRemote.SetStripMute(MicStripIndex, true);
                if (vmMuted)
                    Console.WriteLine("[VM] 已静音 Stereo Input 1");
            }

            // 2. 腾讯会议：Alt+M 打开麦克风，让铃声能传进会议
            ToggleMic(true);

            // 播放铃声（走 WASAPI 常驻输出）
            await audioPlayer.PlayFileAsync(file, repeatTimes);
        }
        finally
        {
            // 复原：Voicemeeter 支路恢复
            if (vmMuted)
            {
                if (VoicemeeterRemote.SetStripMute(MicStripIndex, false))
                    Console.WriteLine("[VM] 已恢复 Stereo Input 1");
            }

            // 复原：腾讯会议麦克风关闭（回到不发言状态）
            ToggleMic(false);

            playLock.Release();
        }
    }

    // 检查是否到达铃声时间
    public void CheckBell()
    {
        if (schedule == null) return;

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (lastTriggerDate != today)
        {
            triggeredBellKeys.Clear();
            lastTriggerDate = today;
        }

        var effectiveLastCheckTime = lastCheckTime == DateTime.MinValue
            ? now.AddMilliseconds(-(schedulerTimer?.Interval ?? 500))
            : lastCheckTime;
        var catchUpStart = now - MaxCatchUpDelay;
        if (effectiveLastCheckTime < catchUpStart)
            effectiveLastCheckTime = catchUpStart;

        foreach (var c in schedule)
        {
            var bellTime = DateTime.Today.Add(c.Time);
            var bellKey = $"{today:yyyyMMdd}:{c.Type}:{c.Time:c}";

            if (triggeredBellKeys.Contains(bellKey))
                continue;

            if (bellTime > effectiveLastCheckTime && bellTime <= now)
            {
                Console.WriteLine("TRIGGERED! Playing Bell...");
                // ★ 根据类型选择铃声
                var bellFile = c.Type == "start" ? startBell : endBell;
                var repeat = c.Type == "start" ? 3 : 1;
                Console.WriteLine(bellFile);
                if (string.IsNullOrWhiteSpace(bellFile) || !File.Exists(bellFile))
                {
                    Console.WriteLine("Bell file not found: " + bellFile);
                    continue;
                }

                triggeredBellKeys.Add(bellKey);

                // ★ 播放前开麦，播放后关麦
                _ = PlayBellWithMicOpenThenMuteSafeAsync(bellFile, repeat);
            }
        }

        lastCheckTime = now;
    }

    private async Task PlayBellWithMicOpenThenMuteSafeAsync(string file, int repeatTimes)
    {
        try
        {
            await PlayBellWithMicOpenThenMute(file, repeatTimes);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to play scheduled bell: " + ex.Message);
        }
    }

    // 异步播放铃声（重复次数）
    public static async Task PlayBell(string filePath, int repeatTimes, int deviceNumber = -1)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("找不到指定的音频文件", filePath);

        for (var i = 0; i < repeatTimes; i++)
            await PlayOnceAsync(filePath, deviceNumber);
    }

    // 播放一次铃声
    // 关键：用「正常结束」和「超时兜底」双保险释放资源，防止 PlaybackStopped
    // 不触发（设备被抢占/状态异常）时 WaveOut/AudioFileReader 永久泄漏
    private static async Task PlayOnceAsync(string filePath, int deviceNumber)
    {
        // 资源先声明为 null，在 try 内创建。
        // 这样即使创建/Init 抛异常，finally 也能把已创建的部分释放，杜绝泄漏。
        AudioFileReader? audioFile = null;
        WaveOut? output = null;

        try
        {
            audioFile = new AudioFileReader(filePath);
            output = new WaveOut { DeviceNumber = deviceNumber };

            // 计算音频时长，超时设为它 + 缓冲。读不到时长就用 15 秒兜底
            var timeout = audioFile.TotalTime + TimeSpan.FromSeconds(5);
            if (timeout <= TimeSpan.Zero || timeout.TotalSeconds > 60)
                timeout = TimeSpan.FromSeconds(15);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            output.PlaybackStopped += (_, e) =>
            {
                if (e.Exception != null)
                    tcs.TrySetException(e.Exception);
                else
                    tcs.TrySetResult(true);
            };

            output.Init(audioFile);
            output.Play();

            // 等待「播放结束」或「超时」，哪个先到算哪个
            var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

            if (finished != tcs.Task)
            {
                // 超时：PlaybackStopped 没触发，强制停止并释放
                Console.WriteLine("[播放] 超时兜底：强制释放音频资源 " + Path.GetFileName(filePath));
                try { output.Stop(); } catch { /* 忽略停止时的异常 */ }
            }
            else
            {
                // 正常结束：把播放异常（如果有）抛出去
                await tcs.Task;
            }
        }
        finally
        {
            // 无论成功/超时/异常（包括 Init 抛异常），已创建的资源都必须释放
            try { output?.Dispose(); } catch { }
            try { audioFile?.Dispose(); } catch { }
        }
    }
}
