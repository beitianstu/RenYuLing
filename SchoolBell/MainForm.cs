using System.Diagnostics;
using System.Text;
using NAudio.Wave;
using Timer = System.Windows.Forms.Timer;

namespace SchoolBell;

public partial class MainForm : Form
{
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(300);
    private DateTime _lastClick = DateTime.MinValue;

    private Timer? clockTimer; // 显示当前时间
    private readonly BellScheduler scheduler;
    private readonly Timer uiTimer; // 刷新倒计时与下一次铃声

    public MainForm()
    {
        _ = PlayBigDogSafeAsync();
        MessageBox.Show("欢迎使用 任禹铃(TM) 自动打铃软件");

        InitializeComponent();
        LoadAudioDevices();

        btnStart.Click += btnStart_Click;
        btnStop.Click += btnStop_Click;
        btnChooseStartBell.Click += btnChooseStartBell_Click;
        btnChooseEndBell.Click += btnChooseEndBell_Click;
        btnTestStartBell.Click += btnTestStartBell_Click;
        btnTestEndBell.Click += btnTestEndBell_Click;
        Pause.Click += Pause_Click;
        Resume.Click += Resume_Click;

        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        SizeGripStyle = SizeGripStyle.Hide;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        scheduler = new BellScheduler();
        scheduler.MicStatusChanged += OnMicStatusChanged;
        scheduler.ScheduleLoaded += OnScheduleLoaded;

        uiTimer = new Timer();
        uiTimer.Interval = 500;
        uiTimer.Tick += UiTimer_Tick;
        uiTimer.Start();

        Shown += MainForm_Shown;
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        clockTimer = new Timer();
        clockTimer.Interval = 500;
        clockTimer.Tick += ClockTimer_Tick;
        clockTimer.Start();

        // 登录 Voicemeeter 并启动实时监测
        InitVoicemeeter();

        var status = scheduler.LoadSchedule(Path.Combine(AppContext.BaseDirectory, "schedule.json"));
        lblScheduleStatus.Text = "课表：" + status;

        UpdateScheduleDisplay();
        UpdateRunStatus("NOT RUNNING");
        UpdateNextBellUI();
        UpdateBellFileExistState();
    }

    // ===== Voicemeeter 实时监测 =====
    private Timer? vmMonitorTimer;
    private bool lastVmRunning;

    private void InitVoicemeeter()
    {
        var loggedIn = VoicemeeterRemote.Login();
        lastVmRunning = loggedIn && VoicemeeterRemote.IsRunning();
        UpdateVmStatusUI(lastVmRunning, loggedIn);

        // 每 2 秒检测一次 Voicemeeter 是否还在运行
        vmMonitorTimer = new Timer { Interval = 2000 };
        vmMonitorTimer.Tick += VmMonitor_Tick;
        vmMonitorTimer.Start();
    }

    private void VmMonitor_Tick(object? sender, EventArgs e)
    {
        var loggedIn = VoicemeeterRemote.IsLoggedIn;
        var running = loggedIn && VoicemeeterRemote.IsRunning();

        // 状态变化才更新 UI 和日志
        if (running != lastVmRunning)
        {
            lastVmRunning = running;
            Console.WriteLine(running ? "[VM] Voicemeeter 已连接" : "[VM] Voicemeeter 已断开");
            UpdateVmStatusUI(running, loggedIn);
        }
    }

    private void UpdateVmStatusUI(bool running, bool loggedIn)
    {
        // 在麦克风状态标签上显示 Voicemeeter 连接状态
        if (!loggedIn)
            lblMicStatus.Text = "VM：未找到DLL";
        else if (!running)
            lblMicStatus.Text = "VM：未运行";
        else
            lblMicStatus.Text = "VM：已连接";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        scheduler.MicStatusChanged -= OnMicStatusChanged;
        scheduler.ScheduleLoaded -= OnScheduleLoaded;
        scheduler.Dispose();

        vmMonitorTimer?.Stop();
        vmMonitorTimer?.Dispose();
        VoicemeeterRemote.Logout();

        clockTimer?.Stop();
        clockTimer?.Dispose();
        uiTimer.Stop();
        uiTimer.Dispose();

        base.OnFormClosing(e);
    }

    private void OnScheduleLoaded(List<ScheduleItem> _)
    {
        // 热加载时此事件在后台线程触发，需切回 UI 线程再更新控件
        if (InvokeRequired)
        {
            BeginInvoke(UpdateScheduleDisplay);
            return;
        }
        UpdateScheduleDisplay();
    }

