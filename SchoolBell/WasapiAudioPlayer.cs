using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SchoolBell;

// 基于 WASAPI 的常驻音频播放器：
// 程序启动时创建一个 WasapiOut 实例并保持运行，打铃时把铃声混进持续输出流，
// 不反复创建/销毁输出设备，避免虚拟设备（Voicemeeter）因高频开关而劣化。
public class WasapiAudioPlayer : IDisposable
{
    private WasapiOut? output;
    private MixingSampleProvider? mixer;
    private MMDevice? device;
    private string deviceFriendlyName = "";
    private readonly object sync = new();

    public bool IsReady { get; private set; }

    // 用设备友好名称（如 "Voicemeeter Input"）初始化；找不到则失败
    public bool Init(string deviceNamePart)
    {
        lock (sync)
        {
            try
            {
                DisposeLocked();

                device = FindDevice(deviceNamePart);
                if (device == null)
                {
                    Console.WriteLine("[音频] 找不到设备：" + deviceNamePart);
                    IsReady = false;
                    return false;
                }

                deviceFriendlyName = device.FriendlyName;

                // 共享模式、非事件回调，持续输出
                output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);

                // 混音源：用设备混音格式，读不到数据时自动补静音（保持流不断）
                mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2))
                {
                    ReadFully = true
                };

                output.Init(mixer);
                output.Play();

                IsReady = true;
                Console.WriteLine("[音频] WASAPI 输出已启动：" + deviceFriendlyName);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[音频] 初始化失败：" + ex.Message);
                IsReady = false;
                return false;
            }
        }
    }

    // 按名称片段查找播放设备（渲染端、激活状态）
    private static MMDevice? FindDevice(string namePart)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var d in devices)
        {
            if (d.FriendlyName.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                return d;
        }
        return null;
    }

    // 列出所有可用播放设备名（给 UI 下拉框用）
    public static List<string> ListOutputDeviceNames()
    {
        var names = new List<string>();
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var d in devices)
            names.Add(d.FriendlyName);
        return names;
    }

    // 播放一个音频文件，重复 repeatTimes 次，播完返回
    public async Task PlayFileAsync(string filePath, int repeatTimes)
    {
        if (!IsReady || mixer == null)
            throw new InvalidOperationException("音频输出未初始化");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("找不到音频文件", filePath);

        // 兜底超时：文件时长 + 2 秒 + 10% 估算余量。
        // 无论哪条路径挂住（比如播放中途 Init 切设备把旧混音器孤儿化），都能在有限时间内返回，
        // 避免上层 BellScheduler 的 playLock 被永久持有。
        TimeSpan duration;
        using (var probe = new AudioFileReader(filePath))
            duration = probe.TotalTime;
        var timeout = duration + TimeSpan.FromSeconds(2 + duration.TotalSeconds * 0.1);

        for (var i = 0; i < repeatTimes; i++)
        {
            var playTask = PlayOnceInternalAsync(filePath);
            var completed = await Task.WhenAny(playTask, Task.Delay(timeout));
            if (completed != playTask)
                throw new TimeoutException($"铃声播放超过 {timeout.TotalSeconds:F1}s 未完成，放弃等待（可能是播放中切换了输出设备）");
            await playTask; // 播放中的异常从这里传播出去
        }
    }

    private Task PlayOnceInternalAsync(string filePath)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var reader = new AudioFileReader(filePath);

            // 转成与 mixer 相同的采样率/声道，保证能混进去
            ISampleProvider provider = reader;
            if (reader.WaveFormat.SampleRate != 48000 || reader.WaveFormat.Channels != 2)
            {
                provider = new WdlResamplingSampleProvider(reader, 48000);
                if (provider.WaveFormat.Channels == 1)
                    provider = new MonoToStereoSampleProvider(provider);
            }

            lock (sync)
            {
                if (mixer == null)
                {
                    reader.Dispose();
                    tcs.TrySetException(new InvalidOperationException("混音器已释放"));
                    return tcs.Task;
                }

                var mixerRef = mixer;

                // 注意：MixingSampleProvider 在某次 Read 不满 count 时（包括文件结尾的部分读取）
                // 就把输入移出混音器，输入永远不会被读到返回 0。
                // 所以“播完”信号必须挂 MixerInputEnded（输入被移除时触发），不能用读到 0 判断。
                void OnInputEnded(object? s, SampleProviderEventArgs e)
                {
                    if (!ReferenceEquals(e.SampleProvider, provider)) return;
                    mixerRef.MixerInputEnded -= OnInputEnded;
                    reader.Dispose();
                    tcs.TrySetResult(true);
                }

                mixerRef.MixerInputEnded += OnInputEnded;
                try
                {
                    mixerRef.AddMixerInput(provider);
                }
                catch
                {
                    mixerRef.MixerInputEnded -= OnInputEnded;
                    reader.Dispose();
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    private void DisposeLocked()
    {
        try { output?.Stop(); } catch { }
        try { output?.Dispose(); } catch { }
        output = null;
        mixer = null;
        device?.Dispose();
        device = null;
        IsReady = false;
    }

    public void Dispose()
    {
        lock (sync)
        {
            DisposeLocked();
        }
    }
}