using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SchoolBell;

public static class PlayBigDog
{
    // resourceName: 嵌入资源的完整名称（通过 GetManifestResourceNames 确认）
    // repeatTimes: 重复次数
    // volume: 0.0f - 1.0f
    public static async Task PlayEmbeddedResourceAsync(string resourceName, int repeatTimes = 1, float volume = 1.0f)
    {
        var asm = Assembly.GetExecutingAssembly();

        for (var i = 0; i < repeatTimes; i++)
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Embedded resource not found: " + resourceName);

                // 复制到 MemoryStream 以保证可寻址（Mp3FileReader 需要）
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    ms.Position = 0;

                    if (IsWav(ms))
                    {
                        ms.Position = 0;
                        using (var reader = new WaveFileReader(ms))
                        using (var output = new WaveOutEvent())
                        {
                            var sampleProvider = reader.ToSampleProvider();
                            var volumeProvider = new VolumeSampleProvider(sampleProvider) { Volume = volume };
                            output.Init(volumeProvider);
                            output.Play();
                            while (output.PlaybackState == PlaybackState.Playing)
                                await Task.Delay(50);
                        }
                    }
                    else
                    {
                        ms.Position = 0;
                        using (var reader = new Mp3FileReader(ms))
                        using (var output = new WaveOutEvent())
                        {
                            var sampleProvider = reader.ToSampleProvider();
                            var volumeProvider = new VolumeSampleProvider(sampleProvider) { Volume = volume };
                            output.Init(volumeProvider);
                            output.Play();
                            while (output.PlaybackState == PlaybackState.Playing)
                                await Task.Delay(50);
                        }
                    }
                }
            }
    }

    private static bool IsWav(Stream s)
    {
        var pos = s.Position;
        s.Position = 0;
        var header = new byte[4];
        var read = s.Read(header, 0, 4);
        s.Position = pos;
        if (read < 4) return false;
        return header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F';
    }
}