    // UI 定时器：刷新下一次铃声与倒计时
    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        UpdateNextBellUI();
    }

    // 获取下一次铃声时间
    private DateTime? GetNextBellTime()
    {
        var s = scheduler.schedule;
        if (s == null || s.Count == 0)
            return null;

        var now = DateTime.Now;

        var next = s
            .Where(c => DateTime.Today.Add(c.Time) >= now)
            .OrderBy(c => c.Time)
            .FirstOrDefault();

        if (next == null)
            return null;

        return DateTime.Today.Add(next.Time);
    }

    private void UpdateBellFileExistState()
    {
        if (scheduler.isStartBellExist)
        {
            btnChooseStartBell.Enabled = false;
            txtStartBell.Text = "已检测到默认上课铃（bells/bell/程序目录）";
            txtStartBell.Enabled = false;
        }

        if (scheduler.isEndBellExist)
        {
            btnChooseEndBell.Enabled = false;
            txtEndBell.Text = "已检测到默认下课铃（bells/bell/程序目录）";
            txtEndBell.Enabled = false;
        }
    }

    // 刷新下一次铃声与倒计时
    private void UpdateNextBellUI()
    {
        var now = DateTime.Now;
        var nextBell = GetNextBellTime();

        if (nextBell == null)
        {
            lblNextBell.Text = "今天课程已结束";
            lblCountdown.Text = "";
            return;
        }

        var remain = nextBell.Value - now;

        lblNextBell.Text = $"下一次：{nextBell.Value:HH:mm}";
        lblCountdown.Text = $"倒计时 {remain.Hours:D2}:{remain.Minutes:D2}:{remain.Seconds:D2}";
    }

    // 加载音频设备（用 WASAPI 设备名列表）
    private void LoadAudioDevices()
    {
        comboDevice.Items.Clear();
        foreach (var name in WasapiAudioPlayer.ListOutputDeviceNames())
            comboDevice.Items.Add(name);

        // 默认选中 Voicemeeter Input（排除 AUX 和 VAIO3），找不到再用第一个设备
        comboDevice.SelectedIndex = FindVoicemeeterInputIndex();
    }

    // 在设备列表里找 "Voicemeeter Input"，但排除 "AUX" 和 "VAIO3"
    private int FindVoicemeeterInputIndex()
    {
        for (var i = 0; i < comboDevice.Items.Count; i++)
        {
            var name = comboDevice.Items[i]?.ToString() ?? "";
            if (!name.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("AUX", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("VAIO3", StringComparison.OrdinalIgnoreCase)) continue;
            return i;
        }

        // 找不到 Voicemeeter Input 就用第一个设备
        return comboDevice.Items.Count > 0 ? 0 : -1;
    }

    // 当前选中的输出设备名
    private string SelectedDeviceName => comboDevice.SelectedItem?.ToString() ?? "";

    // 启动
    private void btnStart_Click(object? sender, EventArgs e)
    {
        scheduler.RefreshBellPaths(txtStartBell.Text, txtEndBell.Text);
        scheduler.SetOutputDeviceByName(SelectedDeviceName);
        scheduler.Start();
        Console.WriteLine("STARTED!");
        MessageBox.Show("已启动");
        UpdateRunStatus("RUNNING");
    }

    // 停止
    private void btnStop_Click(object? sender, EventArgs e)
    {
        scheduler.Stop();
        Console.WriteLine("STOPPED!");
        MessageBox.Show("已停止");
        UpdateRunStatus("STOPPED");
    }

    private void Pause_Click(object? sender, EventArgs e)
    {
        OpenFocusMode();
    }

    private void Resume_Click(object? sender, EventArgs e)
    {
        scheduler.Stop();
        UpdateRunStatus("STOPPED");
        OpenFocusMode();
    }

    private void OpenFocusMode()
    {
        var borderless = new BorderlessForm(scheduler);
        borderless.FormClosed += (_, _) => Show();
        Hide();
        borderless.Show();
    }

    private async Task PlayBellSafelyAsync(string path)
    {
        try
        {
            // 测试时确保输出设备已按当前下拉框选择初始化（未启动也能测）
            scheduler.SetOutputDeviceByName(SelectedDeviceName);
            await scheduler.PlayBellPublicAsync(path, 1);
            Console.WriteLine("Test Sound Should Be Played");
        }
        catch (Exception ex)
        {
            MessageBox.Show("播放失败：" + ex.Message);
        }
    }

    private async Task PlayBigDogJiaoSafeAsync()
    {
        try
        {
            await PlayBigDog.PlayEmbeddedResourceAsync("SchoolBell.sound.dagoujiao.wav", 1, 0.9f);
        }
        catch (Exception ex)
        {
            Console.WriteLine("PlayBigDogJiaoSafeAsync() Failed: " + ex.Message);
        }
    }

    private async Task PlayBigDogSafeAsync()
    {
        try
        {
            await PlayBigDog.PlayEmbeddedResourceAsync("SchoolBell.sound.bigdog.wav", 1, 0.9f);
        }
        catch (Exception ex)
        {
            Console.WriteLine("PlayBigDogSafeAsync() Failed: " + ex.Message);
        }
    }

    // 选择铃声文件
    private void ChooseBell(TextBox target)
    {
        using var dlg = new OpenFileDialog { Filter = "音频文件|*.wav;*.mp3" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        target.Text = dlg.FileName;
        scheduler.RefreshBellPaths(txtStartBell.Text, txtEndBell.Text);
    }

    private void btnChooseStartBell_Click(object? sender, EventArgs e)
    {
        ChooseBell(txtStartBell);
    }

    private void btnChooseEndBell_Click(object? sender, EventArgs e)
    {
        ChooseBell(txtEndBell);
    }

    // 显示当前时间
    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        lblTime.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void TestBell(string path)
    {
        scheduler.RefreshBellPaths(txtStartBell.Text, txtEndBell.Text);
        _ = PlayBellSafelyAsync(path);
        Hit(pictureBox1, 500, 0.91);
    }

    private void btnTestEndBell_Click(object? sender, EventArgs e)
    {
        TestBell(scheduler.endBell);
    }

    private void btnTestStartBell_Click(object? sender, EventArgs e)
    {
        TestBell(scheduler.startBell);
    }

    // 显示课表
    private void UpdateScheduleDisplay()
    {
        var s = scheduler.schedule;

        if (s == null || s.Count == 0)
        {
            txtboxClassTable.Text = "课表为空";
            return;
        }

        var sb = new StringBuilder();
        foreach (var item in s)
        {
            var t = item.Type switch
            {
                "start" => "上课",
                "end" => "下课",
                _ => "默认"
            };
            sb.AppendLine($"{item.Time:hh\\:mm}     {t}");
        }

        txtboxClassTable.Text = sb.ToString();
    }

    private void UpdateRunStatus(string status)
    {
        lblRunStatus.Text = "状态：" + status;
        lblRunStatus.ForeColor = status == "RUNNING" ? Color.FromArgb(60, 180, 100) : Color.FromArgb(230, 90, 90);
    }

    private void OnMicStatusChanged(bool muted)
    {
        // Voicemeeter 模式下，lblMicStatus 显示的是 VM 连接状态，不被 Alt+M 覆盖
        if (scheduler.UseVoicemeeterMute && VoicemeeterRemote.IsRunning())
            return;
        lblMicStatus.Text = muted ? "麦克风：已静音" : "麦克风：已开启";
    }

    //pictureBox任禹翰敲击动画
    public static void Hit(PictureBox pb, int durationMs = 220, double amplitude = 0.18,
        double damping = 7.0, double cycles = 2.2)
    {
        if (pb.Tag is Timer oldTimer)
        {
            oldTimer.Stop();
            oldTimer.Dispose();
        }

        var timer = new Timer { Interval = 15 };
        pb.Tag = timer;

        var maxHeight = pb.Height;
        var fixedBottom = pb.Bottom;
        var sw = Stopwatch.StartNew();

        timer.Tick += (_, _) =>
        {
            if (pb.IsDisposed)
            {
                timer.Stop();
                timer.Dispose();
                pb.Tag = null;
                return;
            }

            var t = sw.Elapsed.TotalMilliseconds / durationMs;
            if (t >= 1) t = 1;

            var u = t;
            var scaleY = 1.0 - amplitude * Math.Exp(-damping * u) * Math.Cos(2.0 * Math.PI * cycles * u);

            var h = (int)(maxHeight * scaleY);
            pb.Height = h;
            pb.Top = fixedBottom - pb.Height;

            if (t >= 1)
            {
                pb.Height = maxHeight;
                pb.Top = fixedBottom - pb.Height;

                timer.Stop();
                timer.Dispose();
                pb.Tag = null;
            }
        };

        timer.Start();
    }

    private void pictureBox1_Click(object? sender, EventArgs e)
    {
        _ = PlayBigDogJiaoSafeAsync();
        Hit(pictureBox1, 500, 0.91);
    }
}